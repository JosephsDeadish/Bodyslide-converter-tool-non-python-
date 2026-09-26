namespace Bodyslide.Core;

internal sealed record DesktopLaunchOptions(string? StartupOutputDirectory, string? StartupInputPath, bool FromModOrganizerLauncher)
{
    internal static DesktopLaunchOptions Empty { get; } = new(null, null, false);
}

internal static class DesktopWorkflowSupport
{
    private static readonly StringComparison FileSystemPathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly string[] DesktopResultArgumentNames =
    [
        "load-result",
        "result",
        "output",
        "mo2-output",
        "mo2-result",
        "mo2-mod"
    ];

    private static readonly string[] DesktopResultMarkerFiles =
    [
        "preview-workbench.html",
        "preview.html",
        "batch-report.json",
        "conversion-quality.json",
        "armor-pack-validation.json",
        "desktop-workflow-automation.json",
        "proof-harness-bundle.json",
        "proof-result-bundle.json",
        "runtime-validation-plan.json",
        "live-game-execution.json"
    ];

    public static string? ReadOptionalSelection(string? selected) =>
        string.IsNullOrWhiteSpace(selected) || selected.Trim().Equals("(auto)", StringComparison.OrdinalIgnoreCase)
            ? null
            : selected.Trim();

    public static bool IsAutoSelectionText(string? selected) =>
        string.IsNullOrWhiteSpace(selected) ||
        selected.Trim().Equals("(auto)", StringComparison.OrdinalIgnoreCase);

    public static string? ResolveDisplayedSourceBody(
        string? comboText,
        string? selectedItem,
        string? autoDetectedSourceBody)
    {
        if (IsAutoSelectionText(comboText))
        {
            return !string.IsNullOrWhiteSpace(autoDetectedSourceBody)
                ? autoDetectedSourceBody.Trim()
                : selectedItem?.Trim();
        }

        return string.IsNullOrWhiteSpace(comboText)
            ? selectedItem?.Trim()
            : comboText.Trim();
    }

