using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record DesktopSmokeTestSummary(
    string Status,
    string Title,
    int Presets,
    int Targets,
    int Profiles,
    int Physics,
    int Tabs);

internal static class DesktopSmokeTestContract
{
    internal const string ReadyStatus = "ok";

    internal static string Serialize(
        string title,
        int presets,
        int targets,
        int profiles,
        int physics,
        int tabs)
    {
        return JsonSerializer.Serialize(new DesktopSmokeTestSummary(
            ReadyStatus,
            title,
            presets,
            targets,
            profiles,
            physics,
            tabs));
    }
}
