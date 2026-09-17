using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record RaceCompatibilityRace(string Name, uint FormId, IReadOnlyList<string> Groups);
internal sealed record RaceCompatibilityBodyRule(
    string Body,
    IReadOnlyList<string> CompatibleGroups,
    IReadOnlyList<string> WarningGroups,
    string? WarningMessage);

internal static class RaceCompatibilityCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.race-compatibility.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<RaceCompatibilityCatalogData> Data = new(Load);

    public static IReadOnlyCollection<RaceCompatibilityRace> Races => Data.Value.RacesByName.Values.ToArray();
    public static IReadOnlyCollection<RaceCompatibilityBodyRule> BodyRules => Data.Value.BodyRules.Values.ToArray();

    public static bool TryGetRace(uint formId, out RaceCompatibilityRace race) =>
        Data.Value.RacesByFormId.TryGetValue(formId & 0x00FFFFFFu, out race!);

    public static bool TryGetBodyRule(string bodyName, out RaceCompatibilityBodyRule rule)
    {
        rule = default!;
        if (string.IsNullOrWhiteSpace(bodyName))
        {
            return false;
        }

        var canonicalBody = BodyTypeCatalog.ResolveName(bodyName);
        return Data.Value.BodyRules.TryGetValue(canonicalBody, out rule!);
    }

    private static RaceCompatibilityCatalogData Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded race compatibility resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dto = JsonSerializer.Deserialize<RaceCompatibilityCatalogDto>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Race compatibility metadata could not be deserialized.");

        var races = (dto.Races ?? [])
            .Select(NormalizeRace)
            .ToArray();
        var bodyRules = (dto.BodyRules ?? [])
            .Select(NormalizeBodyRule)
            .ToDictionary(static rule => rule.Body, StringComparer.OrdinalIgnoreCase);

        return new RaceCompatibilityCatalogData(
            races.ToDictionary(static race => race.FormId),
            races.ToDictionary(static race => race.Name, StringComparer.OrdinalIgnoreCase),
            bodyRules);
    }

    private static RaceCompatibilityRace NormalizeRace(RaceCompatibilityRaceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new InvalidOperationException("Race compatibility metadata contained a race without a name.");
        }

        if (string.IsNullOrWhiteSpace(dto.FormId))
        {
            throw new InvalidOperationException($"Race compatibility metadata contained a missing FormID for '{dto.Name}'.");
        }

        return new RaceCompatibilityRace(
            dto.Name.Trim(),
            ParseFormId(dto.FormId),
            NormalizeStringList(dto.Groups));
    }

    private static RaceCompatibilityBodyRule NormalizeBodyRule(RaceCompatibilityBodyRuleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Body))
        {
            throw new InvalidOperationException("Race compatibility metadata contained a body rule without a body name.");
        }

        return new RaceCompatibilityBodyRule(
            BodyTypeCatalog.ResolveName(dto.Body.Trim()),
            NormalizeStringList(dto.CompatibleGroups),
            NormalizeStringList(dto.WarningGroups),
            string.IsNullOrWhiteSpace(dto.WarningMessage) ? null : dto.WarningMessage.Trim());
    }

    private static uint ParseFormId(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.Parse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return uint.Parse(trimmed, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<string> NormalizeStringList(IEnumerable<string>? values) =>
        values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private sealed record RaceCompatibilityCatalogData(
        IReadOnlyDictionary<uint, RaceCompatibilityRace> RacesByFormId,
        IReadOnlyDictionary<string, RaceCompatibilityRace> RacesByName,
        IReadOnlyDictionary<string, RaceCompatibilityBodyRule> BodyRules);

    private sealed class RaceCompatibilityCatalogDto
    {
        public RaceCompatibilityRaceDto[]? Races { get; init; }
        public RaceCompatibilityBodyRuleDto[]? BodyRules { get; init; }
    }

    private sealed class RaceCompatibilityRaceDto
    {
        public string? Name { get; init; }
        public string? FormId { get; init; }
        public string[]? Groups { get; init; }
    }

    private sealed class RaceCompatibilityBodyRuleDto
    {
        public string? Body { get; init; }
        public string[]? CompatibleGroups { get; init; }
        public string[]? WarningGroups { get; init; }
        public string? WarningMessage { get; init; }
    }
}
