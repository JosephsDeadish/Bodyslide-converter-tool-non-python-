using System.Text.Json;

namespace Bodyslide.Core;

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
        return JsonSerializer.Serialize(new
        {
            status = ReadyStatus,
            title,
            presets,
            targets,
            profiles,
            physics,
            tabs
        });
    }
}
