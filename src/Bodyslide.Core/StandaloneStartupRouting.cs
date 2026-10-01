namespace Bodyslide.Core;

public sealed record StandaloneDesktopLaunchDecision(
    bool ShouldAttemptDesktopHandoff,
    bool LauncherSignalDetected,
    bool ModManagerLaunchDetected,
    bool ExplicitCliLaunchDetected,
    bool StrictLauncherModeEnabled = false,
    string RoutingReason = "");

public static class StandaloneStartupRouting
{
    public static StandaloneDesktopLaunchDecision EvaluateDesktopLaunchDecision(
        IReadOnlyList<string> args,
        string? executablePath,
        string? workingDirectory,
        Func<string, bool>? hasEnvironmentVariable = null,
        bool strictLauncherMode = false)
    {
        hasEnvironmentVariable ??= static name =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));

        var explicitCliLaunch = HasExplicitStandaloneCliSwitch(args);
        var launcherSignal = IsExplicitLauncherSignal(args, hasEnvironmentVariable);
        var modManagerLaunch = IsLikelyModManagerLaunch(args, executablePath, workingDirectory, hasEnvironmentVariable);
        var shouldAttemptDesktopHandoff = args.Count == 0 || launcherSignal || modManagerLaunch;
        var routingReason = strictLauncherMode
            ? shouldAttemptDesktopHandoff
                ? launcherSignal
                    ? "strict-launcher-mode: launcher signal detected"
                    : modManagerLaunch
                        ? "strict-launcher-mode: mod-manager launch detected"
                    : "strict-launcher-mode: no args"
                : "strict-launcher-mode: no launcher signal"
            : shouldAttemptDesktopHandoff
                ? launcherSignal
                    ? "launcher signal detected"
                    : modManagerLaunch
                        ? "mod-manager launch detected"
                    : "default desktop handoff"
                : "explicit cli without launcher signal";

        return new StandaloneDesktopLaunchDecision(
            shouldAttemptDesktopHandoff,
            launcherSignal,
            modManagerLaunch,
            explicitCliLaunch,
            strictLauncherMode,
            routingReason);
    }

    public static bool IsModManagerLauncherArgument(string? arg)
    {
        if (!TryReadOptionName(arg, out var option))
        {
            return false;
        }

        return ModManagerLaunchArgumentCatalog.LauncherSwitchNames.Contains(option, StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasLauncherPathOptionArgument(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (!TryReadOptionName(arg, out var option))
            {
                continue;
            }

            if (!ModManagerLaunchArgumentCatalog.LauncherPathOptionNames.Contains(option, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryReadOptionValue(args, index, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.Trim().Trim('"');
            if (normalized.Length == 0)
            {
                continue;
            }

            if (IsModManagerSpecificPathOption(option) || File.Exists(normalized) || Directory.Exists(normalized))
            {
                return true;
            }
        }

        return false;
    }

    public static bool PathLooksLikeModManagerManagedLocation(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/');
        return normalizedPath.Contains("mod organizer", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("modorganizer", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/mo2/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("/vortex/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains("black tree gaming", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLikelyLauncherPathArgument(string? arg)
    {
        if (string.IsNullOrWhiteSpace(arg) || TryReadOptionName(arg, out _))
        {
            return false;
        }

        var trimmed = arg.Trim().Trim('"');
        if (trimmed.Length == 0 || trimmed.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            var nextSeparator = trimmed.IndexOfAny(['/', '\\'], 1);
            var optionSeparator = trimmed.IndexOfAny(['=', ':'], 1);
            if (nextSeparator >= 0 && (optionSeparator < 0 || nextSeparator < optionSeparator))
            {
                return false;
            }
        }

        if (!trimmed.Contains('\\') &&
            !trimmed.Contains('/') &&
            !trimmed.Contains(':'))
        {
            return false;
        }

        return PathLooksLikeModManagerManagedLocation(trimmed);
    }

    public static bool HasExplicitStandaloneCliSwitch(IReadOnlyList<string> args) =>
        HasStandaloneCommandSwitch(args) || HasStandaloneConversionSwitches(args) || HasStandalonePositionalConversionUsage(args);

    public static bool IsExplicitLauncherSignal(IReadOnlyList<string> args, Func<string, bool> hasEnvironmentVariable) =>
        HasLauncherSignal(args, hasEnvironmentVariable);

    public static bool IsLikelyModManagerLaunch(
        IReadOnlyList<string> args,
        string? executablePath,
        string? workingDirectory,
        Func<string, bool> hasEnvironmentVariable) =>
        HasLauncherSignal(args, hasEnvironmentVariable) ||
        (PathLooksLikeModManagerManagedLocation(executablePath) &&
         PathLooksLikeModManagerManagedLocation(workingDirectory));

    public static bool TryReadOptionName(string? arg, out string option)
    {
        if (TryReadOptionToken(arg, out option, out _))
        {
            return true;
        }

        option = string.Empty;
        return false;
    }

    public static bool TryReadOptionToken(string? arg, out string option, out string? inlineValue)
    {
        option = string.Empty;
        inlineValue = null;
        if (string.IsNullOrWhiteSpace(arg))
        {
            return false;
        }

        var trimmed = arg.Trim();
        if (trimmed.StartsWith("--", StringComparison.Ordinal))
        {
            option = trimmed[2..].Trim();
        }
        else if (trimmed.StartsWith("-", StringComparison.Ordinal) || trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            option = trimmed[1..].Trim();
        }
        else
        {
            return false;
        }

        if (option.Length == 0)
        {
            return false;
        }

        var separatorIndex = option.IndexOf('=');
        if (separatorIndex >= 0)
        {
            inlineValue = separatorIndex + 1 < option.Length ? option[(separatorIndex + 1)..] : string.Empty;
            option = option[..separatorIndex].Trim();
        }
        else
        {
            var colonIndex = option.IndexOf(':');
            if (colonIndex > 1)
            {
                inlineValue = colonIndex + 1 < option.Length ? option[(colonIndex + 1)..] : string.Empty;
                option = option[..colonIndex].Trim();
            }
        }

        if (option.Contains(Path.DirectorySeparatorChar) || option.Contains(Path.AltDirectorySeparatorChar))
        {
            option = string.Empty;
            return false;
        }

        return option.Length > 0;
    }

    private static bool TryReadOptionValue(IReadOnlyList<string> args, int index, out string? value)
    {
        value = null;
        var arg = args[index];
        if (string.IsNullOrWhiteSpace(arg))
        {
            return false;
        }

        var inlineSeparatorIndex = arg.IndexOf('=');
        if (inlineSeparatorIndex < 0)
        {
            var colonIndex = arg.IndexOf(':');
            if (colonIndex > 1)
            {
                inlineSeparatorIndex = colonIndex;
            }
        }

        if (inlineSeparatorIndex >= 0)
        {
            if (inlineSeparatorIndex < arg.Length - 1)
            {
                value = arg[(inlineSeparatorIndex + 1)..];
                return true;
            }

            return false;
        }

        if (index + 1 < args.Count && !TryReadOptionName(args[index + 1], out _))
        {
            value = args[index + 1];
            return true;
        }

        return false;
    }

    private static bool IsLikelyModManagerEnvironment(Func<string, bool> hasEnvironmentVariable) =>
        hasEnvironmentVariable("MO2_INSTANCE") ||
        hasEnvironmentVariable("USVFS_PARAMETERS") ||
        hasEnvironmentVariable("USVFS_PROCESS") ||
        hasEnvironmentVariable("USVFS_PROXY") ||
        hasEnvironmentVariable("MODORGANIZER_INSTANCE") ||
        hasEnvironmentVariable("MODORGANIZER_PATH") ||
        hasEnvironmentVariable("MODORGANIZER_ROOT") ||
        hasEnvironmentVariable("VORTEX_USERDATA") ||
        hasEnvironmentVariable("VORTEX_PROFILE_ID") ||
        hasEnvironmentVariable("VORTEX_STAGING_FOLDER") ||
        hasEnvironmentVariable("VORTEX_INSTANCE_ID") ||
        hasEnvironmentVariable("VORTEX_SESSION");

    private static bool IsModManagerSpecificPathOption(string option) =>
        option.StartsWith("mo2-", StringComparison.OrdinalIgnoreCase) ||
        option.StartsWith("modorganizer-", StringComparison.OrdinalIgnoreCase) ||
        option.StartsWith("vortex-", StringComparison.OrdinalIgnoreCase);

    private static bool HasLauncherSignal(IReadOnlyList<string> args, Func<string, bool> hasEnvironmentVariable) =>
        IsLikelyModManagerEnvironment(hasEnvironmentVariable) ||
        args.Any(IsModManagerLauncherArgument) ||
        args.Any(IsLikelyLauncherPathArgument) ||
        HasLauncherPathOptionArgument(args);

    private static bool HasStandaloneCommandSwitch(IReadOnlyList<string> args) =>
        args.Any(static arg =>
        {
            if (!TryReadOptionName(arg, out var option))
            {
                return false;
            }

            if (IsModManagerLauncherArgument(arg))
            {
                return false;
            }

            return option.Equals("pause", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("h", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("list-bodies", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("list-presets", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("list-profiles", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("list-physics", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("self-check", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("conversion-guide", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("export-cache", StringComparison.OrdinalIgnoreCase) ||
                   option.Equals("body-reference", StringComparison.OrdinalIgnoreCase);
        });

    private static bool HasStandaloneConversionSwitches(IReadOnlyList<string> args)
    {
        var hasModManagerLauncherArgument = args.Any(IsModManagerLauncherArgument);
        var hasTarget = false;
        var hasConversionModifier = false;

        foreach (var arg in args)
        {
            if (!TryReadOptionName(arg, out var option))
            {
                continue;
            }

            if (option.Equals("target", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("targets", StringComparison.OrdinalIgnoreCase))
            {
                hasTarget = true;
                continue;
            }

            if (option.Equals("preset", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("presets", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("input", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("output", StringComparison.OrdinalIgnoreCase) ||
                (option.Equals("profile", StringComparison.OrdinalIgnoreCase) && !hasModManagerLauncherArgument) ||
                option.Equals("source", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("physics", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("cache-path", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("output-zip", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("skeleton-nif", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("skeleton-nif-path", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("build-sliders", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("custom-profiles", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("world-mode", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("shared-plugin-output", StringComparison.OrdinalIgnoreCase))
            {
                hasConversionModifier = true;
            }
        }

        return hasTarget || hasConversionModifier;
    }

    private static bool HasStandalonePositionalConversionUsage(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            return false;
        }

        if (TryReadOptionName(args[0], out _) || TryReadOptionName(args[1], out _))
        {
            return false;
        }

        return !IsLikelyLauncherPathArgument(args[0]);
    }

}
