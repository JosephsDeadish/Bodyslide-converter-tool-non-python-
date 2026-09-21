using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record SemanticAnchorProfile(
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Anchors);

internal static class SemanticAnchorCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.semantic-anchor-profiles.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<IReadOnlyDictionary<string, SemanticAnchorProfile>> Profiles = new(LoadProfiles);
    private static readonly Lazy<IReadOnlyDictionary<string, string>> AliasMap = new(LoadAliasMap);

    public static bool TryGet(string? bodyName, out SemanticAnchorProfile profile)
    {
        profile = default!;
        if (string.IsNullOrWhiteSpace(bodyName))
        {
            return false;
        }

        if (Profiles.Value.TryGetValue(bodyName.Trim(), out profile!))
        {
            return true;
        }

        return AliasMap.Value.TryGetValue(bodyName.Trim(), out var canonicalName) &&
               Profiles.Value.TryGetValue(canonicalName, out profile!);
    }

    private static IReadOnlyDictionary<string, SemanticAnchorProfile> LoadProfiles()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded semantic anchor resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dtos = JsonSerializer.Deserialize<List<SemanticAnchorProfileDto>>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Semantic anchor profiles could not be deserialized.");

        return dtos
            .Select(Normalize)
            .ToDictionary(static profile => profile.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> LoadAliasMap()
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in Profiles.Value.Values)
        {
            aliases[profile.Name] = profile.Name;
            foreach (var alias in profile.Aliases)
            {
                aliases[alias] = profile.Name;
            }
        }

        return aliases;
    }

    private static SemanticAnchorProfile Normalize(SemanticAnchorProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new InvalidOperationException("Semantic anchor profile contained an entry without a name.");
        }

        var anchors = (dto.Anchors ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase))
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                static pair => pair.Key.Trim(),
                static pair => (IReadOnlyList<string>)pair.Value
                    .Where(static token => !string.IsNullOrWhiteSpace(token))
                    .Select(static token => token.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new SemanticAnchorProfile(
            dto.Name.Trim(),
            dto.Aliases?
                .Where(static alias => !string.IsNullOrWhiteSpace(alias))
                .Select(static alias => alias.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [],
            anchors);
    }

    private sealed class SemanticAnchorProfileDto
    {
        public string? Name { get; init; }
        public string[]? Aliases { get; init; }
        public Dictionary<string, string[]>? Anchors { get; init; }
    }
}
