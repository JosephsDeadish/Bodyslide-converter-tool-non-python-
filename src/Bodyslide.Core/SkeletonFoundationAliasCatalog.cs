using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal static class SkeletonFoundationAliasCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.skeleton-foundation-aliases.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<IReadOnlyDictionary<string, string>> AliasMap = new(Load);

    public static bool TryResolve(string? skeletonFoundation, out string canonicalLabel)
    {
        canonicalLabel = string.Empty;
        if (string.IsNullOrWhiteSpace(skeletonFoundation))
        {
            return false;
        }

        var normalized = skeletonFoundation.Trim();
        if (AliasMap.Value.TryGetValue(normalized, out canonicalLabel!))
        {
            return true;
        }

        return AliasMap.Value.TryGetValue(SkeletonTextNormalization.Slugify(normalized), out canonicalLabel!);
    }

    private static IReadOnlyDictionary<string, string> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded skeleton foundation alias resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dtos = JsonSerializer.Deserialize<List<SkeletonFoundationAliasDto>>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Skeleton foundation aliases could not be deserialized.");

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in dtos)
        {
            if (string.IsNullOrWhiteSpace(entry.Canonical))
            {
                throw new InvalidOperationException("Skeleton foundation alias metadata contained an entry without a canonical label.");
            }

            var canonical = entry.Canonical.Trim();
            AddAlias(map, canonical, canonical);
            AddAlias(map, SkeletonTextNormalization.Slugify(canonical), canonical);
            foreach (var alias in entry.Aliases ?? [])
            {
                AddAlias(map, alias, canonical);
                AddAlias(map, SkeletonTextNormalization.Slugify(alias), canonical);
            }
        }

        return map;
    }

    private static void AddAlias(IDictionary<string, string> map, string? alias, string canonical)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        map[alias.Trim()] = canonical;
    }

    private sealed class SkeletonFoundationAliasDto
    {
        public string? Canonical { get; init; }
        public string[]? Aliases { get; init; }
    }
}
