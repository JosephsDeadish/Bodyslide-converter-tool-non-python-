namespace Bodyslide.Core;

internal static class DesktopLaunchPathResolver
{
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
            candidateDirectories.Add(Path.Combine(ancestorDirectory, "Bodyslide.Desktop", "bin", "Debug", "net10.0-windows"));
            candidateDirectories.Add(Path.Combine(ancestorDirectory, "Bodyslide.Desktop", "bin", "Release", "net10.0-windows"));
            candidateDirectories.Add(Path.Combine(ancestorDirectory, "Bodyslide.Desktop", "publish"));
            ancestorDirectory = Path.GetDirectoryName(ancestorDirectory);
        }

        return candidateDirectories
            .Where(static directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
