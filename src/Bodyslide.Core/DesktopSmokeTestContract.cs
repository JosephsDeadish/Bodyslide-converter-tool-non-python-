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
    [property: JsonPropertyName("expectedTabs")] int ExpectedTabs);

public static class DesktopSmokeTestContract
{
    public const string ReadyStatus = "ok";
    public const string LayoutMismatchStatus = "layout-mismatch";
    public const int ExpectedDesktopTabCount = 10;

    public static DesktopSmokeTestSummary Create(string title, int tabs)
    {
        return new DesktopSmokeTestSummary(
            tabs == ExpectedDesktopTabCount ? ReadyStatus : LayoutMismatchStatus,
            title,
            PresetCatalog.All.Count,
            BodyTypeCatalog.All.Count,
            DeformationProfileModifier.All.Count,
            PhysicsProfileCatalog.All.Count,
            tabs,
            ExpectedDesktopTabCount);
    }

    public static DesktopSmokeTestSummary CreateReady(string title, int tabs)
    {
        return new DesktopSmokeTestSummary(
            ReadyStatus,
            title,
            PresetCatalog.All.Count,
            BodyTypeCatalog.All.Count,
            DeformationProfileModifier.All.Count,
            PhysicsProfileCatalog.All.Count,
            tabs,
            ExpectedDesktopTabCount);
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
        return JsonSerializer.Serialize(new
        {
            status = ReadyStatus,
            title,
            presets,
            targets,
            profiles,
            physics,
            tabs,
            expectedTabs = ExpectedDesktopTabCount
        });
    }
}
