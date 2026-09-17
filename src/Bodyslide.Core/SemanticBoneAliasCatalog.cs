using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal static class SemanticBoneAliasCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.semantic-bone-aliases.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> Aliases = new(LoadAliases);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> All => Aliases.Value;

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> LoadAliases()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded semantic alias resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var data = JsonSerializer.Deserialize<Dictionary<string, string[]>>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Semantic bone alias metadata could not be deserialized.");

        return data.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }
}
