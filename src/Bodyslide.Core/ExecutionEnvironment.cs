namespace Bodyslide.Core;

public static class ExecutionEnvironment
{
    public static string GetExecutionRoot(
        string? processPath = null,
        string? appContextBaseDirectory = null,
        string? currentDirectory = null)
    {
        var processDirectory = GetFileDirectoryOrNull(processPath);
        if (!string.IsNullOrWhiteSpace(processDirectory))
        {
            return processDirectory;
        }

        var appBaseDirectory = NormalizeDirectoryOrNull(appContextBaseDirectory);
        if (!string.IsNullOrWhiteSpace(appBaseDirectory))
        {
            return appBaseDirectory;
        }

        return NormalizeDirectoryOrNull(currentDirectory)
            ?? Path.GetFullPath(Environment.CurrentDirectory);
    }

    public static string GetDefaultOutputRoot(
        string? processPath = null,
        string? appContextBaseDirectory = null,
        string? currentDirectory = null)
    {
        return Path.Combine(
            GetExecutionRoot(processPath, appContextBaseDirectory, currentDirectory),
            "output");
    }

    public static bool TryNormalizeCurrentDirectoryToExecutionRoot(
        string? processPath = null,
        string? appContextBaseDirectory = null)
    {
        try
        {
            var executionRoot = GetExecutionRoot(processPath, appContextBaseDirectory);
            if (string.IsNullOrWhiteSpace(executionRoot) || !Directory.Exists(executionRoot))
            {
                return false;
            }

            var currentDirectory = Path.GetFullPath(Environment.CurrentDirectory);
            if (string.Equals(currentDirectory, executionRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Environment.CurrentDirectory = executionRoot;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetFileDirectoryOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        return string.IsNullOrWhiteSpace(directory)
            ? null
            : directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string? NormalizeDirectoryOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