    public static string? ReadOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    public static IReadOnlyList<string> ParseDelimitedValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    public static IReadOnlyList<string> CombineSelections(string? primarySelection, IReadOnlyList<string> extraSelections)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(primarySelection))
        {
            values.Add(primarySelection.Trim());
        }

        values.AddRange(extraSelections.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()));
        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? GetBestOutputDirectory(IReadOnlyList<ConversionResult> results)
    {
        if (results.Count == 0)
        {
            return null;
        }

        var first = results[0].OutputDirectory;
        if (results.All(r => string.Equals(r.OutputDirectory, first, FileSystemPathComparison)))
        {
            return first;
        }

        var commonRoot = FindCommonDirectory(results.Select(r => r.OutputDirectory));
        if (!string.IsNullOrWhiteSpace(commonRoot) && Directory.Exists(commonRoot))
        {
            return commonRoot;
        }

        return Directory.Exists(first) ? first : Path.GetDirectoryName(first);
    }

    public static string? GetFirstExistingOutputFile(IReadOnlyList<ConversionResult> results, params string[] fileNames)
    {
        if (fileNames.Length == 0)
        {
            return null;
        }

        return results
            .SelectMany(r => r.OutputFiles)
            .Where(File.Exists)
            .OrderBy(path => GetPreviewCandidateRank(Path.GetFileName(path), fileNames))
            .FirstOrDefault(path =>
                fileNames.Any(fileName => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)));
    }

    public static int GetPreviewCandidateRank(string? fileName, IReadOnlyList<string> fileNames)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return int.MaxValue;
        }

        for (var index = 0; index < fileNames.Count; index++)
        {
            if (fileName.Equals(fileNames[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    public static string? ResolvePreviewPath(string folder, IReadOnlyList<string> previewFileCandidates)
    {
        foreach (var candidate in previewFileCandidates)
        {
            var path = Path.Combine(folder, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static string? FindCommonDirectory(IEnumerable<string> directories)
    {
        var normalized = directories
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        if (normalized.Length == 0)
        {
            return null;
        }

        var candidate = normalized[0];
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            var matchPrefix = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var allMatch = normalized.All(path =>
                string.Equals(path, candidate, FileSystemPathComparison) ||
                path.StartsWith(matchPrefix, FileSystemPathComparison));
            if (allMatch)
            {
                return candidate;
            }

            candidate = Path.GetDirectoryName(candidate);
        }

        return null;
    }

    public static DesktopLaunchOptions ParseLaunchOptions(IReadOnlyList<string>? args)
    {
        if (args is null || args.Count == 0)
        {
            return DesktopLaunchOptions.Empty;
        }

        string? candidatePath = null;
        var fromMo2 = args.Any(static arg =>
            !string.IsNullOrWhiteSpace(arg) &&
            (arg.Equals("--mo2-launcher", StringComparison.OrdinalIgnoreCase) ||
             arg.Equals("--modorganizer-launcher", StringComparison.OrdinalIgnoreCase) ||
             arg.StartsWith("--mo2-launcher=", StringComparison.OrdinalIgnoreCase) ||
             arg.StartsWith("--modorganizer-launcher=", StringComparison.OrdinalIgnoreCase)));

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (TryReadNamedArgumentValue(args, index, out var consumedIndex, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                candidatePath ??= value;
                index = consumedIndex;
                continue;
            }

            if (arg.StartsWith('-'))
            {
                continue;
            }

            if (candidatePath is null)
            {
                candidatePath = arg;
            }
        }

        var startupOutputDirectory = TryResolveResultOutputDirectory(candidatePath);
        return new DesktopLaunchOptions(
            startupOutputDirectory,
            startupOutputDirectory is null ? TryResolveExistingInputPath(candidatePath) : null,
            fromMo2);
    }

    public static string? TryResolveResultOutputDirectory(string? candidatePath)
    {
        var normalizedCandidate = NormalizeCandidatePath(candidatePath);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return null;
        }

        if (!File.Exists(normalizedCandidate) && !Directory.Exists(normalizedCandidate))
        {
            return null;
        }

        var currentDirectory = Directory.Exists(normalizedCandidate)
            ? Path.GetFullPath(normalizedCandidate)
            : Path.GetDirectoryName(Path.GetFullPath(normalizedCandidate));
        while (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            if (LooksLikeSlideSmithOutputDirectory(currentDirectory))
            {
                return currentDirectory;
            }

            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }

        return null;
    }

    public static bool LooksLikeSlideSmithOutputDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        if (DesktopResultMarkerFiles.Any(fileName => File.Exists(Path.Combine(directory, fileName))))
        {
            return true;
        }

        var fomodDirectory = Path.Combine(directory, "fomod");
        if (Directory.Exists(fomodDirectory))
        {
            return File.Exists(Path.Combine(fomodDirectory, "ModuleConfig.xml")) ||
                   File.Exists(Path.Combine(fomodDirectory, "info.xml"));
        }

        return false;
    }

    public static string? TryResolveExistingInputPath(string? candidatePath)
    {
        var normalizedCandidate = NormalizeCandidatePath(candidatePath);
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return null;
        }

        if (File.Exists(normalizedCandidate))
        {
            return Path.GetFullPath(normalizedCandidate);
        }

        return Directory.Exists(normalizedCandidate)
            ? Path.GetFullPath(normalizedCandidate)
            : null;
    }

    private static bool TryReadNamedArgumentValue(
        IReadOnlyList<string> args,
        int index,
        out int consumedIndex,
        out string? value)
    {
        consumedIndex = index;
        value = null;

        var arg = args[index];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = arg.IndexOf('=');
        var key = separatorIndex >= 0 ? arg[2..separatorIndex] : arg[2..];
        if (!DesktopResultArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (separatorIndex >= 0)
        {
            value = arg[(separatorIndex + 1)..];
            return true;
        }

        if (index + 1 < args.Count)
        {
            var next = args[index + 1];
            if (!string.IsNullOrWhiteSpace(next) && !LooksLikeRecognizedOptionToken(next))
            {
                value = next;
                consumedIndex = index + 1;
                return true;
            }
        }

        return true;
    }

    private static bool LooksLikeRecognizedOptionToken(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = arg.IndexOf('=');
        var key = separatorIndex >= 0 ? arg[2..separatorIndex] : arg[2..];
        return DesktopResultArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               key.Equals("mo2-launcher", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("modorganizer-launcher", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeCandidatePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
