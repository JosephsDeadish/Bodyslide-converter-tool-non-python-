using System.Xml.Linq;

namespace Bodyslide.Core;

internal sealed record ResolvedBodySlideSliders(
    IReadOnlyList<string> Sliders,
    IReadOnlyList<string> ZapSliders,
    string Gender,
    SourceMorphQualityMetrics? SourceMorphQuality = null,
    IReadOnlyDictionary<string, SourceMorphPayloadVariants>? ReusableMorphPayloads = null,
    SourceAssetSupportMetrics? SourceAssetSupport = null);

internal static class BodySlideSourceProjectSupport
{
    private static readonly IReadOnlyList<string> DefaultSliders = ["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth"];
    private static readonly StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;
    private static readonly SourceSliderCandidate EmptyCandidate = new(string.Empty, 0);

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
        var inferredSourceBody = sourceSupport.Sliders.Count == 0
            ? InferFallbackSourceBody(armor, targetBody)
            : null;
        var mergedSliders = MergeSliderLists(baseSliders, sourceSupport.Sliders);
        if (sourceSupport.Sliders.Count == 0 &&
            inferredSourceBody is not null &&
            BuiltInBodyMetadataCatalog.TryGet(inferredSourceBody.BodyName, out var inferredMetadata) &&
            inferredMetadata.SliderNames.Count > 0)
        {
            mergedSliders = MergeSliderLists(mergedSliders, inferredMetadata.SliderNames);
        }

