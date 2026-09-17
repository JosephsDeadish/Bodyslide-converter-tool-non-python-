using System.Xml.Linq;

namespace Bodyslide.Core;

internal sealed record ResolvedBodySlideSliders(IReadOnlyList<string> Sliders, IReadOnlyList<string> ZapSliders, string Gender);

internal static class BodySlideSourceProjectSupport
{
    private static readonly IReadOnlyList<string> DefaultSliders = ["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth"];

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

    public static ResolvedBodySlideSliders Resolve(ImportedArmor armor, string targetBody)
    {
        var customProfileFound = CustomBodyProfileSupport.TryGetProfile(armor, targetBody, out var customProfile);
        var baseSliders = customProfileFound
            ? customProfile.SliderNames ?? DefaultSliders
            : BuiltInBodyMetadataCatalog.TryGet(targetBody, out var metadata) && metadata.SliderNames.Count > 0
                ? metadata.SliderNames
                : DefaultSliders;
        var sourceSupport = ExtractSourceSupport(armor.BodyReferenceFiles);
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

        var gender = customProfileFound
            ? string.Equals(customProfile.Gender, "male", StringComparison.OrdinalIgnoreCase) ? "male" : "female"
            : BuiltInBodyMetadataCatalog.TryGet(targetBody, out var builtInMetadata)
                ? builtInMetadata.Gender
                : "female";

        return new ResolvedBodySlideSliders(
            mergedSliders,
            zapSliders.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            gender);
    }

    private static BodySlideSourceSupport ExtractSourceSupport(IReadOnlyList<string> bodyReferenceFiles)
    {
        var sliders = new List<string>();
        var zapSliders = new List<string>();

        foreach (var filePath in bodyReferenceFiles
                     .Where(static path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var extension = Path.GetExtension(filePath);
            if (extension.Equals(".osp", StringComparison.OrdinalIgnoreCase))
            {
                var fromOsp = TryReadOsp(filePath);
                sliders.AddRange(fromOsp.Sliders);
                zapSliders.AddRange(fromOsp.ZapSliders);
            }
            else if (extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase))
            {
                var sliderName = NormalizeSliderFileName(Path.GetFileNameWithoutExtension(filePath));
                if (!string.IsNullOrWhiteSpace(sliderName))
                {
                    sliders.Add(sliderName);
                }
            }
        }

        return new BodySlideSourceSupport(
            sliders.Where(IsLikelySliderName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            zapSliders.Where(IsLikelySliderName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static BodySlideSourceSupport TryReadOsp(string filePath)
    {
        try
        {
            var document = XDocument.Load(filePath, LoadOptions.None);
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
        if (normalized.EndsWith("_1", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^2];
        }

        return normalized;
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
