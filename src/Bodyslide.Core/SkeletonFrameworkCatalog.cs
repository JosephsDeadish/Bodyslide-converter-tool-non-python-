using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record SkeletonFrameworkMetadata(
    string Label,
    IReadOnlyList<string> BonePrefixes,
    IReadOnlyList<string> BoneTokens,
    IReadOnlyList<string> DistinctiveSignatures,
    int MinimumSignatureMatches);

internal static class SkeletonFrameworkCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.skeleton-frameworks.json";

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

    public static string? DetectFramework(IReadOnlyList<string> boneNames)
    {
        if (boneNames.Count == 0)
        {
            return null;
        }

        var normalizedBoneNames = boneNames
            .Where(static bone => !string.IsNullOrWhiteSpace(bone))
            .Select(static bone => bone.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedBoneNames.Length == 0)
        {
            return null;
        }

        var condensedBoneNames = normalizedBoneNames
            .Select(NormalizeForMatching)
            .Where(static bone => !string.IsNullOrWhiteSpace(bone))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (condensedBoneNames.Length == 0)
        {
            return null;
        }

        string? bestFramework = null;
        var bestFallbackScore = 0d;
        var observedSemanticKeys = ExtractSemanticKeys(normalizedBoneNames);
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
                return framework.Label;
            }

            var fallbackScore = ComputeFallbackScore(framework, condensedBoneNames, observedSemanticKeys);
            if (fallbackScore > bestFallbackScore)
            {
                bestFallbackScore = fallbackScore;
                bestFramework = framework.Label;
            }
        }

        return bestFallbackScore >= 2d ? bestFramework : null;
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
        IReadOnlySet<string> observedSemanticKeys)
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
        if (prefixes.Length == 0 && tokens.Length == 0)
        {
            return 0d;
        }

        var prefixMatches = prefixes.Count(prefix =>
            condensedBoneNames.Any(bone => bone.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        var tokenMatches = tokens.Count(token =>
            condensedBoneNames.Any(bone => bone.Contains(token, StringComparison.OrdinalIgnoreCase)));

        if (prefixMatches == 0 && tokenMatches < framework.MinimumSignatureMatches)
        {
            var frameworkSemanticKeys = ExtractSemanticKeys(framework.BonePrefixes.Concat(framework.BoneTokens).Concat(framework.DistinctiveSignatures));
            if (observedSemanticKeys.Count == 0 || frameworkSemanticKeys.Count == 0)
            {
                return 0d;
            }

            var semanticMatches = observedSemanticKeys.Intersect(frameworkSemanticKeys, StringComparer.OrdinalIgnoreCase).Count();
            return semanticMatches >= framework.MinimumSignatureMatches
                ? semanticMatches * 0.9d
                : 0d;
        }

        var semanticKeys = ExtractSemanticKeys(framework.BonePrefixes.Concat(framework.BoneTokens).Concat(framework.DistinctiveSignatures));
        var semanticOverlap = semanticKeys.Count == 0 || observedSemanticKeys.Count == 0
            ? 0
            : observedSemanticKeys.Intersect(semanticKeys, StringComparer.OrdinalIgnoreCase).Count();

        return prefixMatches + (tokenMatches * 0.75d) + (semanticOverlap * 0.65d);
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

    private sealed class SkeletonFrameworkMetadataDto
    {
        public string? Label { get; init; }
        public string[]? BonePrefixes { get; init; }
        public string[]? BoneTokens { get; init; }
        public string[]? DistinctiveSignatures { get; init; }
        public int MinimumSignatureMatches { get; init; }
    }
}
