using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bodyslide.Core;

public sealed record DesktopSmokeTestSummary(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("presets")] int Presets,
    [property: JsonPropertyName("targets")] int Targets,
    [property: JsonPropertyName("profiles")] int Profiles,
    [property: JsonPropertyName("physics")] int Physics,
    [property: JsonPropertyName("tabs")] int Tabs,
    [property: JsonPropertyName("expectedTabs")] int ExpectedTabs,
    [property: JsonPropertyName("tabTitles")] IReadOnlyList<string> TabTitles,
    [property: JsonPropertyName("expectedTabTitles")] IReadOnlyList<string> ExpectedTabTitles);

public static class DesktopSmokeTestContract
{
    public const string ReadyStatus = "ok";
    public const string LayoutMismatchStatus = "layout-mismatch";
    public const string LogTabTitle = "Log";
    public const string PreviewTabTitle = "Preview";
    public const string InspectTabTitle = "Inspect";
    public const string SummaryTabTitle = "Summary";
    public const string NextActionsTabTitle = "Next actions";
    public const string ReportsTabTitle = "Reports";
    public const string CatalogTabTitle = "Catalog";
    public const string ReadinessTabTitle = "Readiness";
    public const string FilesTabTitle = "Files";
    public const string CacheTabTitle = "Cache";

    public static IReadOnlyList<string> ExpectedDesktopTabTitles { get; } =
    [
        LogTabTitle,
        PreviewTabTitle,
        InspectTabTitle,
        SummaryTabTitle,
        NextActionsTabTitle,
        ReportsTabTitle,
        CatalogTabTitle,
        ReadinessTabTitle,
        FilesTabTitle,
        CacheTabTitle
    ];

    public static int ExpectedDesktopTabCount => ExpectedDesktopTabTitles.Count;
    public static int ExpectedPresetCount => PresetCatalog.All.Count;
    public static int ExpectedTargetCount => BodyTypeCatalog.All.Count;
    public static int ExpectedProfileCount => DeformationProfileModifier.All.Count;
    public static int ExpectedPhysicsCount => PhysicsProfileCatalog.All.Count;

    public static DesktopSmokeTestSummary Create(string title, int tabs)
    {
        return Create(
            title,
            ExpectedPresetCount,
            ExpectedTargetCount,
            ExpectedProfileCount,
            ExpectedPhysicsCount,
            tabs);
    }

    public static DesktopSmokeTestSummary Create(
        string title,
        IReadOnlyCollection<string> presets,
        IReadOnlyCollection<string> targets,
        IReadOnlyCollection<string> profiles,
        IReadOnlyCollection<string> physics,
        IReadOnlyCollection<string> tabTitles)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(physics);
        ArgumentNullException.ThrowIfNull(tabTitles);

        var countsMatch = presets.Count == ExpectedPresetCount &&
            targets.Count == ExpectedTargetCount &&
            profiles.Count == ExpectedProfileCount &&
            physics.Count == ExpectedPhysicsCount;
        var optionsMatch = MatchesExpectedOptions(
                presets,
                PresetCatalog.All.Select(static preset => preset.Name)) &&
            MatchesExpectedOptions(
                targets,
                BodyTypeCatalog.All.Select(static body => body.Name)) &&
            MatchesExpectedOptions(
                profiles,
                DeformationProfileModifier.All) &&
            MatchesExpectedOptions(
                physics,
                PhysicsProfileCatalog.All.Select(PhysicsProfileCatalog.ToDisplayName));
        var normalizedTabTitles = NormalizeValues(tabTitles).ToArray();
        var normalizedExpectedTabTitles = NormalizeValues(ExpectedDesktopTabTitles).ToArray();
        var tabLayoutMatches = normalizedTabTitles.SequenceEqual(
            normalizedExpectedTabTitles,
            StringComparer.OrdinalIgnoreCase);

        return new DesktopSmokeTestSummary(
            tabLayoutMatches && countsMatch && optionsMatch ? ReadyStatus : LayoutMismatchStatus,
            title,
            presets.Count,
            targets.Count,
            profiles.Count,
            physics.Count,
            normalizedTabTitles.Length,
            ExpectedDesktopTabCount,
            normalizedTabTitles,
            normalizedExpectedTabTitles);
    }

    public static DesktopSmokeTestSummary Create(
        string title,
        int presets,
        int targets,
        int profiles,
        int physics,
        int tabs)
    {
        var countsMatch = presets == ExpectedPresetCount &&
            targets == ExpectedTargetCount &&
            profiles == ExpectedProfileCount &&
            physics == ExpectedPhysicsCount;

        return new DesktopSmokeTestSummary(
            tabs == ExpectedDesktopTabCount && countsMatch ? ReadyStatus : LayoutMismatchStatus,
            title,
            presets,
            targets,
            profiles,
            physics,
            tabs,
            ExpectedDesktopTabCount,
            [],
            ExpectedDesktopTabTitles);
    }

    public static DesktopSmokeTestSummary CreateReady(string title, int tabs)
    {
        return Create(
            title,
            ExpectedPresetCount,
            ExpectedTargetCount,
            ExpectedProfileCount,
            ExpectedPhysicsCount,
            tabs);
    }

    public static string Serialize(DesktopSmokeTestSummary payload)
    {
        return JsonSerializer.Serialize(payload);
    }

    public static string Serialize(string title, int tabs)
    {
        return Serialize(Create(title, tabs));
    }

    public static string Serialize(
        string title,
        int presets,
        int targets,
        int profiles,
        int physics,
        int tabs)
    {
        return Serialize(Create(title, presets, targets, profiles, physics, tabs));
    }

    private static bool MatchesExpectedOptions(
        IReadOnlyCollection<string> actual,
        IEnumerable<string> expected)
    {
        var normalizedActual = NormalizeValues(actual)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedExpected = NormalizeValues(expected)
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalizedActual.Length == normalizedExpected.Length &&
               normalizedActual.SequenceEqual(normalizedExpected, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> NormalizeValues(IEnumerable<string> values)
    {
        return values
            .Select(static value => value?.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!);
    }
}
