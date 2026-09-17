using System.Xml.Linq;

namespace Bodyslide.Core;

internal sealed record ResolvedBodySlideSliders(IReadOnlyList<string> Sliders, IReadOnlyList<string> ZapSliders, string Gender);

internal static class BodySlideSourceProjectSupport
{
    private static readonly IReadOnlyList<string> DefaultSliders = ["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth"];
    private static readonly StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;

    private static readonly IReadOnlyDictionary<string, string> ZapSliderHints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bra"] = "HideBra",
            ["panty"] = "HidePanties",
            ["panties"] = "HidePanties",
            ["underwear"] = "HidePanties",
            ["sleeve"] = "HideSleeves",
            ["cape"] = "HideCape",
            ["cloak"] = "HideCloak",
            ["hood"] = "HideHood",
            ["mask"] = "HideMask"
        };

    private static readonly string[] ZapNamePrefixes =
    [
        "hide", "remove", "toggle", "strip", "delete"
    ];

    public static async Task<ResolvedBodySlideSliders> ResolveAsync(
        ImportedArmor armor,
        string targetBody,
        CancellationToken cancellationToken)
    {
        var customProfileFound = CustomBodyProfileSupport.TryGetProfile(armor, targetBody, out var customProfile);
        var baseSliders = customProfileFound
            ? customProfile.SliderNames ?? DefaultSliders
            : BuiltInBodyMetadataCatalog.TryGet(targetBody, out var metadata) && metadata.SliderNames.Count > 0
                ? metadata.SliderNames
                : DefaultSliders;
        var sourceSupport = await ExtractSourceSupportAsync(armor, cancellationToken);
        var mergedSliders = MergeSliderLists(baseSliders, sourceSupport.Sliders);

        var zapSliders = new HashSet<string>(customProfile?.ZapSliderNames ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var slider in sourceSupport.ZapSliders)
        {
            zapSliders.Add(slider);
        }

        var sliderSet = mergedSliders.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var meshFile in armor.MeshFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(meshFile) ?? string.Empty;
            foreach (var (hint, zapName) in ZapSliderHints)
            {
                if (fileName.Contains(hint, StringComparison.OrdinalIgnoreCase) && !sliderSet.Contains(zapName))
                {
                    zapSliders.Add(zapName);
                }
            }
        }

        zapSliders.ExceptWith(sliderSet);

        var gender = customProfileFound && customProfile is not null
            ? string.Equals(customProfile.Gender, "male", StringComparison.OrdinalIgnoreCase) ? "male" : "female"
            : BuiltInBodyMetadataCatalog.TryGet(targetBody, out var builtInMetadata)
                ? builtInMetadata.Gender
                : "female";

        return new ResolvedBodySlideSliders(
            mergedSliders,
            zapSliders.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            gender);
    }

    private static async Task<BodySlideSourceSupport> ExtractSourceSupportAsync(
        ImportedArmor armor,
        CancellationToken cancellationToken)
    {
        var sliders = new List<string>();
        var zapSliders = new List<string>();

        foreach (var filePath in EnumerateAssociatedBodySlideFiles(armor))
        {
            var extension = Path.GetExtension(filePath);
            if (extension.Equals(".osp", StringComparison.OrdinalIgnoreCase))
            {
                var fromOsp = await TryReadOspAsync(filePath, cancellationToken);
                sliders.AddRange(fromOsp.Sliders);
                zapSliders.AddRange(fromOsp.ZapSliders);
            }
            else if (extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadBsdSlider(filePath, out var sliderName, out var isZap) &&
                    !string.IsNullOrWhiteSpace(sliderName))
                {
                    if (isZap)
                    {
                        zapSliders.Add(sliderName);
                    }
                    else
                    {
                        sliders.Add(sliderName);
                    }
                }
            }
            else if (extension.Equals(".tri", StringComparison.OrdinalIgnoreCase) &&
                     TriMorphReader.TryRead(filePath, out var triPayload) &&
                     triPayload is not null)
            {
                foreach (var morph in triPayload.Morphs)
                {
                    var sliderName = NormalizeSliderFileName(morph.Name);
                    if (!ShouldIncludePayloadSlider(sliderName, morph.Deltas, out var isZap))
                    {
                        continue;
                    }

                    if (isZap)
                    {
                        zapSliders.Add(sliderName);
                    }
                    else
                    {
                        sliders.Add(sliderName);
                    }
                }
            }
        }

        return new BodySlideSourceSupport(
            sliders.Where(IsLikelySliderName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            zapSliders.Where(IsLikelySliderName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IEnumerable<string> EnumerateAssociatedBodySlideFiles(ImportedArmor armor)
    {
        var sourceRoot = ResolveSourceRoot(armor.SourcePath);
        var meshTokens = armor.MeshFiles
            .Select(NormalizeMeshToken)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var explicitFiles = armor.BodyReferenceFiles
            .Where(static path =>
                path.EndsWith(".osp", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".bsd", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".tri", StringComparison.OrdinalIgnoreCase));

        var discoveredFiles = Directory.Exists(sourceRoot)
            ? Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var extension = Path.GetExtension(path);
                    return extension.Equals(".osp", StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(".tri", StringComparison.OrdinalIgnoreCase);
                })
                .Where(path => IsAssociatedWithArmor(path, meshTokens))
            : [];

        return explicitFiles
            .Concat(discoveredFiles)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveSourceRoot(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return sourcePath;
        }

        return Path.GetDirectoryName(sourcePath) ?? sourcePath;
    }

    private static bool IsAssociatedWithArmor(string filePath, IReadOnlyList<string> meshTokens)
    {
        if (meshTokens.Count == 0)
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
        if (meshTokens.Any(token => fileName.Contains(token, PathComparison)))
        {
            return true;
        }

        var directoryPath = Path.GetDirectoryName(filePath) ?? string.Empty;
        return meshTokens.Any(token => directoryPath.Contains(token, PathComparison));
    }

    private static async Task<BodySlideSourceSupport> TryReadOspAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            var sliders = new List<string>();
            var zapSliders = new List<string>();

            foreach (var sliderElement in document.Descendants("Slider"))
            {
                var name = sliderElement.Attribute("name")?.Value?.Trim();
                if (!IsLikelySliderName(name))
                {
                    continue;
                }

                var isZap = IsTruthy(sliderElement.Attribute("zap")?.Value);
                if (isZap)
                {
                    zapSliders.Add(name!);
                }
                else
                {
                    sliders.Add(name!);
                }
            }

            return new BodySlideSourceSupport(
                sliders.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                zapSliders.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return new BodySlideSourceSupport([], []);
        }
    }

    private static string NormalizeMeshToken(string meshFilePath)
    {
        var token = Path.GetFileNameWithoutExtension(meshFilePath) ?? meshFilePath;
        return token.EndsWith("_0", StringComparison.OrdinalIgnoreCase) || token.EndsWith("_1", StringComparison.OrdinalIgnoreCase)
            ? token[..^2]
            : token;
    }

    private static IReadOnlyList<string> MergeSliderLists(IReadOnlyList<string> primary, IReadOnlyList<string> secondary)
    {
        var merged = new List<string>(primary.Count + secondary.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slider in primary.Concat(secondary))
        {
            if (!IsLikelySliderName(slider) || !seen.Add(slider))
            {
                continue;
            }

            merged.Add(slider);
        }

        return merged;
    }

    private static string NormalizeSliderFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var normalized = fileName.Trim();
        if (normalized.EndsWith("_0", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("_1", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^2];
        }

        return normalized;
    }

    private static bool TryReadBsdSlider(string filePath, out string sliderName, out bool isZap)
    {
        if (BsdMorphReader.TryRead(filePath, out var payload) && payload is not null)
        {
            sliderName = NormalizeSliderFileName(payload.SliderName);
            return ShouldIncludePayloadSlider(sliderName, payload.Deltas, out isZap);
        }

        sliderName = NormalizeSliderFileName(Path.GetFileNameWithoutExtension(filePath));
        isZap = IsLikelyZapSliderName(sliderName);
        return isZap && !string.IsNullOrWhiteSpace(sliderName);
    }

    private static bool ShouldIncludePayloadSlider(
        string sliderName,
        IReadOnlyList<(float X, float Y, float Z)> deltas,
        out bool isZap)
    {
        isZap = IsLikelyZapSliderName(sliderName);
        if (string.IsNullOrWhiteSpace(sliderName))
        {
            return false;
        }

        return isZap || MorphPayloadAnalysis.HasMeaningfulDeltas(deltas);
    }

    private static bool IsLikelyZapSliderName(string sliderName)
    {
        if (string.IsNullOrWhiteSpace(sliderName))
        {
            return false;
        }

        foreach (var prefix in ZapNamePrefixes)
        {
            if (sliderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            for (var i = 1; i < sliderName.Length; i++)
            {
                if (!IsTokenBoundary(sliderName, i))
                {
                    continue;
                }

                if (sliderName.AsSpan(i).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTokenBoundary(string value, int index)
    {
        var current = value[index];
        var previous = value[index - 1];
        return current is '_' or '-' or ' ' ||
               previous is '_' or '-' or ' ' ||
               char.IsUpper(current) && !char.IsUpper(previous);
    }

    private static bool IsTruthy(string? value) =>
        value is not null &&
        (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static bool IsLikelySliderName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return candidate.Length >= 2 && candidate.Any(char.IsLetter);
    }

    private sealed record BodySlideSourceSupport(IReadOnlyList<string> Sliders, IReadOnlyList<string> ZapSliders);
}
