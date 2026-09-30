namespace Bodyslide.Core;

internal static class DesktopLaunchPathResolver
{
    private const string DesktopTargetFramework = "net10.0-windows";
    private static readonly string[] DesktopConfigurations = ["Debug", "Release"];

    internal static IReadOnlyList<string> GetLikelyDesktopCandidateDirectories(string executableDirectory)
    {
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return [];
        }

        var normalizedExecutableDirectory = Path.GetFullPath(executableDirectory);
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