        var zapSliders = new HashSet<string>(customProfile?.ZapSliderNames ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var slider in sourceSupport.ZapSliders.Select(static candidate => candidate.Name))
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
            gender,
            sourceSupport.SourceMorphQuality,
            sourceSupport.ReusableMorphPayloads,
            sourceSupport.BuildAssetSupport(
                baseSliders.Count > 0 && sourceSupport.Sliders.Count == 0,
                armor.BodyReferenceFiles.Count > 0,
                inferredSourceBody));
    }

    private static async Task<BodySlideSourceSupport> ExtractSourceSupportAsync(
        ImportedArmor armor,
        CancellationToken cancellationToken)
    {
        var sliders = new List<SourceSliderCandidate>();
        var zapSliders = new List<SourceSliderCandidate>();
        var hasOsp = false;
        var hasTriPayloads = false;
        var hasBsdPayloads = false;

        foreach (var filePath in EnumerateAssociatedBodySlideFiles(armor))
        {
            var extension = Path.GetExtension(filePath);
            if (extension.Equals(".osp", StringComparison.OrdinalIgnoreCase))
            {
                hasOsp = true;
                var fromOsp = await TryReadOspAsync(filePath, cancellationToken);
                sliders.AddRange(fromOsp.Sliders);
                zapSliders.AddRange(fromOsp.ZapSliders);
            }
            else if (extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadBsdSlider(filePath, out var candidate))
                {
                    hasBsdPayloads |= candidate.ReusablePayload is not null;
                    if (candidate.IsZap)
                    {
                        zapSliders.Add(candidate);
                    }
                    else
                    {
                        sliders.Add(candidate);
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
                    if (!TryCreatePayloadCandidate(
                        sliderName,
                        morph.Deltas,
                        SourcePriority.TriPayloadBase,
                        IsHighWeightVariant(morph.Name),
                        "tri",
                        out var candidate))
                    {
                        continue;
                    }

                    hasTriPayloads |= candidate.ReusablePayload is not null;
                    if (candidate.IsZap)
                    {
                        zapSliders.Add(candidate);
                    }
                    else
                    {
                        sliders.Add(candidate);
                    }
                }
            }
        }

        return new BodySlideSourceSupport(
            CollapseCandidates(sliders),
            CollapseCandidates(zapSliders),
            hasOsp,
            hasTriPayloads,
            hasBsdPayloads);
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

        var discoveredFiles = EnumerateLikelyBodySlideRoots(sourceRoot, armor)
            .SelectMany(location => EnumerateBodySlideSupportFiles(location.Root, location.SearchOption, meshTokens));

        return explicitFiles
            .Concat(discoveredFiles)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<SearchLocation> EnumerateLikelyBodySlideRoots(string sourceRoot, ImportedArmor armor)
    {
        var roots = new Dictionary<string, SearchOption>(StringComparer.OrdinalIgnoreCase);

        static void AddRoot(IDictionary<string, SearchOption> map, string? root, SearchOption searchOption)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return;
            }

            if (map.TryGetValue(root, out var existing) && existing == SearchOption.AllDirectories)
            {
                return;
            }

            map[root] = searchOption;
        }

        if (Directory.Exists(sourceRoot))
        {
            var bodySlideRoot = Path.Combine(sourceRoot, "BodySlide");
            AddRoot(roots, bodySlideRoot, SearchOption.AllDirectories);

            var calienteRoot = Path.Combine(sourceRoot, "CalienteTools", "BodySlide");
            AddRoot(roots, calienteRoot, SearchOption.AllDirectories);
        }

        foreach (var meshFile in armor.MeshFiles)
        {
            var directory = Path.GetDirectoryName(meshFile);
            AddRoot(roots, directory, SearchOption.TopDirectoryOnly);
        }

        foreach (var bodyReferenceFile in armor.BodyReferenceFiles)
        {
            var directory = Path.GetDirectoryName(bodyReferenceFile);
            AddRoot(roots, directory, SearchOption.TopDirectoryOnly);
        }

        return roots.Select(static pair => new SearchLocation(pair.Key, pair.Value));
    }

    private static IEnumerable<string> EnumerateBodySlideSupportFiles(string root, SearchOption searchOption, IReadOnlyList<string> meshTokens)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            return EnumerateSupportedFiles(root)
                .Where(path => IsAssociatedWithArmor(path, meshTokens));
        }

        var discovered = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            discovered.AddRange(EnumerateSupportedFiles(current)
                .Where(path => IsAssociatedWithArmor(path, meshTokens)));

            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (ShouldTraverseBodySlideDirectory(root, directory, meshTokens))
                {
                    pending.Push(directory);
                }
            }
        }

        return discovered;
    }

    private static IEnumerable<string> EnumerateSupportedFiles(string root) =>
        Directory.EnumerateFiles(root, "*.osp", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(root, "*.bsd", SearchOption.TopDirectoryOnly))
            .Concat(Directory.EnumerateFiles(root, "*.tri", SearchOption.TopDirectoryOnly));

    private static bool ShouldTraverseBodySlideDirectory(string searchRoot, string directoryPath, IReadOnlyList<string> meshTokens)
    {
        if (IsAssociatedWithArmor(directoryPath, meshTokens))
        {
            return true;
        }

        if (string.Equals(directoryPath, searchRoot, PathComparison))
        {
            return true;
        }

        var directoryName = Path.GetFileName(directoryPath);
        return directoryName.Equals("BodySlide", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("CalienteTools", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("ShapeData", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("SliderSets", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("SliderGroups", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("Presets", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("Project", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("Projects", StringComparison.OrdinalIgnoreCase);
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
            var sliders = new List<SourceSliderCandidate>();
            var zapSliders = new List<SourceSliderCandidate>();

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
                    zapSliders.Add(new SourceSliderCandidate(name!, SourcePriority.OspZap));
                }
                else
                {
                    sliders.Add(new SourceSliderCandidate(name!, SourcePriority.OspSlider));
                }
            }

            return new BodySlideSourceSupport(
                CollapseCandidates(sliders),
                CollapseCandidates(zapSliders),
                HasOsp: true,
                HasTriPayloads: false,
                HasBsdPayloads: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return new BodySlideSourceSupport([], [], HasOsp: true, HasTriPayloads: false, HasBsdPayloads: false);
        }
    }

    private static string NormalizeMeshToken(string meshFilePath)
    {
        var token = Path.GetFileNameWithoutExtension(meshFilePath) ?? meshFilePath;
        return token.EndsWith("_0", StringComparison.OrdinalIgnoreCase) || token.EndsWith("_1", StringComparison.OrdinalIgnoreCase)
            ? token[..^2]
            : token;
    }

    private static IReadOnlyList<string> MergeSliderLists(IReadOnlyList<string> primary, IReadOnlyList<SourceSliderCandidate> secondary)
    {
        var merged = new List<string>(primary.Count + secondary.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slider in primary)
        {
            if (!IsLikelySliderName(slider) || !seen.Add(slider))
            {
                continue;
            }

            merged.Add(slider);
        }

        foreach (var slider in secondary.Select(static candidate => candidate.Name))
        {
            if (!IsLikelySliderName(slider) || !seen.Add(slider))
            {
                continue;
            }

            merged.Add(slider);
        }

        return merged;
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

    private static InferredSourceBodySupport? InferFallbackSourceBody(ImportedArmor armor, string targetBody)
    {
        var targetCanonicalBody = BuiltInBodyMetadataCatalog.TryResolveCanonicalName(targetBody, out var canonicalTargetBody)
            ? canonicalTargetBody
            : targetBody;
        var evidence = armor.MeshFiles
            .Concat(armor.TextureFiles)
            .Concat(armor.PhysicsFiles)
            .Concat(armor.BodyReferenceFiles)
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? path)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (evidence.Length == 0)
        {
            return null;
        }

        var best = default(InferredSourceBodySupport?);
        foreach (var body in BuiltInBodyMetadataCatalog.All)
        {
            if (body.Name.Equals(targetCanonicalBody, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var signals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var score = 0;
            score += ScoreTokens(body.ReferenceTokens, "reference", signals);
            score += ScoreTokens(body.DetectionTokens, "detection", signals);
            score += ScoreTokens(body.TextureTokens, "texture", signals);
            score += ScoreTokens(body.Aliases, "alias", signals);

            if (score < 4 || signals.Count == 0)
            {
                continue;
            }

            var candidate = new InferredSourceBodySupport(body.Name, score, signals.Order(StringComparer.OrdinalIgnoreCase).ToArray());
            if (best is null || candidate.Score > best.Score)
            {
                best = candidate;
            }
        }

        return best;

        int ScoreTokens(IEnumerable<string> tokens, string category, ISet<string> signals)
        {
            var score = 0;
            foreach (var token in tokens.Where(static token => !string.IsNullOrWhiteSpace(token)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var fileToken in evidence)
                {
                    if (!fileToken.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    score += category switch
                    {
                        "reference" => 5,
                        "alias" => 4,
                        "detection" => 3,
                        "texture" => 2,
                        _ => 1
                    };
                    signals.Add($"{category}:{token}");
                    break;
                }
            }

            return score;
        }
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

    private static bool TryReadBsdSlider(string filePath, out SourceSliderCandidate candidate)
    {
        if (BsdMorphReader.TryRead(filePath, out var payload) && payload is not null)
        {
            return TryCreatePayloadCandidate(
                NormalizeSliderFileName(payload.SliderName),
                payload.Deltas,
                payload.IsHighWeight ? SourcePriority.BsdHighWeightBase : SourcePriority.BsdLowWeightBase,
                payload.IsHighWeight,
                "bsd",
                out candidate);
        }

        var sliderName = NormalizeSliderFileName(Path.GetFileNameWithoutExtension(filePath));
        if (IsLikelyZapSliderName(sliderName) && !string.IsNullOrWhiteSpace(sliderName))
        {
            candidate = new SourceSliderCandidate(sliderName, SourcePriority.UnreadablePayloadZapFallback, IsZap: true);
            return true;
        }

        candidate = EmptyCandidate;
        return false;
    }

    private static bool TryCreatePayloadCandidate(
        string sliderName,
        IReadOnlyList<(float X, float Y, float Z)> deltas,
        int basePriority,
        bool isHighWeight,
        string payloadKind,
        out SourceSliderCandidate candidate)
    {
        var isZap = IsLikelyZapSliderName(sliderName);
        if (string.IsNullOrWhiteSpace(sliderName))
        {
            candidate = EmptyCandidate;
            return false;
        }

        var stats = MorphPayloadAnalysis.Analyze(deltas);
        if (!isZap && stats.MeaningfulCount == 0)
        {
            candidate = EmptyCandidate;
            return false;
        }

        candidate = new SourceSliderCandidate(
            sliderName,
            basePriority + ComputePayloadPriorityOffset(stats),
            isZap,
            stats,
            new SourceMorphPayload(sliderName, isHighWeight, payloadKind, deltas.Count, deltas));
        return true;
    }

    private static bool IsHighWeightVariant(string morphName) =>
        !string.IsNullOrWhiteSpace(morphName) &&
        morphName.Trim().EndsWith("_1", StringComparison.OrdinalIgnoreCase);

    private static int ComputePayloadPriorityOffset(MorphDeltaStats stats)
    {
        if (stats.TotalCount <= 0)
        {
            return 0;
        }

        var densityScore = (int)Math.Round(Math.Clamp(stats.MeaningfulRatio, 0f, 1f) * 100);
        var magnitudeScore = (int)Math.Round(Math.Clamp(stats.TotalMagnitude * 10f, 0f, 100f));
        var peakScore = (int)Math.Round(Math.Clamp(stats.MaxMagnitude * 80f, 0f, 80f));
        return densityScore + magnitudeScore + peakScore;
    }

    private static IReadOnlyList<SourceSliderCandidate> CollapseCandidates(IEnumerable<SourceSliderCandidate> candidates)
    {
        return candidates
            .Where(static candidate => IsLikelySliderName(candidate.Name))
            .GroupBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(static candidate => candidate.Priority)
                .ThenBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderByDescending(static candidate => candidate.Priority)
            .ThenBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static SourceMorphQualityMetrics? BuildSourceMorphQuality(IEnumerable<SourceSliderCandidate> sliderCandidates, IEnumerable<SourceSliderCandidate> zapCandidates)
    {
        var payloadStats = sliderCandidates
            .Concat(zapCandidates)
            .Select(static candidate => candidate.PayloadStats)
            .Where(static stats => stats is not null)
            .Select(static stats => stats!.Value)
            .ToArray();
        if (payloadStats.Length == 0)
        {
            return null;
        }

        var meaningfulPayloadMorphCount = payloadStats.Count(static stats => stats.MeaningfulCount > 0);
        var payloadStrengthScore = payloadStats
            .Select(ComputePayloadStrengthScore)
            .DefaultIfEmpty(0d)
            .Average();
        var payloadCoverageRatio = payloadStats.Length == 0
            ? 0d
            : (double)meaningfulPayloadMorphCount / payloadStats.Length;

        return new SourceMorphQualityMetrics(
            PayloadMorphCount: payloadStats.Length,
            MeaningfulPayloadMorphCount: meaningfulPayloadMorphCount,
            PayloadCoverageRatio: Math.Round(Math.Clamp(payloadCoverageRatio, 0d, 1d), 4),
            PayloadStrengthScore: Math.Round(Math.Clamp(payloadStrengthScore, 0d, 1d), 4));
    }

    private static double ComputePayloadStrengthScore(MorphDeltaStats stats)
    {
        var density = Math.Clamp(stats.MeaningfulRatio, 0f, 1f);
        var magnitude = Math.Clamp(stats.TotalMagnitude / 2f, 0f, 1f);
        var peak = Math.Clamp(stats.MaxMagnitude / 0.06f, 0f, 1f);
        return density * 0.5d + magnitude * 0.35d + peak * 0.15d;
    }

    private sealed record BodySlideSourceSupport(
        IReadOnlyList<SourceSliderCandidate> Sliders,
        IReadOnlyList<SourceSliderCandidate> ZapSliders,
        bool HasOsp,
        bool HasTriPayloads,
        bool HasBsdPayloads)
    {
        public SourceMorphQualityMetrics? SourceMorphQuality => BuildSourceMorphQuality(Sliders, ZapSliders);

        public IReadOnlyDictionary<string, SourceMorphPayloadVariants> ReusableMorphPayloads =>
            BuildReusableMorphPayloads(Sliders, ZapSliders);

        public SourceAssetSupportMetrics BuildAssetSupport(
            bool usedFallbackSliders,
            bool hasReferenceAssets,
            InferredSourceBodySupport? inferredSourceBody)
        {
            var missingAssets = new List<string>();
            if (!HasOsp)
            {
                missingAssets.Add("osp");
            }

            if (!HasTriPayloads && !HasBsdPayloads)
            {
                missingAssets.Add("morph-payloads");
            }

            if (!hasReferenceAssets)
            {
                missingAssets.Add("reference-assets");
            }

            return new SourceAssetSupportMetrics(
                HasOsp,
                HasTriPayloads,
                HasBsdPayloads,
                hasReferenceAssets,
                usedFallbackSliders,
                missingAssets,
                ReusableMorphPayloads.Count,
                inferredSourceBody?.BodyName,
                inferredSourceBody?.Signals);
        }
    }

    private static IReadOnlyDictionary<string, SourceMorphPayloadVariants> BuildReusableMorphPayloads(
        IEnumerable<SourceSliderCandidate> sliders,
        IEnumerable<SourceSliderCandidate> zapSliders)
    {
        return sliders
            .Concat(zapSliders)
            .Where(static candidate => candidate.ReusablePayload is not null)
            .GroupBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group =>
                {
                    var lowWeight = group
                        .Where(static candidate => candidate.ReusablePayload?.IsHighWeight is false)
                        .OrderByDescending(static candidate => candidate.Priority)
                        .Select(static candidate => candidate.ReusablePayload)
                        .FirstOrDefault();
                    var highWeight = group
                        .Where(static candidate => candidate.ReusablePayload?.IsHighWeight is true)
                        .OrderByDescending(static candidate => candidate.Priority)
                        .Select(static candidate => candidate.ReusablePayload)
                        .FirstOrDefault();

                    return new SourceMorphPayloadVariants(lowWeight, highWeight);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record SourceSliderCandidate(
        string Name,
        int Priority,
        bool IsZap = false,
        MorphDeltaStats? PayloadStats = null,
        SourceMorphPayload? ReusablePayload = null);
    private sealed record InferredSourceBodySupport(string BodyName, int Score, IReadOnlyList<string> Signals);
    private sealed record SearchLocation(string Root, SearchOption SearchOption);

    private static class SourcePriority
    {
        public const int OspSlider = 100;
        public const int OspZap = 100;
        public const int TriPayloadBase = 220;
        public const int BsdLowWeightBase = 260;
        public const int BsdHighWeightBase = 300;
        public const int UnreadablePayloadZapFallback = 40;
    }
}
