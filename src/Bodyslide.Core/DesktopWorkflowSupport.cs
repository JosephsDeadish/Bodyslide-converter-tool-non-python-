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

    public static string? ResolveSourceBodyOverride(
        string? comboText,
        string? selectedItem,
        string? autoDetectedSourceBody)
    {
        if (IsAutoSelectionText(comboText) && IsAutoSelectionText(selectedItem))
        {
            return null;
        }

        var displayed = ResolveDisplayedSourceBody(comboText, selectedItem, autoDetectedSourceBody);
        if (string.IsNullOrWhiteSpace(displayed))
        {
            return null;
        }

        return BodyTypeCatalog.ResolveName(displayed);
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
        var candidateFromResultArgument = false;
        var fromMo2 = args.Any(static arg => IsMo2LauncherArgument(arg));

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (TryReadNamedArgumentValue(args, index, out var consumedIndex, out var value, out var fromResultArgument) &&
                !string.IsNullOrWhiteSpace(value))
            {
                candidatePath ??= value;
                candidateFromResultArgument |= fromResultArgument;
                index = consumedIndex;
                continue;
            }

            if (IsOptionToken(arg))
            {
                continue;
            }

            if (candidatePath is null)
            {
                candidatePath = arg;
            }
        }

        var startupOutputDirectory = TryResolveResultOutputDirectory(candidatePath, allowAncestorWalk: candidateFromResultArgument);
        return new DesktopLaunchOptions(
            startupOutputDirectory,
            startupOutputDirectory is null ? TryResolveExistingInputPath(candidatePath) : null,
            fromMo2);
    }

    public static string? TryResolveResultOutputDirectory(string? candidatePath, bool allowAncestorWalk = true)
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

        var fullCandidatePath = Path.GetFullPath(normalizedCandidate);
        if (Directory.Exists(fullCandidatePath))
        {
            if (LooksLikeSlideSmithOutputDirectory(fullCandidatePath))
            {
                return fullCandidatePath;
            }

            return allowAncestorWalk
                ? TryWalkAncestorResultDirectory(fullCandidatePath)
                : null;
        }

        if (!File.Exists(fullCandidatePath))
        {
            return null;
        }

        var parentDirectory = Path.GetDirectoryName(fullCandidatePath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return null;
        }

        if (IsResultMarkerFile(fullCandidatePath) && LooksLikeSlideSmithOutputDirectory(parentDirectory))
        {
            return parentDirectory;
        }

        return allowAncestorWalk
            ? TryWalkAncestorResultDirectory(parentDirectory)
            : null;
    }

    public static bool LooksLikeSlideSmithOutputDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return DesktopResultMarkerFiles.Any(fileName => File.Exists(Path.Combine(directory, fileName)));
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
        out string? value,
        out bool fromResultArgument)
    {
        consumedIndex = index;
        value = null;
        fromResultArgument = false;

        var arg = args[index];
        if (!TryExtractOptionToken(arg, out var key, out var inlineValue))
        {
            return false;
        }

        if (!DesktopResultArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }
        fromResultArgument = true;

        if (inlineValue is not null)
        {
            value = inlineValue;
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

    private static string? TryWalkAncestorResultDirectory(string? currentDirectory)
    {
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

    private static bool IsResultMarkerFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return !string.IsNullOrWhiteSpace(fileName) &&
               DesktopResultMarkerFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsMo2LauncherArgument(string? argument)
    {
        if (!TryExtractOptionToken(argument, out var key, out _))
        {
            return false;
        }

        return key.StartsWith("mo2-", StringComparison.OrdinalIgnoreCase) ||
               key.StartsWith("modorganizer-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRecognizedOptionToken(string arg)
    {
        if (!TryExtractOptionToken(arg, out var key, out _))
        {
            return false;
        }
        return DesktopResultArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               key.Equals("mo2-launcher", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("modorganizer-launcher", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOptionToken(string? arg) =>
        TryExtractOptionToken(arg, out _, out _);

    private static bool TryExtractOptionToken(string? arg, out string key, out string? inlineValue)
    {
        key = string.Empty;
        inlineValue = null;
        if (string.IsNullOrWhiteSpace(arg))
        {
            return false;
        }

        string trimmed;
        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            trimmed = arg[2..];
        }
        else if (arg.StartsWith("-", StringComparison.Ordinal) || arg.StartsWith("/", StringComparison.Ordinal))
        {
            trimmed = arg[1..];
        }
        else
        {
            return false;
        }

        if (trimmed.Length == 0)
        {
            return false;
        }

        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex < 0)
        {
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 1)
            {
                separatorIndex = colonIndex;
            }
        }
        if (separatorIndex >= 0)
        {
            key = trimmed[..separatorIndex];
            if (key.Contains(Path.DirectorySeparatorChar) || key.Contains(Path.AltDirectorySeparatorChar))
            {
                return false;
            }
            inlineValue = trimmed[(separatorIndex + 1)..];
            return key.Length > 0;
        }

        key = trimmed;
        if (key.Contains(Path.DirectorySeparatorChar) || key.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }
        return key.Length > 0;
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
