namespace Bodyslide.Core;

public static class ExecutionEnvironment
{
    private static readonly StringComparison FileSystemPathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

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

    public static string GetDefaultOutputRootForInput(
        string? inputPath,
        string? processPath = null,
        string? appContextBaseDirectory = null,
        string? currentDirectory = null)
    {
        var inputRoot = TryGetInputAdjacentOutputRoot(inputPath);
        return !string.IsNullOrWhiteSpace(inputRoot)
            ? inputRoot
            : GetDefaultOutputRoot(processPath, appContextBaseDirectory, currentDirectory);
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

            var currentDirectory = Path.GetFullPath(Environment.CurrentDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedExecutionRoot = executionRoot
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(currentDirectory, normalizedExecutionRoot, FileSystemPathComparison))
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

    private static string? TryGetInputAdjacentOutputRoot(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        var fullInputPath = Path.GetFullPath(inputPath);
        var normalizedInputPath = fullInputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parentDirectory = IsLikelyDirectoryInputPath(inputPath, normalizedInputPath)
            ? normalizedInputPath
            : Path.GetDirectoryName(fullInputPath);

        return string.IsNullOrWhiteSpace(parentDirectory)
            ? null
            : Path.Combine(parentDirectory, "SlideSmith-output");
    }

    private static bool IsLikelyDirectoryInputPath(string originalInputPath, string normalizedInputPath)
    {
        if (Directory.Exists(normalizedInputPath))
        {
            return true;
        }

        if (File.Exists(normalizedInputPath))
        {
            return false;
        }

        var trimmedOriginal = originalInputPath.Trim();
        if (trimmedOriginal.EndsWith(Path.DirectorySeparatorChar) ||
            trimmedOriginal.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        var leafName = Path.GetFileName(trimmedOriginal.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (Path.HasExtension(leafName))
        {
            return false;
        }

        if (IsLikelyFileName(leafName))
        {
            return false;
        }

        return IsLikelyDirectoryName(leafName);
    }

    private static bool IsLikelyFileName(string leafName)
    {
        if (string.IsNullOrWhiteSpace(leafName))
        {
            return false;
        }

        var normalizedLeafName = leafName.Trim().ToLowerInvariant();
        return normalizedLeafName.Contains("archive", StringComparison.Ordinal) ||
               normalizedLeafName.Contains("launcher", StringComparison.Ordinal) ||
               normalizedLeafName.Contains("binary", StringComparison.Ordinal) ||
               normalizedLeafName.Contains("executable", StringComparison.Ordinal) ||
               normalizedLeafName.Contains("program", StringComparison.Ordinal);
    }

    private static bool IsLikelyDirectoryName(string leafName)
    {
        if (string.IsNullOrWhiteSpace(leafName))
        {
            return false;
        }

        var normalizedLeafName = leafName.Trim().ToLowerInvariant();
        return normalizedLeafName.Contains("pack", StringComparison.Ordinal) ||
               normalizedLeafName.Contains("folder", StringComparison.Ordinal) ||
               normalizedLeafName.Contains("input", StringComparison.Ordinal) ||
               normalizedLeafName.Equals("mods", StringComparison.Ordinal) ||
               normalizedLeafName.EndsWith("mods", StringComparison.Ordinal);
    }
}
