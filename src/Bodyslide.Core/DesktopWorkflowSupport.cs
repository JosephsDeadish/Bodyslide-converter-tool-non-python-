using System.Text;

namespace Bodyslide.Core;

internal static class ModManagerLaunchArgumentCatalog
{
    internal static readonly string[] LauncherSwitchNames =
    [
        "mo2-launcher",
        "modorganizer-launcher",
        "vortex-launcher",
        "from-modmanager",
        "mo2",
        "modorganizer",
        "vortex",
        "nxmhandler",
        "from-modorganizer",
        "from-vortex",
        "from-mo2"
    ];

    internal static readonly string[] PathOptionNames =
    [
        "load-result",
        "result",
        "mo2-output",
        "mo2-result",
        "mo2-mod",
        "mo2-path",
        "modorganizer-output",
        "modorganizer-result",
        "modorganizer-mod",
        "modorganizer-path",
        "vortex-output",
        "vortex-result",
        "vortex-mod",
        "vortex-path",
        "vortex-stage",
        "vortex-staging",
        "vortex-deployment",
        "vortex-deploy-path",
        "vortex-deployment-path",
        "vortex-staging-path",
        "vortex-mod-path",
        "mods-path",
        "mod-path",
        "output-dir",
        "output-path",
        "staging-path"
    ];

    internal static readonly string[] InputFallbackPathOptionNames =
    [
        "result",
        "load-result",
        ..LauncherPathOptionNames
    ];

    internal static readonly string[] LauncherPathOptionNames =
    [
        "mo2-output",
        "mo2-result",
        "mo2-mod",
        "mo2-path",
        "modorganizer-output",
        "modorganizer-result",
        "modorganizer-mod",
        "modorganizer-path",
        "vortex-output",
        "vortex-result",
        "vortex-mod",
        "vortex-path",
        "vortex-staging",
        "vortex-stage",
        "vortex-deployment",
        "vortex-deploy-path",
        "vortex-deployment-path",
        "vortex-staging-path",
        "vortex-mod-path",
        "mods-path",
        "mod-path",
        "output-dir",
        "output-path",
        "staging-path"
    ];

    internal static readonly string[] StartupDiagnosticsArgumentNames =
    [
        "startup-diagnostics",
        "launcher-handoff-diagnostics"
    ];
}

internal sealed record DesktopLaunchOptions(
    string? StartupOutputDirectory,
    string? StartupInputPath,
    bool FromModOrganizerLauncher,
    string? StartupDiagnostics = null)
{
    internal static DesktopLaunchOptions Empty { get; } = new(null, null, false, null);
}

internal sealed record DesktopStartupHandoffPlan(
    bool ShouldLoadStartupResult,
    bool ShouldApplyStartupInput,
    bool ShouldQueueStartupAutoInspect,
    string? StartupInputPath);

internal static class DesktopWorkflowSupport
{
    private static readonly StringComparison FileSystemPathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly string[] DesktopResultArgumentNames =
    [
        "load-result",
        "output",
        ..ModManagerLaunchArgumentCatalog.PathOptionNames
    ];
    private static readonly string[] DesktopInputFallbackArgumentNames =
    [
        "output",
        ..ModManagerLaunchArgumentCatalog.InputFallbackPathOptionNames
    ];
    private static readonly string[] DesktopInputArgumentNames =
    [
        "input",
        "path",
        "source",
        "file",
        "folder"
    ];
    private static readonly string[] StartupDiagnosticsArgumentNames = ModManagerLaunchArgumentCatalog.StartupDiagnosticsArgumentNames;

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

