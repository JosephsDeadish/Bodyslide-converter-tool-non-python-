using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record SkeletonFrameworkMetadata(
    string Label,
    IReadOnlyList<string> BonePrefixes,
    IReadOnlyList<string> BoneTokens,
    IReadOnlyList<string> DistinctiveSignatures,
    IReadOnlyList<string> EcosystemCues,
    int MinimumSignatureMatches);
internal sealed record SkeletonFrameworkDetectionResult(
    string? Label,
    double Confidence,
    IReadOnlyList<string> Evidence,
    bool UsedSparseInference);

internal static class SkeletonFrameworkCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.skeleton-frameworks.json";
    private static readonly string[] ChainDepthMarkers =
    [
        "root",
        "base",
        "mid",
        "tip",
        "lower",
        "upper",
        "segment",
        "branch",
        "finger",
        "tail",
        "tongue",
        "throat",
        "jaw",
        "mane",
        "frill",
        "fin",
        "whisker",
        "antenna",
        "mandible",
        "wing"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<IReadOnlyList<SkeletonFrameworkMetadata>> Frameworks = new(LoadFrameworks);

    public static IReadOnlyList<SkeletonFrameworkMetadata> All => Frameworks.Value;

    public static bool MatchesKnownBonePattern(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var framework in All)
        {
            if (framework.BonePrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
                framework.BoneTokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public static string? DetectFramework(IReadOnlyList<string> boneNames, IReadOnlyList<string>? contextCues = null)
        => DetectFrameworkDetails(boneNames, contextCues).Label;

    public static SkeletonFrameworkDetectionResult DetectFrameworkDetails(IReadOnlyList<string> boneNames, IReadOnlyList<string>? contextCues = null)
        => RankFrameworkDetections(boneNames, contextCues, maxCandidates: 1).FirstOrDefault()
           ?? new SkeletonFrameworkDetectionResult(null, 0d, [], false);

    public static IReadOnlyList<SkeletonFrameworkDetectionResult> RankFrameworkDetections(
        IReadOnlyList<string> boneNames,
        IReadOnlyList<string>? contextCues = null,
        int maxCandidates = 3)
    {
        if (boneNames.Count == 0)
        {
            return [];
        }

        var normalizedBoneNames = boneNames
            .Where(static bone => !string.IsNullOrWhiteSpace(bone))
            .Select(static bone => bone.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedBoneNames.Length == 0)
        {
            return [];
        }

        var condensedBoneNames = normalizedBoneNames
            .Select(NormalizeForMatching)
            .Where(static bone => !string.IsNullOrWhiteSpace(bone))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (condensedBoneNames.Length == 0)
        {
            return [];
        }

        var condensedContextCues = (contextCues ?? [])
            .Where(static cue => !string.IsNullOrWhiteSpace(cue))
            .Select(static cue => cue.Trim())
            .Select(NormalizeForMatching)
            .Where(static cue => !string.IsNullOrWhiteSpace(cue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var detections = new List<(double Score, SkeletonFrameworkDetectionResult Detection)>();
        var observedSemanticKeys = ExtractSemanticKeys(normalizedBoneNames);
        var observedPhysicsGroups = PhysicsRepairCatalog.DetectGroups(normalizedBoneNames);
        foreach (var framework in All)
        {
            var signatures = framework.DistinctiveSignatures
                .Where(static signature => !string.IsNullOrWhiteSpace(signature))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (signatures.Length == 0)
            {
                continue;
            }

            var matches = signatures.Count(signature =>
                ContainsNormalized(condensedBoneNames, signature));
            if (matches >= Math.Min(Math.Max(framework.MinimumSignatureMatches, 1), signatures.Length))
            {
                var matchedSignatures = signatures
                    .Where(signature => ContainsNormalized(condensedBoneNames, signature))
                    .Take(6)
                    .Select(signature => $"signature:{signature}")
                    .ToArray();
                var confidence = Math.Min(1d, 0.7d + (matches / (double)Math.Max(signatures.Length, 1)));
                detections.Add((100d + matches, new SkeletonFrameworkDetectionResult(
                    framework.Label,
                    Math.Round(confidence, 2, MidpointRounding.AwayFromZero),
                    matchedSignatures,
                    UsedSparseInference: false)));
                continue;
            }

            var fallbackEvidence = new List<string>();
            var fallbackScore = ComputeFallbackScore(framework, condensedBoneNames, condensedContextCues, observedSemanticKeys, observedPhysicsGroups, fallbackEvidence);
            if (fallbackScore >= 2d)
            {
                var normalizedScore = Math.Min(0.89d, 0.25d + (fallbackScore / 5d));
                detections.Add((fallbackScore, new SkeletonFrameworkDetectionResult(
                    framework.Label,
                    Math.Round(normalizedScore, 2, MidpointRounding.AwayFromZero),
                    fallbackEvidence,
                    UsedSparseInference: true)));
            }
        }

        return detections
            .OrderBy(static entry => entry.Detection.UsedSparseInference)
            .ThenByDescending(static entry => entry.Detection.Confidence)
            .ThenByDescending(static entry => entry.Score)
            .Select(static entry => entry.Detection)
            .DistinctBy(static detection => detection.Label, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, maxCandidates))
            .ToArray();
    }

    private static IReadOnlyList<SkeletonFrameworkMetadata> LoadFrameworks()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded skeleton framework resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dtos = JsonSerializer.Deserialize<List<SkeletonFrameworkMetadataDto>>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Skeleton framework metadata could not be deserialized.");

        return dtos
            .Select(Normalize)
            .Where(static framework => !string.IsNullOrWhiteSpace(framework.Label))
            .ToArray();
    }

    private static SkeletonFrameworkMetadata Normalize(SkeletonFrameworkMetadataDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Label))
        {
            throw new InvalidOperationException("Skeleton framework metadata contained an entry without a label.");
        }

        return new SkeletonFrameworkMetadata(
            dto.Label.Trim(),
            NormalizeStringList(dto.BonePrefixes),
            NormalizeStringList(dto.BoneTokens),
            NormalizeStringList(dto.DistinctiveSignatures),
            NormalizeStringList(dto.EcosystemCues),
            Math.Max(1, dto.MinimumSignatureMatches));
    }

    private static IReadOnlyList<string> NormalizeStringList(IEnumerable<string>? values) =>
        values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private static double ComputeFallbackScore(
        SkeletonFrameworkMetadata framework,
        IReadOnlyList<string> condensedBoneNames,
        IReadOnlyList<string> condensedContextCues,
        IReadOnlySet<string> observedSemanticKeys,
        IReadOnlySet<string> observedPhysicsGroups,
        List<string> evidence)
    {
        var prefixes = framework.BonePrefixes
            .Select(NormalizeForMatching)
            .Where(static prefix => !string.IsNullOrWhiteSpace(prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var tokens = framework.BoneTokens
            .Select(NormalizeForMatching)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var ecosystemCues = framework.EcosystemCues
            .Select(NormalizeForMatching)
            .Where(static cue => !string.IsNullOrWhiteSpace(cue))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (prefixes.Length == 0 && tokens.Length == 0)
        {
            return 0d;
        }

        var prefixMatches = prefixes.Count(prefix =>
            condensedBoneNames.Any(bone => bone.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        var tokenMatches = tokens.Count(token =>
            condensedBoneNames.Any(bone => bone.Contains(token, StringComparison.OrdinalIgnoreCase)));
        if (prefixMatches > 0)
        {
            evidence.Add($"prefix-matches:{prefixMatches}");
        }
        if (tokenMatches > 0)
        {
            evidence.Add($"token-matches:{tokenMatches}");
        }
        var cueMatches = ecosystemCues.Count(cue =>
            condensedBoneNames.Any(bone => bone.Contains(cue, StringComparison.OrdinalIgnoreCase)));
        if (cueMatches > 0)
        {
            evidence.Add($"ecosystem-cues:{cueMatches}");
        }
        var contextCueMatches = ecosystemCues.Count(cue =>
            condensedContextCues.Any(context => context.Contains(cue, StringComparison.OrdinalIgnoreCase) ||
                                                cue.Contains(context, StringComparison.OrdinalIgnoreCase)));
        if (contextCueMatches > 0)
        {
            evidence.Add($"context-cues:{contextCueMatches}");
        }
        var frameworkChainMarkers = ExtractChainDepthMarkers(framework.BonePrefixes.Concat(framework.BoneTokens).Concat(framework.DistinctiveSignatures));
        var observedChainMarkers = ExtractChainDepthMarkers(condensedBoneNames);

        if (prefixMatches == 0 && tokenMatches < framework.MinimumSignatureMatches)
        {
            var frameworkSemanticKeys = ExtractSemanticKeys(framework.BonePrefixes.Concat(framework.BoneTokens).Concat(framework.DistinctiveSignatures));
            var sparseFrameworkPhysicsGroups = ExtractPhysicsGroups(framework.BonePrefixes.Concat(framework.BoneTokens).Concat(framework.DistinctiveSignatures));
            if (observedSemanticKeys.Count == 0 || frameworkSemanticKeys.Count == 0)
            {
                if ((observedPhysicsGroups.Count == 0 || sparseFrameworkPhysicsGroups.Count == 0) &&
                    cueMatches + contextCueMatches < framework.MinimumSignatureMatches)
                {
                    return 0d;
                }
            }

            var semanticMatches = observedSemanticKeys.Intersect(frameworkSemanticKeys, StringComparer.OrdinalIgnoreCase).Count();
            if (semanticMatches > 0)
            {
                evidence.Add($"semantic-overlap:{semanticMatches}");
            }
            var physicsMatches = observedPhysicsGroups.Count == 0 || sparseFrameworkPhysicsGroups.Count == 0
                ? 0
                : observedPhysicsGroups.Intersect(sparseFrameworkPhysicsGroups, StringComparer.OrdinalIgnoreCase).Count();
            if (physicsMatches > 0)
            {
                evidence.Add($"group-overlap:{physicsMatches}");
            }
            var chainDepthMatches = frameworkChainMarkers.Count == 0 || observedChainMarkers.Count == 0
                ? 0
                : observedChainMarkers.Intersect(frameworkChainMarkers, StringComparer.OrdinalIgnoreCase).Count();
            if (chainDepthMatches > 0 &&
                (semanticMatches > 0 || physicsMatches > 0 || cueMatches + contextCueMatches > 0))
            {
                evidence.Add($"chain-depth:{chainDepthMatches}");
            }

            var semanticScore = semanticMatches >= framework.MinimumSignatureMatches
                ? semanticMatches * 1.05d
                : 0d;
            var physicsScore = physicsMatches >= framework.MinimumSignatureMatches
                ? physicsMatches * 1d
                : 0d;
            var cueScore = cueMatches + contextCueMatches >= framework.MinimumSignatureMatches
                ? (cueMatches + contextCueMatches) * 1.10d
                : 0d;
            var chainScore = chainDepthMatches >= framework.MinimumSignatureMatches &&
                             (semanticMatches > 0 || physicsMatches > 0 || cueMatches + contextCueMatches > 0)
                ? chainDepthMatches * 1.15d
                : 0d;
            return Math.Max(Math.Max(semanticScore, physicsScore), Math.Max(cueScore, chainScore));
        }

        var semanticKeys = ExtractSemanticKeys(framework.BonePrefixes.Concat(framework.BoneTokens).Concat(framework.DistinctiveSignatures));
        var frameworkPhysicsGroups = ExtractPhysicsGroups(framework.BonePrefixes.Concat(framework.BoneTokens).Concat(framework.DistinctiveSignatures));
        var semanticOverlap = semanticKeys.Count == 0 || observedSemanticKeys.Count == 0
            ? 0
            : observedSemanticKeys.Intersect(semanticKeys, StringComparer.OrdinalIgnoreCase).Count();
        if (semanticOverlap > 0)
        {
            evidence.Add($"semantic-overlap:{semanticOverlap}");
        }

        var physicsOverlap = frameworkPhysicsGroups.Count == 0 || observedPhysicsGroups.Count == 0
            ? 0
            : observedPhysicsGroups.Intersect(frameworkPhysicsGroups, StringComparer.OrdinalIgnoreCase).Count();
        if (physicsOverlap > 0)
        {
            evidence.Add($"group-overlap:{physicsOverlap}");
        }
        var chainDepthOverlap = frameworkChainMarkers.Count == 0 || observedChainMarkers.Count == 0
            ? 0
            : observedChainMarkers.Intersect(frameworkChainMarkers, StringComparer.OrdinalIgnoreCase).Count();
        if (chainDepthOverlap > 0 &&
            (semanticOverlap > 0 || physicsOverlap > 0 || cueMatches + contextCueMatches > 0))
        {
            evidence.Add($"chain-depth:{chainDepthOverlap}");
        }

        return prefixMatches + (tokenMatches * 0.75d) + ((cueMatches + contextCueMatches) * 0.90d) + (semanticOverlap * 0.65d) + (physicsOverlap * 0.70d) + (chainDepthOverlap * 0.60d);
    }

    private static bool ContainsNormalized(IReadOnlyList<string> condensedBoneNames, string value)
    {
        var normalized = NormalizeForMatching(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return condensedBoneNames.Any(bone => bone.Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeForMatching(string value)
    {
        const int MaxStackLength = 256;
        Span<char> buffer = value.Length <= MaxStackLength
            ? stackalloc char[value.Length]
            : new char[value.Length];
        var length = 0;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[length++] = char.ToLowerInvariant(ch);
            }
        }

        return new string(buffer[..length]);
    }

    private static IReadOnlySet<string> ExtractSemanticKeys(IEnumerable<string> values)
    {
        var semanticKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.Trim();
            foreach (var (key, aliases) in SemanticBoneAliasCatalog.All)
            {
                if (aliases.Any(alias => normalized.Contains(alias, StringComparison.OrdinalIgnoreCase)) ||
                    normalized.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    semanticKeys.Add(key);
                }
            }
        }

        return semanticKeys;
    }

    private static IReadOnlySet<string> ExtractPhysicsGroups(IEnumerable<string> values)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (PhysicsRepairCatalog.TryMatchGroup(value, out var groupName))
            {
                groups.Add(groupName);
            }
        }

        return groups;
    }

    private static IReadOnlySet<string> ExtractChainDepthMarkers(IEnumerable<string> values)
    {
        var markers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var normalized = NormalizeForMatching(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            foreach (var marker in ChainDepthMarkers)
            {
                if (normalized.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    markers.Add(marker);
                }
            }
        }

        return markers;
    }

    private sealed class SkeletonFrameworkMetadataDto
    {
        public string? Label { get; init; }
        public string[]? BonePrefixes { get; init; }
        public string[]? BoneTokens { get; init; }
        public string[]? DistinctiveSignatures { get; init; }
        public string[]? EcosystemCues { get; init; }
        public int MinimumSignatureMatches { get; init; }
    }
}
