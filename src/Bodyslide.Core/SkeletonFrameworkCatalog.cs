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
                normalizedBoneNames.Any(bone => bone.Contains(signature, StringComparison.OrdinalIgnoreCase) ||
                                               bone.Equals(signature, StringComparison.OrdinalIgnoreCase)));
            if (matches >= Math.Min(Math.Max(framework.MinimumSignatureMatches, 1), signatures.Length))
            {
                return framework.Label;
            }
        }

        return null;
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

    private sealed class SkeletonFrameworkMetadataDto
    {
        public string? Label { get; init; }
        public string[]? BonePrefixes { get; init; }
        public string[]? BoneTokens { get; init; }
        public string[]? DistinctiveSignatures { get; init; }
        public int MinimumSignatureMatches { get; init; }
    }
}