    public static string BuildMo2SetupGuidance(
        string processPath,
        string desktopPath,
        string workingDirectory,
        string? cliPath,
        bool looksLikeCliTarget)
    {
        var guidance = new StringBuilder()
            .AppendLine("Recommended mod manager setup for SlideSmith")
            .AppendLine($"Title: SlideSmith (Desktop)")
            .AppendLine($"Binary: {desktopPath}")
            .AppendLine($"Start in: {workingDirectory}")
            .AppendLine("MO2 note: keep Binary and Start in on the exact desktop executable folder so the VFS/USVFS hook can inject mods before startup.")
            .AppendLine("Troubleshooting: pass startup diagnostics with --startup-diagnostics or --launcher-handoff-diagnostics when you need launcher handoff logs.")
            .AppendLine("Arguments (MO2): --mo2-launcher")
            .AppendLine("Arguments (Vortex): --vortex-launcher")
            .AppendLine();

        if (looksLikeCliTarget && !desktopPath.Equals(processPath, StringComparison.OrdinalIgnoreCase))
        {
            guidance.AppendLine($"Detected current process as CLI ({processPath}) and switched suggested MO2 binary to desktop executable ({desktopPath}).")
                .AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(cliPath))
        {
            guidance.AppendLine("Optional CLI entry:")
                .AppendLine("Title: SlideSmith CLI")
                .AppendLine($"Binary: {cliPath}")
                .AppendLine($"Start in: {Path.GetDirectoryName(cliPath)}")
                .AppendLine("Arguments: --mo2-launcher");
        }

        return guidance.ToString();
    }

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

        var resultCandidates = new List<(string Path, bool AllowsInputFallback)>();
        var inputCandidates = new List<string>();
        string? positionalCandidatePath = null;
        string? startupDiagnostics = null;
        var fromModManagerLauncher = args.Any(static arg => IsModManagerLauncherArgument(arg));

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (TryReadStartupDiagnosticsArgument(args, index, out var diagnosticsConsumedIndex, out var diagnosticsValue))
            {
                if (!string.IsNullOrWhiteSpace(diagnosticsValue))
                {
                    startupDiagnostics = diagnosticsValue;
                }

                index = diagnosticsConsumedIndex;
                continue;
            }

            if (TryReadNamedArgumentValue(args, index, out var consumedIndex, out var value, out var fromResultArgument, out var allowsInputFallback) &&
                !string.IsNullOrWhiteSpace(value))
            {
                if (fromResultArgument)
                {
                    resultCandidates.Add((value, allowsInputFallback));
                }
                else
                {
                    inputCandidates.Add(value);
                }
                index = consumedIndex;
                continue;
            }

            if (IsOptionToken(arg))
            {
                continue;
            }

