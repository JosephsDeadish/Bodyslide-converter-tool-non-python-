using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record SkeletonMappingFramework(
    string Id,
    IReadOnlyList<string> Bones,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FallbackMappings);

internal static class SkeletonMappingCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.skeleton-mapping.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<SkeletonMappingCatalogData> Data = new(Load);

    public static IReadOnlyList<string> CommonBones => Data.Value.CommonBones;

    public static IReadOnlyList<string> GetFrameworkBones(string? frameworkId)
    {
        if (string.IsNullOrWhiteSpace(frameworkId))
        {
            return [];
        }

        return Data.Value.Frameworks.TryGetValue(frameworkId.Trim(), out var framework)
            ? framework.Bones
            : [];
    }

    public static bool IsBoneSupportedByFramework(string? frameworkId, string boneName)
    {
        if (string.IsNullOrWhiteSpace(boneName))
        {
            return false;
        }

        if (Data.Value.CommonBoneIndex.Contains(boneName.Trim()))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(frameworkId) &&
               Data.Value.Frameworks.TryGetValue(frameworkId.Trim(), out var framework) &&
               framework.Bones.Contains(boneName.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryResolveSupportedBone(
        string sourceBone,
        string? frameworkId,
        out string resolvedBone)
    {
        resolvedBone = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceBone))
        {
            return false;
        }

        var trimmedSourceBone = sourceBone.Trim();
        if (IsBoneSupportedByFramework(frameworkId, trimmedSourceBone))
        {
            resolvedBone = trimmedSourceBone;
            return true;
        }

        if (!TryGetFallbackCandidates(trimmedSourceBone, frameworkId, out var fallbackCandidates))
        {
            return false;
        }

        foreach (var candidate in fallbackCandidates)
        {
            if (!IsBoneSupportedByFramework(frameworkId, candidate))
            {
                continue;
            }

            resolvedBone = candidate;
            return true;
        }

        return false;
    }

    public static bool ContainsFrameworkBone(string boneName)
    {
        var normalizedBoneName = boneName?.Trim();
        return !string.IsNullOrWhiteSpace(normalizedBoneName) &&
               Data.Value.FrameworkBoneIndex.Contains(normalizedBoneName);
    }

    public static bool TryGetFallbackCandidates(
        string sourceBone,
        string? frameworkId,
        out IReadOnlyList<string> fallbackCandidates)
    {
        fallbackCandidates = [];
        var normalizedSourceBone = sourceBone?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSourceBone))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(frameworkId) &&
            Data.Value.Frameworks.TryGetValue(frameworkId.Trim(), out var framework) &&
            framework.FallbackMappings.TryGetValue(normalizedSourceBone, out var frameworkCandidates))
        {
            fallbackCandidates = frameworkCandidates;
            return true;
        }

        if (Data.Value.FallbackMappings.TryGetValue(normalizedSourceBone, out var candidates))
        {
            fallbackCandidates = candidates;
            return true;
        }

        return false;
    }

    private static SkeletonMappingCatalogData Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded skeleton mapping resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dto = JsonSerializer.Deserialize<SkeletonMappingCatalogDto>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Skeleton mapping metadata could not be deserialized.");

        var commonBones = NormalizeStringList(dto.CommonBones);
        var fallbackMappings = NormalizeFallbackMappings(dto.FallbackMappings);
        var frameworks = (dto.Frameworks ?? [])
            .Select(NormalizeFramework)
            .Where(static framework => !string.IsNullOrWhiteSpace(framework.Id))
            .ToDictionary(static framework => framework.Id, StringComparer.OrdinalIgnoreCase);
        var commonBoneIndex = commonBones.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var frameworkBoneIndex = frameworks.Values
            .SelectMany(static framework => framework.Bones)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SkeletonMappingCatalogData(commonBones, commonBoneIndex, fallbackMappings, frameworks, frameworkBoneIndex);
    }

    private static SkeletonMappingFramework NormalizeFramework(SkeletonMappingFrameworkDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw new InvalidOperationException("Skeleton mapping framework metadata contained an entry without an id.");
        }

        return new SkeletonMappingFramework(
            dto.Id.Trim(),
            NormalizeStringList(dto.Bones),
            NormalizeFallbackMappings(dto.FallbackMappings));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizeFallbackMappings(
        Dictionary<string, string[]>? rawMappings)
    {
        var normalized = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (rawMappings is null || rawMappings.Count == 0)
        {
            return normalized;
        }

        foreach (var (sourceBone, fallbacks) in rawMappings)
        {
            if (string.IsNullOrWhiteSpace(sourceBone))
            {
                continue;
            }

            var normalizedFallbacks = NormalizeStringList(fallbacks);
            if (normalizedFallbacks.Count == 0)
            {
                continue;
            }

            normalized[sourceBone.Trim()] = normalizedFallbacks;
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeStringList(IEnumerable<string>? values) =>
        values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private sealed record SkeletonMappingCatalogData(
        IReadOnlyList<string> CommonBones,
        IReadOnlySet<string> CommonBoneIndex,
        IReadOnlyDictionary<string, IReadOnlyList<string>> FallbackMappings,
        IReadOnlyDictionary<string, SkeletonMappingFramework> Frameworks,
        IReadOnlySet<string> FrameworkBoneIndex);

    private sealed class SkeletonMappingCatalogDto
    {
        public string[]? CommonBones { get; init; }
        public Dictionary<string, string[]>? FallbackMappings { get; init; }
        public SkeletonMappingFrameworkDto[]? Frameworks { get; init; }
    }

    private sealed class SkeletonMappingFrameworkDto
    {
        public string? Id { get; init; }
        public string[]? Bones { get; init; }
        public Dictionary<string, string[]>? FallbackMappings { get; init; }
    }
}
