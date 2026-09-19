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
internal sealed record RaceCompatibilityInferenceRule(
    string Name,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> EditorIdHints,
    IReadOnlyList<string> MeshPathHints);

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
        Data.Value.RacesByFormId.TryGetValue(formId, out race!)
        || Data.Value.RacesByFormId.TryGetValue(formId & 0x00FFFFFFu, out race!);

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

    public static bool TryInferRaceFromContext(
        string? editorId,
        IReadOnlyList<string>? meshPaths,
        out RaceCompatibilityRace race)
    {
        race = default!;
        var normalizedEditorId = NormalizeHintSource(editorId);
        var normalizedMeshPaths = meshPaths?
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizeHintSource)
            .ToArray() ?? [];

        if (string.IsNullOrWhiteSpace(normalizedEditorId) && normalizedMeshPaths.Length == 0)
        {
            return false;
        }

        RaceCompatibilityInferenceRule? bestRule = null;
        var bestScore = 0;
        foreach (var rule in Data.Value.InferenceRules)
        {
            var score = ScoreInferenceRule(rule, normalizedEditorId, normalizedMeshPaths);
            if (score > bestScore)
            {
                bestRule = rule;
                bestScore = score;
            }
            else if (score > 0 && score == bestScore)
            {
                bestRule = null;
            }
        }

        if (bestRule is null || bestScore <= 0)
        {
            return false;
        }

        race = new RaceCompatibilityRace(bestRule.Name, 0u, bestRule.Groups);
        return true;
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
        var inferenceRules = (dto.InferenceRules ?? [])
            .Select(NormalizeInferenceRule)
            .ToArray();

        return new RaceCompatibilityCatalogData(
            races.ToDictionary(static race => race.FormId),
            races.ToDictionary(static race => race.Name, StringComparer.OrdinalIgnoreCase),
            bodyRules,
            inferenceRules);
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

    private static RaceCompatibilityInferenceRule NormalizeInferenceRule(RaceCompatibilityInferenceRuleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new InvalidOperationException("Race compatibility metadata contained an inference rule without a name.");
        }

        return new RaceCompatibilityInferenceRule(
            dto.Name.Trim(),
            NormalizeStringList(dto.Groups),
            NormalizeStringList(dto.EditorIdHints),
            NormalizeStringList(dto.MeshPathHints));
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

    private static int ScoreInferenceRule(
        RaceCompatibilityInferenceRule rule,
        string? normalizedEditorId,
        IReadOnlyList<string> normalizedMeshPaths)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(normalizedEditorId))
        {
            score += rule.EditorIdHints
                .Where(hint => normalizedEditorId.Contains(hint, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() * 4;
        }

        foreach (var meshPath in normalizedMeshPaths)
        {
            score += rule.MeshPathHints
                .Where(hint => meshPath.Contains(hint, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        return score;
    }

    private static string NormalizeHintSource(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace('\\', '/');

    private sealed record RaceCompatibilityCatalogData(
        IReadOnlyDictionary<uint, RaceCompatibilityRace> RacesByFormId,
        IReadOnlyDictionary<string, RaceCompatibilityRace> RacesByName,
        IReadOnlyDictionary<string, RaceCompatibilityBodyRule> BodyRules,
        IReadOnlyList<RaceCompatibilityInferenceRule> InferenceRules);

    private sealed class RaceCompatibilityCatalogDto
    {
        public RaceCompatibilityRaceDto[]? Races { get; init; }
        public RaceCompatibilityBodyRuleDto[]? BodyRules { get; init; }
        public RaceCompatibilityInferenceRuleDto[]? InferenceRules { get; init; }
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

    private sealed class RaceCompatibilityInferenceRuleDto
    {
        public string? Name { get; init; }
        public string[]? Groups { get; init; }
        public string[]? EditorIdHints { get; init; }
        public string[]? MeshPathHints { get; init; }
    }
}
