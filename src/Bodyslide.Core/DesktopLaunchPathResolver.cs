using System.Text.Json;

namespace Bodyslide.Core;

internal static class DesktopLaunchPathResolver
{
    private const string DesktopTargetFramework = "net10.0-windows";
    private static readonly string[] DesktopConfigurations = ["Debug", "Release"];

    internal static string GetLauncherDirectory(string processPath, string applicationBaseDirectory)
    {
        var hostName = Path.GetFileNameWithoutExtension(processPath);
        return hostName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationBaseDirectory))
            : Path.GetDirectoryName(Path.GetFullPath(processPath))!;
    }

    internal static IReadOnlyList<string> GetDesktopCandidates(
        IReadOnlyList<string> directories, bool useDll)
    {
        var names = useDll
            ? new[] { "SlideSmith.Desktop.dll", "Bodyslide.Desktop.dll", "SlideSmith.dll" }
            : new[] { "SlideSmith.exe", "SlideSmith.Desktop.exe", "Bodyslide.Desktop.exe", "SlideSmith-Desktop.exe" };
        var candidates = new List<string>();
        for (var index = 0; index < directories.Count; index++)
        {
            var directory = directories[index];
            var acceptsSharedName = index == 0 ||
                Path.GetFileName(Path.TrimEndingDirectorySeparator(directory))
                    .Equals("desktop", StringComparison.OrdinalIgnoreCase) ||
                HasDesktopRuntimeConfiguration(directory);
            foreach (var name in names)
            {
                if (name.Equals(useDll ? "SlideSmith.dll" : "SlideSmith.exe", StringComparison.OrdinalIgnoreCase) &&
                    !acceptsSharedName)
                {
                    continue;
                }
                candidates.Add(Path.Combine(directory, name));
            }
        }
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool HasDesktopRuntimeConfiguration(string directory)
    {
        try
        {
            var path = Path.Combine(directory, "SlideSmith.runtimeconfig.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024)
            {
                return false;
            }
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("runtimeOptions", out var options) ||
                options.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            return (options.TryGetProperty("framework", out var framework) && IsDesktopFramework(framework)) ||
                HasDesktopFrameworkArray(options, "frameworks") ||
                HasDesktopFrameworkArray(options, "includedFrameworks");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool HasDesktopFrameworkArray(JsonElement options, string property) =>
        options.TryGetProperty(property, out var frameworks) &&
        frameworks.ValueKind == JsonValueKind.Array &&
        frameworks.EnumerateArray().Any(IsDesktopFramework);

    private static bool IsDesktopFramework(JsonElement framework) =>
        framework.ValueKind == JsonValueKind.Object &&
        framework.TryGetProperty("name", out var name) &&
        name.ValueKind == JsonValueKind.String &&
        name.GetString() == "Microsoft.WindowsDesktop.App";

    internal static string ResolveWorkingDirectory(string launchWorkingDirectory, string desktopPath)
    {
        return Directory.Exists(launchWorkingDirectory)
            ? Path.GetFullPath(launchWorkingDirectory)
            : Path.GetDirectoryName(Path.GetFullPath(desktopPath))!;
    }

    internal static string? FindCliExecutable(string desktopDirectory)
    {
        if (string.IsNullOrWhiteSpace(desktopDirectory))
        {
            return null;
        }

        var nestedCliDirectory = Path.Combine(desktopDirectory, "cli");
        var siblingCliDirectory = Path.GetFullPath(Path.Combine(desktopDirectory, "..", "cli"));
        return new[]
        {
            Path.Combine(desktopDirectory, "SlideSmith-CLI.exe"),
            Path.Combine(nestedCliDirectory, "SlideSmith-CLI.exe"),
            Path.Combine(siblingCliDirectory, "SlideSmith-CLI.exe"),
            Path.Combine(nestedCliDirectory, "SlideSmith.exe"),
            Path.Combine(siblingCliDirectory, "SlideSmith.exe")
        }.FirstOrDefault(File.Exists);
    }

    internal static IReadOnlyList<string> GetLikelyDesktopCandidateDirectories(string executableDirectory)
    {
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return [];
        }

        var normalizedExecutableDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(executableDirectory));
        var parentDirectory = Path.GetDirectoryName(normalizedExecutableDirectory);

        var candidateDirectories = new List<string>
        {
            normalizedExecutableDirectory,
            parentDirectory ?? string.Empty,
            Path.Combine(normalizedExecutableDirectory, "desktop"),
            Path.Combine(normalizedExecutableDirectory, "bin"),
            Path.Combine(normalizedExecutableDirectory, "publish")
        };

        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            candidateDirectories.Add(Path.Combine(parentDirectory, "desktop"));
            candidateDirectories.Add(Path.Combine(parentDirectory, "bin"));
            candidateDirectories.Add(Path.Combine(parentDirectory, "publish"));
        }

        var ancestorDirectory = parentDirectory;
        for (var depth = 0; depth < 5 && !string.IsNullOrWhiteSpace(ancestorDirectory); depth++)
        {
            foreach (var configuration in DesktopConfigurations)
            {
                candidateDirectories.Add(Path.Combine(ancestorDirectory, "Bodyslide.Desktop", "bin", configuration, DesktopTargetFramework));
            }
            candidateDirectories.Add(Path.Combine(ancestorDirectory, "Bodyslide.Desktop", "publish"));
            ancestorDirectory = Path.GetDirectoryName(ancestorDirectory);
        }

        return candidateDirectories
            .Where(static directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