            if (positionalCandidatePath is null)
            {
                if (index > 0 &&
                    (OptionTokenConsumesFollowingValue(args[index - 1]) ||
                     IsLikelyUnrecognizedOptionValue(args[index - 1], arg)))
                {
                    continue;
                }

                positionalCandidatePath = arg;
            }
        }

        string? startupOutputDirectory = null;
        if (resultCandidates.Count > 0)
        {
            foreach (var candidate in resultCandidates.AsEnumerable().Reverse())
            {
                startupOutputDirectory = TryResolveResultOutputDirectory(candidate.Path, allowAncestorWalk: true);
                if (!string.IsNullOrWhiteSpace(startupOutputDirectory))
                {
                    break;
                }
            }
        }
        else
        {
            startupOutputDirectory = TryResolveResultOutputDirectory(positionalCandidatePath, allowAncestorWalk: false);
        }

        string? startupInputPath = null;
        if (startupOutputDirectory is null)
        {
            foreach (var candidate in resultCandidates.AsEnumerable().Reverse().Where(static entry => entry.AllowsInputFallback))
            {
                startupInputPath = TryResolveExistingInputPath(candidate.Path);
                if (startupInputPath is not null)
                {
                    break;
                }
            }

            if (startupInputPath is null)
            {
                foreach (var inputCandidatePath in inputCandidates.AsEnumerable().Reverse())
                {
                    startupInputPath = TryResolveExistingInputPath(inputCandidatePath);
                    if (startupInputPath is not null)
                    {
                        break;
                    }
                }
            }

            if (startupInputPath is null &&
                !string.IsNullOrWhiteSpace(positionalCandidatePath))
            {
                startupInputPath = TryResolveExistingInputPath(positionalCandidatePath);
            }
        }

        return new DesktopLaunchOptions(
            startupOutputDirectory,
            startupInputPath,
            fromModManagerLauncher,
            startupDiagnostics);
    }

    public static DesktopStartupHandoffPlan BuildStartupHandoffPlan(
        DesktopLaunchOptions launchOptions,
        Func<string, bool> shouldAutoInspectInputPath,
        Func<string, bool>? fileExists = null,
        Func<string, bool>? directoryExists = null)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);
        ArgumentNullException.ThrowIfNull(shouldAutoInspectInputPath);
        fileExists ??= File.Exists;
        directoryExists ??= Directory.Exists;

        if (!string.IsNullOrWhiteSpace(launchOptions.StartupOutputDirectory))
        {
            return new DesktopStartupHandoffPlan(
                ShouldLoadStartupResult: true,
                ShouldApplyStartupInput: false,
                ShouldQueueStartupAutoInspect: false,
                StartupInputPath: null);
        }

        var inputPath = launchOptions.StartupInputPath;
        var hasUsableInputPath =
            !string.IsNullOrWhiteSpace(inputPath) &&
            (fileExists(inputPath) || directoryExists(inputPath));
        if (!hasUsableInputPath)
        {
            return new DesktopStartupHandoffPlan(
                ShouldLoadStartupResult: false,
                ShouldApplyStartupInput: false,
                ShouldQueueStartupAutoInspect: false,
                StartupInputPath: null);
        }

        return new DesktopStartupHandoffPlan(
            ShouldLoadStartupResult: false,
            ShouldApplyStartupInput: true,
            ShouldQueueStartupAutoInspect: shouldAutoInspectInputPath(inputPath!),
            StartupInputPath: inputPath);
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
        out bool fromResultArgument,
        out bool allowsInputFallback)
    {
        consumedIndex = index;
        value = null;
        fromResultArgument = false;
        allowsInputFallback = false;

        var arg = args[index];
        if (!TryExtractOptionToken(arg, out var key, out var inlineValue))
        {
            return false;
        }

        var isResultArgument = DesktopResultArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase);
        var isInputArgument = DesktopInputArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase);
        if (!isResultArgument && !isInputArgument)
        {
            return false;
        }
        fromResultArgument = isResultArgument;
        allowsInputFallback = isResultArgument &&
                              DesktopInputFallbackArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase);

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

    private static bool TryReadStartupDiagnosticsArgument(
        IReadOnlyList<string> args,
        int index,
        out int consumedIndex,
        out string? diagnostics)
    {
        consumedIndex = index;
        diagnostics = null;

        var arg = args[index];
        if (!TryExtractOptionToken(arg, out var key, out var inlineValue) ||
            !StartupDiagnosticsArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(inlineValue))
        {
            diagnostics = DecodeStartupDiagnostics(inlineValue);
            return true;
        }

        if (index + 1 < args.Count)
        {
            var next = args[index + 1];
            if (!string.IsNullOrWhiteSpace(next) && !LooksLikeRecognizedOptionToken(next))
            {
                diagnostics = DecodeStartupDiagnostics(next);
                consumedIndex = index + 1;
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

    private static bool IsModManagerLauncherArgument(string? argument)
    {
        if (!TryExtractOptionToken(argument, out var key, out _))
        {
            return false;
        }

        return ModManagerLaunchArgumentCatalog.LauncherSwitchNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               ModManagerLaunchArgumentCatalog.LauncherPathOptionNames.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRecognizedOptionToken(string arg)
    {
        if (!TryExtractOptionToken(arg, out var key, out _))
        {
            return false;
        }
        return DesktopResultArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               DesktopInputArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               StartupDiagnosticsArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               key.Equals("mo2-launcher", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("modorganizer-launcher", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("vortex-launcher", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("from-modmanager", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("nxmhandler", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("mo2", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("modorganizer", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("from-modorganizer", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("from-vortex", StringComparison.OrdinalIgnoreCase) ||
               key.Equals("from-mo2", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOptionToken(string? arg) =>
        TryExtractOptionToken(arg, out _, out _);

    private static bool OptionTokenConsumesFollowingValue(string? arg)
    {
        if (!TryExtractOptionToken(arg, out var key, out var inlineValue))
        {
            return false;
        }

        if (inlineValue is not null)
        {
            return false;
        }

        return DesktopResultArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               DesktopInputArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               StartupDiagnosticsArgumentNames.Contains(key, StringComparer.OrdinalIgnoreCase) ||
               key.Equals("profile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyUnrecognizedOptionValue(string? previousArg, string currentArg)
    {
        if (!TryExtractOptionToken(previousArg, out _, out var inlineValue) || inlineValue is not null)
        {
            return false;
        }

        if (LooksLikeRecognizedOptionToken(previousArg!))
        {
            return false;
        }

        return LooksLikePathToken(currentArg);
    }

    private static bool LooksLikePathToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.Contains('\\') ||
               trimmed.Contains('/') ||
               trimmed.Contains(':') ||
               Path.HasExtension(trimmed);
    }

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
                var potentialKey = trimmed[..colonIndex];
                var recognizesColonSeparatedValue =
                    DesktopResultArgumentNames.Contains(potentialKey, StringComparer.OrdinalIgnoreCase) ||
                    DesktopInputArgumentNames.Contains(potentialKey, StringComparer.OrdinalIgnoreCase) ||
                    StartupDiagnosticsArgumentNames.Contains(potentialKey, StringComparer.OrdinalIgnoreCase) ||
                    potentialKey.Equals("mo2-launcher", StringComparison.OrdinalIgnoreCase) ||
                    potentialKey.Equals("modorganizer-launcher", StringComparison.OrdinalIgnoreCase) ||
                    potentialKey.Equals("vortex-launcher", StringComparison.OrdinalIgnoreCase) ||
                    potentialKey.Equals("nxmhandler", StringComparison.OrdinalIgnoreCase) ||
                    potentialKey.Equals("mo2", StringComparison.OrdinalIgnoreCase) ||
                    potentialKey.Equals("modorganizer", StringComparison.OrdinalIgnoreCase) ||
                    potentialKey.Equals("from-modorganizer", StringComparison.OrdinalIgnoreCase) ||
                    potentialKey.Equals("from-vortex", StringComparison.OrdinalIgnoreCase) ||
                    potentialKey.Equals("from-mo2", StringComparison.OrdinalIgnoreCase);
                if (recognizesColonSeparatedValue)
                {
                    separatorIndex = colonIndex;
                }
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

    private static string DecodeStartupDiagnostics(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var trimmed = rawValue.Trim().Trim('"');
        var decoded = trimmed;
        try
        {
            decoded = Uri.UnescapeDataString(trimmed);
        }
        catch
        {
            decoded = trimmed;
        }

        if (File.Exists(decoded))
        {
            try
            {
                var lines = File.ReadAllLines(decoded);
                if (lines.Length == 0)
                {
                    return $"startup diagnostics file was empty: {decoded}";
                }

                var tail = lines
                    .Where(static line => !string.IsNullOrWhiteSpace(line))
                    .TakeLast(6)
                    .ToArray();
                return tail.Length == 0
                    ? $"startup diagnostics file was empty: {decoded}"
                    : string.Join(" | ", tail);
            }
            catch
            {
                return $"startup diagnostics file could not be read: {decoded}";
            }
        }

        return decoded;
    }
}
