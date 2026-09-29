using Bodyslide.Core;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

ExecutionEnvironment.TryNormalizeCurrentDirectoryToExecutionRoot(
    Environment.ProcessPath,
    AppContext.BaseDirectory);

var shouldPauseOnExit = ShouldPauseOnExit(args);
var startupDiagnosticsPath = ResolveStartupDiagnosticsPath(args);
WriteStartupDiagnostics(
    startupDiagnosticsPath,
    $"startup: exe={Environment.ProcessPath ?? "(unknown)"}, cwd={Environment.CurrentDirectory}, args=[{string.Join(", ", args)}]");

if (TryLaunchDesktopGuiOnWindows(args, startupDiagnosticsPath))
{
    WriteStartupDiagnostics(startupDiagnosticsPath, "desktop-launch: handoff complete");
    return;
}

var parsedArgs = ParseNamedArguments(args);

if (args.Contains("--conversion-guide", StringComparer.OrdinalIgnoreCase))
{
    WriteConversionGuide();
    return;
}

if (TryGetNamedValue(parsedArgs, "body-reference", out var bodyReference))
{
    WriteBodyReference(bodyReference);
    return;
}

if (args.Contains("--export-cache", StringComparer.OrdinalIgnoreCase))
{
    parsedArgs.TryGetValue("export-cache", out var exportCachePath);
    parsedArgs.TryGetValue("cache-path", out var exportCacheOverridePath);

    // Allow --export-cache <path> OR --cache-path <path> to specify the cache file location.
    var resolvedPath = (!string.IsNullOrWhiteSpace(exportCachePath) && exportCachePath != "true")
        ? exportCachePath
        : exportCacheOverridePath;

    if (!string.IsNullOrWhiteSpace(resolvedPath))
    {
        ConversionLearningCache.SetGlobalCachePath(resolvedPath);
    }

    var entries = await ConversionLearningCache.LoadMergedEntriesAsync(string.Empty, CancellationToken.None);
    if (entries.Count == 0)
    {
        Console.WriteLine("Learning cache is empty. Run at least one successful conversion to populate it.");
    }
    else
    {
        Console.WriteLine($"Learning cache — {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}:");
        foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  [{entry.Key}]");
            Console.WriteLine($"    Target body  : {entry.TargetBody}");
            Console.WriteLine($"    Mesh type    : {entry.MeshType}");
            Console.WriteLine($"    Strategy     : {entry.Strategy}");
            Console.WriteLine($"    Had clipping : {entry.HadClipping}");
            Console.WriteLine($"    Correction   : {entry.CorrectionMethod}");
            Console.WriteLine($"    Cached at    : {entry.LastSuccessfulConversion:u}");
            if (entry.RegionalMorphing.Count > 0)
            {
                Console.WriteLine("    Regional morphs:");
                foreach (var (region, factor) in entry.RegionalMorphing
                    .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"      {region,-14}: {factor:F4}");
                }
            }

            Console.WriteLine();
        }
    }

    return;
}

if (args.Contains("--list-presets", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Available presets:");
    foreach (var preset in PresetCatalog.All.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($" - {preset.Name} => {preset.TargetBody} ({preset.DeformationProfile}, {preset.PhysicsProfile})");
    }

    return;
}

if (args.Contains("--list-profiles", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Available deformation profiles:");
    foreach (var profile in DeformationProfileModifier.All)
    {
        Console.WriteLine($" - {profile}");
    }

    return;
}

if (args.Contains("--list-bodies", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Supported body types (signature detection + conversion reference):");
    foreach (var body in BodyTypeCatalog.All)
    {
        var vcRange = body.VertexCountMin > 0
            ? $"  vertices: {body.VertexCountMin}–{body.VertexCountMax}"
            : string.Empty;
        Console.WriteLine($" - {body.Name,-8} tokens: [{string.Join(", ", body.DetectionTokens)}]{vcRange}");
        if (BodyTechnicalProfileCatalog.TryGet(body.Name, out var profile))
        {
            var defaultPhysicsDisplay = PhysicsProfileCatalog.ToDisplayName(profile.DefaultPhysics);
            Console.WriteLine($"           skeleton: {profile.SkeletonFoundation}");
            Console.WriteLine($"           supports physics: {(profile.SupportsPhysics ? "yes" : "no")}");
            Console.WriteLine($"           default physics: {defaultPhysicsDisplay} [{profile.DefaultPhysics}]");
            Console.WriteLine($"           recommended physics: {PhysicsProfileCatalog.ToDisplayName(profile.RecommendedPhysicsProfile)} [{profile.RecommendedPhysicsProfile}]");
            Console.WriteLine($"           support regions: {FormatDisplayList(profile.ExpectedSemanticRegions)}");
            Console.WriteLine($"           collision regions: {FormatDisplayList(profile.ExpectedCollisionRegions)} ({profile.CollisionComplexity})");
            Console.WriteLine($"           bilateral regions: {FormatDisplayList(profile.ExpectedBilateralRegions)}");
            Console.WriteLine($"           minimum physics coverage: slots {profile.MinimumPhysicsSlotCount}, families {profile.MinimumPhysicsFamilyCount}, chain depth {profile.MinimumPhysicsChainDepth}");
            Console.WriteLine($"           physics-capable bones: {(profile.SupportsPhysics ? string.Join(", ", profile.RequiredPhysicsBones) : "none")}");
            Console.WriteLine($"           notes: {profile.Notes}");
        }
    }
    Console.WriteLine(" - all/any/*  alias: convert to every supported body type");

    return;
}

if (args.Contains("--list-physics", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("Available canonical physics engine profiles (can be applied to ANY body via --physics):");
    foreach (var profile in PhysicsProfileCatalog.All)
    {
        PhysicsProfileCatalog.Descriptions.TryGetValue(profile, out var desc);
        var display = PhysicsProfileCatalog.ToDisplayName(profile);
        var label = string.Equals(display, profile, StringComparison.OrdinalIgnoreCase)
            ? profile
            : $"{display} [{profile}]";
        Console.WriteLine($" - {label,-34}{(desc is not null ? $"  {desc}" : string.Empty)}");
    }
    Console.WriteLine("Aliases accepted: soft-body / full-soft-body / hdt-smp / fsmp / cbp => canonical physics profiles");
    Console.WriteLine(" - auto         => use preset/custom/default target-body physics");

    return;
}

if (args.Contains("--self-check", StringComparer.OrdinalIgnoreCase))
{
    WriteSelfCheck();
    return;
}

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase)
    || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
{
    WriteUsage();
    return;
}

if (!TryParseRequest(args, out var request, out var error, out var cachePath))
{
    var missingRequiredArgs = !string.IsNullOrEmpty(error) &&
        (error.StartsWith("Missing required", StringComparison.OrdinalIgnoreCase)
         || error.StartsWith("Provide --target", StringComparison.OrdinalIgnoreCase));

    if (!string.IsNullOrEmpty(error))
    {
        Console.WriteLine($"No conversion executed: {error}");
        Console.WriteLine();
    }

    WriteUsage();

    if (shouldPauseOnExit)
    {
        PauseBeforeExit();
    }

    if (missingRequiredArgs)
    {
        Environment.ExitCode = 2;
    }

    return;
}

// Apply global cache path override before running any conversion.
if (!string.IsNullOrWhiteSpace(cachePath))
{
    ConversionLearningCache.SetGlobalCachePath(cachePath);
}

try
{
    var orchestrator = StandaloneConversionModules.CreateDefault();
    var batchRunner = new BatchConversionRunner(orchestrator);
    var results = await batchRunner.ConvertAsync(request);

    Console.WriteLine($"Converted {results.Count} armor item(s).");
    foreach (var result in results)
    {
        Console.WriteLine($"Output: {result.OutputDirectory}");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($" - {step}");
        }

        WritePostConversionGuidance(result);
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Conversion failed: {ex.Message}");

    if (shouldPauseOnExit)
    {
        PauseBeforeExit();
    }

    Environment.ExitCode = 1;
}

static bool TryParseRequest(string[] args, out ConversionRequest request, out string error, out string? cachePath)
{
    request = default!;
    error = string.Empty;
    cachePath = null;

    if (args.Length >= 2 && !args[0].StartsWith("--", StringComparison.Ordinal))
    {
        request = new ConversionRequest(args[0], args[1], args.Length > 2 ? args[2] : null);
        return true;
    }

    var parsed = ParseNamedArguments(args);
    parsed.TryGetValue("input", out var input);
    parsed.TryGetValue("target", out var target);
    parsed.TryGetValue("output", out var output);
    parsed.TryGetValue("preset", out var preset);
    parsed.TryGetValue("presets", out var presetsValue);
    parsed.TryGetValue("profile", out var profile);
    parsed.TryGetValue("source", out var source);
    parsed.TryGetValue("targets", out var targetsValue);
    parsed.TryGetValue("cache-path", out cachePath);
    parsed.TryGetValue("physics", out var physicsOverride);
    parsed.TryGetValue("world-mode", out var worldModeOverride);
    parsed.TryGetValue("build-sliders", out var buildSlidersValue);
    parsed.TryGetValue("skeleton-nif", out var skeletonNif);
    var outputZip = parsed.ContainsKey("output-zip");
    var selectedTargets = CombineSelections(target, ParseDelimitedValues(targetsValue));
    var selectedPresets = CombineSelections(preset, ParseDelimitedValues(presetsValue));
    var normalizedPhysicsOverride = string.Empty;
    if (!string.IsNullOrWhiteSpace(physicsOverride) &&
        !string.Equals(physicsOverride, "auto", StringComparison.OrdinalIgnoreCase) &&
        !PhysicsProfileCatalog.TryNormalize(physicsOverride, out normalizedPhysicsOverride))
    {
        error = $"Unknown --physics value '{physicsOverride}'. Use --list-physics to view available profiles.";
        return false;
    }

    var generateBodySlideFiles = true;
    if (parsed.ContainsKey("build-sliders") &&
        !TryParseBooleanOption(buildSlidersValue, out generateBodySlideFiles))
    {
        error = $"Invalid --build-sliders value '{buildSlidersValue}'. Use true/false, yes/no, on/off, or 1/0.";
        return false;
    }

    var normalizedWorldModeOverride = string.Empty;
    if (!string.IsNullOrWhiteSpace(worldModeOverride) &&
        !string.Equals(worldModeOverride, "auto", StringComparison.OrdinalIgnoreCase) &&
        !WorldDropModeCatalog.TryNormalize(worldModeOverride, out normalizedWorldModeOverride))
    {
        error = $"Unknown --world-mode value '{worldModeOverride}'. Use auto, static, or rigid-proxy.";
        return false;
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        error = "Missing required --input value.";
        return false;
    }

    if (selectedTargets.Count == 0 && selectedPresets.Count == 0)
    {
        error = "Provide --target/--targets or --preset/--presets.";
        return false;
    }

    if (!string.IsNullOrWhiteSpace(skeletonNif))
    {
        skeletonNif = skeletonNif.Trim().Trim('"');
        if (!SkeletonSupportPathResolver.TryResolveSkeletonNifPath(skeletonNif, out var resolvedSkeletonNif))
        {
            error = $"Could not resolve a usable skeleton .nif from '{skeletonNif}'. Provide a skeleton .nif directly, an XP32/XPMSSE mod folder, or a related .pex file from the same mod.";
            return false;
        }

        skeletonNif = resolvedSkeletonNif;
    }

    request = new ConversionRequest(
        InputPath: input,
        TargetBody: selectedTargets.FirstOrDefault() ?? string.Empty,
        OutputDirectory: output,
        Preset: selectedPresets.FirstOrDefault(),
        OutputZip: outputZip,
        DeformationProfile: profile,
        SourceBodyOverride: source,
        TargetBodies: selectedTargets.Count > 1 ? selectedTargets : null,
        Presets: selectedPresets.Count > 1 ? selectedPresets : null,
        PhysicsProfileOverride: string.IsNullOrWhiteSpace(normalizedPhysicsOverride) ? null : normalizedPhysicsOverride,
        GenerateBodySlideFiles: generateBodySlideFiles,
        WorldDropModeOverride: string.IsNullOrWhiteSpace(normalizedWorldModeOverride) ? null : normalizedWorldModeOverride,
        SkeletonNifPath: string.IsNullOrWhiteSpace(skeletonNif) ? null : skeletonNif);

    return true;
}

static Dictionary<string, string> ParseNamedArguments(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = arg[2..];
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            map[key] = "true";
            continue;
        }

        map[key] = args[i + 1];
        i++;
    }

    return map;
}

static bool ShouldPauseOnExit(string[] args)
{
    if (args.Length == 0)
    {
        return true;
    }

    return args.Length == 1 && !args[0].StartsWith("--", StringComparison.Ordinal);
}

static bool TryLaunchDesktopGuiOnWindows(string[] args, string? startupDiagnosticsPath)
{
    if (!OperatingSystem.IsWindows())
    {
        return false;
    }

    try
    {
        var currentExePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExePath))
        {
            return false;
        }

        var executableDirectory = Path.GetDirectoryName(currentExePath);
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return false;
        }

        var currentExeFullPath = Path.GetFullPath(currentExePath);
        var explicitCliLaunch = HasExplicitStandaloneCliSwitch(args);
        var launchedFromModOrganizer = IsLikelyModOrganizerLaunch(args, currentExeFullPath, Environment.CurrentDirectory);
        WriteStartupDiagnostics(
            startupDiagnosticsPath,
            $"desktop-launch: mo2={launchedFromModOrganizer}, cli={explicitCliLaunch}, exe={currentExeFullPath}");
        if (args.Length != 0 && (explicitCliLaunch || !launchedFromModOrganizer))
        {
            WriteStartupDiagnostics(startupDiagnosticsPath, "desktop-launch: skipped (non-launcher invocation)");
            return false;
        }

        var siblingDesktopDirectory = Path.GetFullPath(Path.Combine(executableDirectory, "..", "desktop"));
        foreach (var desktopExePath in new[]
                 {
                     Path.Combine(executableDirectory, "SlideSmith.exe"),
                     Path.Combine(executableDirectory, "SlideSmith.Desktop.exe"),
                     Path.Combine(executableDirectory, "Bodyslide.Desktop.exe"),
                     Path.Combine(executableDirectory, "SlideSmith-Desktop.exe"),
                     Path.Combine(siblingDesktopDirectory, "SlideSmith.exe"),
                     Path.Combine(siblingDesktopDirectory, "SlideSmith.Desktop.exe"),
                     Path.Combine(siblingDesktopDirectory, "Bodyslide.Desktop.exe"),
                     Path.Combine(siblingDesktopDirectory, "SlideSmith-Desktop.exe")
                 })
        {
            if (!File.Exists(desktopExePath))
            {
                continue;
            }

            if (string.Equals(Path.GetFullPath(desktopExePath), currentExeFullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryStartDesktopProcess(desktopExePath, executableDirectory, launchedFromModOrganizer, args, startupDiagnosticsPath, out var launched))
            {
                if (launchedFromModOrganizer && launched is not null)
                {
                    try
                    {
                        if (launched.WaitForExit(1500) && launched.ExitCode != 0)
                        {
                            WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: candidate exited early with code {launched.ExitCode}: {desktopExePath}");
                            continue;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                return true;
            }
        }

        foreach (var desktopDllPath in new[]
                 {
                     Path.Combine(executableDirectory, "SlideSmith.Desktop.dll"),
                     Path.Combine(executableDirectory, "Bodyslide.Desktop.dll"),
                     Path.Combine(siblingDesktopDirectory, "SlideSmith.Desktop.dll"),
                     Path.Combine(siblingDesktopDirectory, "Bodyslide.Desktop.dll")
                 })
        {
            if (!File.Exists(desktopDllPath))
            {
                continue;
            }

            if (TryStartDesktopDllProcess(desktopDllPath, executableDirectory, launchedFromModOrganizer, args, startupDiagnosticsPath, out var launched))
            {
                if (launchedFromModOrganizer && launched is not null)
                {
                    try
                    {
                        if (launched.WaitForExit(1500) && launched.ExitCode != 0)
                        {
                            WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: dll candidate exited early with code {launched.ExitCode}: {desktopDllPath}");
                            continue;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                return true;
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not auto-launch desktop GUI: {ex.Message}");
        WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: exception {ex.GetType().Name}: {ex.Message}");
    }

    WriteStartupDiagnostics(startupDiagnosticsPath, "desktop-launch: no candidate succeeded");
    return false;
}

static bool TryStartDesktopProcess(string desktopExePath, string fallbackWorkingDirectory, bool launchedFromModOrganizer, IReadOnlyList<string> forwardedArgs, string? startupDiagnosticsPath, out Process? launchedProcess)
{
    launchedProcess = null;
    var workingDirectory = Path.GetDirectoryName(desktopExePath) ?? fallbackWorkingDirectory;

    var startInfo = new ProcessStartInfo
    {
        FileName = desktopExePath,
        WorkingDirectory = workingDirectory,
        UseShellExecute = true
    };
    ForwardDesktopLaunchArgs(startInfo, forwardedArgs, launchedFromModOrganizer, startupDiagnosticsPath);

    try
    {
        launchedProcess = Process.Start(startInfo);
        WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: started exe candidate {desktopExePath}");
        return launchedProcess is not null;
    }
    catch (Exception ex)
    {
        WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: shell start failed for {desktopExePath} ({ex.GetType().Name}: {ex.Message})");
        var fallback = new ProcessStartInfo
        {
            FileName = desktopExePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        ForwardDesktopLaunchArgs(fallback, forwardedArgs, launchedFromModOrganizer, startupDiagnosticsPath);
        try
        {
            launchedProcess = Process.Start(fallback);
            WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: started exe candidate (fallback) {desktopExePath}");
            return launchedProcess is not null;
        }
        catch (Exception fallbackEx)
        {
            WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: fallback failed for {desktopExePath} ({fallbackEx.GetType().Name}: {fallbackEx.Message})");
            launchedProcess = null;
            return false;
        }
    }
}

static bool TryStartDesktopDllProcess(string desktopDllPath, string fallbackWorkingDirectory, bool launchedFromModOrganizer, IReadOnlyList<string> forwardedArgs, string? startupDiagnosticsPath, out Process? launchedProcess)
{
    launchedProcess = null;
    var workingDirectory = Path.GetDirectoryName(desktopDllPath) ?? fallbackWorkingDirectory;
    var dotnetHost = ResolveDotnetHostPath();
    var startInfo = new ProcessStartInfo
    {
        FileName = dotnetHost,
        WorkingDirectory = workingDirectory,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add(desktopDllPath);
    ForwardDesktopLaunchArgs(startInfo, forwardedArgs, launchedFromModOrganizer, startupDiagnosticsPath);

    try
    {
        launchedProcess = Process.Start(startInfo);
        WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: started dll candidate {desktopDllPath}");
        return launchedProcess is not null;
    }
    catch (Exception ex)
    {
        WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: dll candidate failed {desktopDllPath} ({ex.GetType().Name}: {ex.Message})");
        launchedProcess = null;
        return false;
    }
}

static string ResolveDotnetHostPath()
{
    var envHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
    if (!string.IsNullOrWhiteSpace(envHost) && File.Exists(envHost))
    {
        return envHost;
    }

    var currentHost = Environment.ProcessPath;
    if (!string.IsNullOrWhiteSpace(currentHost))
    {
        var hostFileName = Path.GetFileNameWithoutExtension(currentHost);
        if (hostFileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return currentHost;
        }

        var currentDirectory = Path.GetDirectoryName(currentHost);
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            var siblingHost = Path.Combine(currentDirectory, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(siblingHost))
            {
                return siblingHost;
            }
        }
    }

    return "dotnet";
}

static void ForwardDesktopLaunchArgs(ProcessStartInfo startInfo, IReadOnlyList<string> forwardedArgs, bool launchedFromModOrganizer, string? startupDiagnosticsPath)
{
    if (forwardedArgs.Count > 0)
    {
        foreach (var arg in forwardedArgs)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            startInfo.ArgumentList.Add(arg);
        }
    }

    if (!string.IsNullOrWhiteSpace(startupDiagnosticsPath) &&
        !HasStartupDiagnosticsArgumentWithValue(forwardedArgs))
    {
        startInfo.ArgumentList.Add("--startup-diagnostics");
        startInfo.ArgumentList.Add(startupDiagnosticsPath);
    }

    if (!launchedFromModOrganizer)
    {
        return;
    }

    var alreadyTagged = forwardedArgs.Any(IsMo2LauncherArg);
    if (!alreadyTagged)
    {
        startInfo.ArgumentList.Add("--mo2-launcher");
    }
}

static bool HasStartupDiagnosticsArgumentWithValue(IReadOnlyList<string> args)
{
    for (var index = 0; index < args.Count; index++)
    {
        var arg = args[index];
        if (string.IsNullOrWhiteSpace(arg))
        {
            continue;
        }

        if (arg.StartsWith("--startup-diagnostics=", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("--startup-diagnostics:", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = arg.IndexOfAny(['=', ':']);
            if (separatorIndex >= 0 && separatorIndex < arg.Length - 1)
            {
                var inlineValue = arg[(separatorIndex + 1)..];
                if (!string.IsNullOrWhiteSpace(NormalizeDiagnosticsPath(inlineValue)))
                {
                    return true;
                }
            }
            continue;
        }

        if (!IsStandaloneOptionMatch(arg, "startup-diagnostics"))
        {
            continue;
        }

        if (index + 1 >= args.Count)
        {
            continue;
        }

        var next = args[index + 1];
        if (!string.IsNullOrWhiteSpace(next) &&
            !TryReadLongOptionName(next, out _) &&
            !string.IsNullOrWhiteSpace(NormalizeDiagnosticsPath(next)))
        {
            return true;
        }
    }

    return false;
}

static bool IsLikelyModOrganizerLaunch(IReadOnlyList<string> args, string? executablePath, string? workingDirectory) =>
    IsLikelyModOrganizerEnvironment() ||
    args.Any(IsMo2LauncherArg) ||
    HasLauncherPathOptionArgument(args) ||
    args.Any(IsLikelyLauncherPathArgument) ||
    (PathLooksLikeModOrganizerManagedLocation(executablePath) &&
     PathLooksLikeModOrganizerManagedLocation(workingDirectory));

static bool HasExplicitStandaloneCliSwitch(IReadOnlyList<string> args) =>
    HasStandaloneCommandSwitch(args) || HasStandaloneConversionSwitches(args);

static bool HasStandaloneCommandSwitch(IReadOnlyList<string> args) =>
    args.Any(static arg =>
    {
        if (!TryReadLongOptionName(arg, out var option))
        {
            return false;
        }

        if (option.StartsWith("mo2-", StringComparison.OrdinalIgnoreCase) ||
            option.StartsWith("modorganizer-", StringComparison.OrdinalIgnoreCase) ||
            option.StartsWith("vortex-", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("vortex", StringComparison.OrdinalIgnoreCase))
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

static bool HasStandaloneConversionSwitches(IReadOnlyList<string> args)
{
    var hasTarget = false;
    var hasConversionModifier = false;

    foreach (var arg in args)
    {
        if (!TryReadLongOptionName(arg, out var option))
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
            option.Equals("deformation-profile", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("source-body", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("physics", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("cache-path", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("zip", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("skeleton-nif", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("skeleton-nif-path", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("generate-bodyslide-files", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("custom-profiles", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("world-drop-mode", StringComparison.OrdinalIgnoreCase) ||
            option.Equals("shared-plugin-output", StringComparison.OrdinalIgnoreCase))
        {
            hasConversionModifier = true;
        }
    }

    return hasTarget || hasConversionModifier;
}

static bool TryReadLongOptionName(string? arg, out string option)
{
    option = string.Empty;
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
        option = option[..separatorIndex];
    }
    else
    {
        var colonIndex = option.IndexOf(':');
        if (colonIndex > 1)
        {
            option = option[..colonIndex];
        }
    }

    if (option.Contains(Path.DirectorySeparatorChar) || option.Contains(Path.AltDirectorySeparatorChar))
    {
        option = string.Empty;
        return false;
    }

    return option.Length > 0;
}

static string? ResolveStartupDiagnosticsPath(IReadOnlyList<string> args)
{
    for (var index = 0; index < args.Count; index++)
    {
        var arg = args[index];
        if (string.IsNullOrWhiteSpace(arg))
        {
            continue;
        }

        if (arg.StartsWith("--startup-diagnostics=", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeDiagnosticsPath(arg["--startup-diagnostics=".Length..]);
        }

        if (arg.StartsWith("--startup-diagnostics:", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeDiagnosticsPath(arg["--startup-diagnostics:".Length..]);
        }

        if (!TryReadLongOptionName(arg, out var option) ||
            !option.Equals("startup-diagnostics", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (index + 1 < args.Count)
        {
            var next = args[index + 1];
            if (!TryReadLongOptionName(next, out _))
            {
                return NormalizeDiagnosticsPath(next);
            }
        }
    }

    if (!IsStandaloneDiagnosticsEnabledByEnvironment())
    {
        return null;
    }

    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (!string.IsNullOrWhiteSpace(localAppData))
    {
        return Path.Combine(localAppData, "SlideSmith", "startup-launch-diagnostics.log");
    }

    return Path.Combine(Path.GetTempPath(), "SlideSmith", "startup-launch-diagnostics.log");
}

static string? NormalizeDiagnosticsPath(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    var trimmed = value.Trim().Trim('"');
    return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
}

static bool IsStandaloneDiagnosticsEnabledByEnvironment()
{
    var flag = Environment.GetEnvironmentVariable("SLIDESMITH_STARTUP_DIAGNOSTICS");
    return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(flag, "yes", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(flag, "on", StringComparison.OrdinalIgnoreCase);
}

static void WriteStartupDiagnostics(string? diagnosticsPath, string message)
{
    if (string.IsNullOrWhiteSpace(diagnosticsPath))
    {
        return;
    }

    try
    {
        var path = Path.GetFullPath(diagnosticsPath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.AppendAllText(
            path,
            $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
    }
    catch
    {
    }
}

static bool IsStandaloneOptionMatch(string? arg, string optionName)
{
    if (!TryReadLongOptionName(arg, out var option))
    {
        return false;
    }

    return option.Equals(optionName, StringComparison.OrdinalIgnoreCase);
}

static bool IsMo2LauncherArg(string? arg)
{
    if (string.IsNullOrWhiteSpace(arg))
    {
        return false;
    }

    var trimmed = arg.Trim();
    if (trimmed.StartsWith("--", StringComparison.Ordinal))
    {
        trimmed = trimmed[2..];
    }
    else if (trimmed.StartsWith("-", StringComparison.Ordinal) || trimmed.StartsWith("/", StringComparison.Ordinal))
    {
        trimmed = trimmed[1..];
    }

    if (trimmed.Length == 0)
    {
        return false;
    }

    var separatorIndex = trimmed.IndexOf('=');
    if (separatorIndex >= 0)
    {
        trimmed = trimmed[..separatorIndex];
    }

    return trimmed.StartsWith("mo2-", StringComparison.OrdinalIgnoreCase) ||
           trimmed.StartsWith("modorganizer-", StringComparison.OrdinalIgnoreCase) ||
           trimmed.StartsWith("vortex-", StringComparison.OrdinalIgnoreCase) ||
           trimmed.Equals("vortex", StringComparison.OrdinalIgnoreCase) ||
           trimmed.Equals("nxmhandler", StringComparison.OrdinalIgnoreCase) ||
           trimmed.Equals("from-vortex", StringComparison.OrdinalIgnoreCase) ||
           trimmed.Equals("from-mo2", StringComparison.OrdinalIgnoreCase);
}

static bool IsLikelyModOrganizerEnvironment()
{
    return HasEnvironmentVariable("MO2_INSTANCE") ||
           HasEnvironmentVariable("USVFS_PARAMETERS") ||
           HasEnvironmentVariable("USVFS_PROCESS") ||
           HasEnvironmentVariable("USVFS_PROXY") ||
           HasEnvironmentVariable("MODORGANIZER_INSTANCE") ||
           HasEnvironmentVariable("MODORGANIZER_PATH") ||
           HasEnvironmentVariable("MODORGANIZER_ROOT") ||
           HasEnvironmentVariable("VORTEX_USERDATA") ||
           HasEnvironmentVariable("VORTEX_PROFILE_ID") ||
           HasEnvironmentVariable("VORTEX_STAGING_FOLDER") ||
           HasEnvironmentVariable("VORTEX_INSTANCE_ID") ||
           HasEnvironmentVariable("VORTEX_SESSION");
}

static bool HasEnvironmentVariable(string name) =>
    !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));

static bool PathLooksLikeModOrganizerManagedLocation(string? path)
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

static bool IsLikelyLauncherPathArgument(string? arg)
{
    if (string.IsNullOrWhiteSpace(arg))
    {
        return false;
    }

    var trimmed = arg.Trim().Trim('"');
    if (trimmed.Length == 0 || trimmed.StartsWith("--", StringComparison.Ordinal))
    {
        return false;
    }

    if (!trimmed.Contains('\\') &&
        !trimmed.Contains('/') &&
        !trimmed.Contains(':'))
    {
        return false;
    }

    return PathLooksLikeModOrganizerManagedLocation(trimmed);
}

static bool HasLauncherPathOptionArgument(IReadOnlyList<string> args)
{
    for (var index = 0; index < args.Count; index++)
    {
        var arg = args[index];
        if (!TryReadLongOptionName(arg, out var option))
        {
            continue;
        }

        if (!option.Equals("mo2-output", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("mo2-result", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("mo2-mod", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("mo2-path", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("modorganizer-path", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("vortex-output", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("vortex-result", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("vortex-mod", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("vortex-path", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("vortex-stage", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("vortex-staging", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("vortex-deployment", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("mods-path", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("mod-path", StringComparison.OrdinalIgnoreCase) &&
            !option.Equals("staging-path", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        string? value = null;
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
            }
        }
        else if (index + 1 < args.Count && !TryReadLongOptionName(args[index + 1], out _))
        {
            value = args[index + 1];
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            continue;
        }

        var normalized = value.Trim().Trim('"');
        if (normalized.Length == 0)
        {
            continue;
        }

        if (File.Exists(normalized) || Directory.Exists(normalized))
        {
            return true;
        }

        if (normalized.Contains('\\') || normalized.Contains('/') || normalized.Contains(':'))
        {
            return true;
        }
    }

    return false;
}

static IReadOnlyList<string> ParseDelimitedValues(string? value) =>
    string.IsNullOrWhiteSpace(value)
        ? []
        : value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

static IReadOnlyList<string> CombineSelections(string? singleValue, IReadOnlyList<string> multiValues)
{
    var combined = new List<string>();
    if (!string.IsNullOrWhiteSpace(singleValue))
    {
        combined.Add(singleValue);
    }

    combined.AddRange(multiValues);
    return combined
        .Where(static value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static bool TryParseBooleanOption(string? value, out bool parsed)
{
    parsed = true;
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    switch (value.Trim().ToLowerInvariant())
    {
        case "true":
        case "yes":
        case "on":
        case "1":
            parsed = true;
            return true;
        case "false":
        case "no":
        case "off":
        case "0":
            parsed = false;
            return true;
        default:
            return false;
    }
}

static bool TryGetNamedValue(IReadOnlyDictionary<string, string> args, string key, out string value)
{
    value = string.Empty;
    if (!args.TryGetValue(key, out var rawValue))
    {
        return false;
    }

    if (string.IsNullOrWhiteSpace(rawValue) || string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    value = rawValue.Trim();
    return true;
}

static void PauseBeforeExit()
{
    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey(intercept: true);
}

static void WriteUsage()
{
    Console.WriteLine($"SlideSmith v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0"} — Bodyslide Armor Converter");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  SlideSmith <armor path> <target body> [output directory]");
    Console.WriteLine("  SlideSmith --input <armor path|folder|archive(.zip/.7z/.tar/.tar.gz/.tgz)> [--target <body|all>] [--targets <body1,body2|all>] [--output <directory>] [--preset <name>] [--presets <preset1,preset2>] [--profile <profile>] [--source <body>] [--physics <auto|none|cbpc|smp|smp+cbpc>] [--world-mode <auto|static|rigid-proxy>] [--build-sliders <true|false>] [--skeleton-nif <path to skeleton.nif|XP32 folder|related .pex>] [--output-zip] [--cache-path <path>]");
    Console.WriteLine("  SlideSmith --list-presets");
    Console.WriteLine("  SlideSmith --list-profiles");
    Console.WriteLine("  SlideSmith --list-bodies");
    Console.WriteLine("  SlideSmith --body-reference <body>");
    Console.WriteLine("  SlideSmith --list-physics");
    Console.WriteLine("  SlideSmith --conversion-guide");
    Console.WriteLine("  SlideSmith --export-cache [--cache-path <path>]");
    Console.WriteLine("  SlideSmith --self-check");
    Console.WriteLine("  SlideSmith --help");
    Console.WriteLine();
    Console.WriteLine("Core options:");
    Console.WriteLine("  --source <body>   FROM body (what the input armor currently targets).");
    Console.WriteLine("  --target <body>   TO body (what you want to convert to).");
    Console.WriteLine("  --preset <name>   Shortcut that sets TO body + default deformation/physics.");
    Console.WriteLine("  --body-reference  Show a focused body profile (tokens, skeleton, soft-body bones, default physics, matching presets).");
    Console.WriteLine("  --conversion-guide  Print practical body/physics/skeleton conversion guidance.");
    Console.WriteLine();
    Console.WriteLine("Drag a .nif file, supported archive (.zip/.7z/.tar/.tar.gz/.tgz), or folder onto SlideSmith.exe, or run it from a command prompt.");
}

static void WriteSelfCheck()
{
    Console.WriteLine("SlideSmith readiness self-check:");
    foreach (var check in RuntimeReadinessReporter.CreateCliReport(Environment.ProcessPath))
    {
        Console.WriteLine($" - [{check.Status}] {check.Area}: {check.Details}");
    }
}

static void WriteBodyReference(string bodyName)
{
    var requested = bodyName.Trim();
    if (requested.Length == 0)
    {
        Console.WriteLine("Provide a body name, for example: --body-reference 3BA");
        return;
    }

    if (!BodyTypeCatalog.TryResolve(requested, out var body))
    {
        Console.WriteLine($"Unknown body '{bodyName}'. Use --list-bodies to view valid names.");
        var suggestions = BodyTypeCatalog.All
            .Where(b => b.Name.Contains(requested, StringComparison.OrdinalIgnoreCase))
            .Select(b => b.Name)
            .ToArray();
        if (suggestions.Length > 0)
        {
            Console.WriteLine($"Closest matches: {string.Join(", ", suggestions)}");
        }
        return;
    }

    Console.WriteLine($"Body reference: {body.Name}");
    Console.WriteLine($" - Detection tokens : {string.Join(", ", body.DetectionTokens)}");
    Console.WriteLine(body.VertexCountMin > 0
        ? $" - Vertex hint range: {body.VertexCountMin}–{body.VertexCountMax}"
        : " - Vertex hint range: n/a");

    if (BodyTechnicalProfileCatalog.TryGet(body.Name, out var profile))
    {
        Console.WriteLine($" - Skeleton base    : {profile.SkeletonFoundation}");
        Console.WriteLine($" - Supports physics : {(profile.SupportsPhysics ? "yes" : "no")}");
        Console.WriteLine($" - Default physics  : {PhysicsProfileCatalog.ToDisplayName(profile.DefaultPhysics)} [{profile.DefaultPhysics}]");
        Console.WriteLine($" - Recommended phys : {PhysicsProfileCatalog.ToDisplayName(profile.RecommendedPhysicsProfile)} [{profile.RecommendedPhysicsProfile}]");
        Console.WriteLine($" - Support regions  : {FormatDisplayList(profile.ExpectedSemanticRegions)}");
        Console.WriteLine($" - Collision focus  : {FormatDisplayList(profile.ExpectedCollisionRegions)} ({profile.CollisionComplexity})");
        Console.WriteLine($" - Bilateral pairs  : {FormatDisplayList(profile.ExpectedBilateralRegions)}");
        Console.WriteLine($" - Min phys cover   : slots {profile.MinimumPhysicsSlotCount}, families {profile.MinimumPhysicsFamilyCount}, chain depth {profile.MinimumPhysicsChainDepth}");
        Console.WriteLine($" - Physics bones    : {(profile.SupportsPhysics ? string.Join(", ", profile.RequiredPhysicsBones) : "none")}");
        if (profile.SupportsPhysics)
        {
            Console.WriteLine($" - Bone groups      : {string.Join(", ", profile.PhysicsBoneGroups.Keys.OrderBy(static k => k, StringComparer.OrdinalIgnoreCase))}");
        }
        Console.WriteLine($" - Notes            : {profile.Notes}");
    }
    else
    {
        Console.WriteLine(" - Skeleton base    : n/a");
        Console.WriteLine(" - Default physics  : n/a");
        Console.WriteLine(" - Soft-body bones  : n/a");
        Console.WriteLine(" - Notes            : n/a");
    }

    var matchingPresets = PresetCatalog.All
        .Where(p => string.Equals(p.TargetBody, body.Name, StringComparison.OrdinalIgnoreCase))
        .Select(p => p.Name)
        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Console.WriteLine($" - Matching presets : {(matchingPresets.Length == 0 ? "none" : string.Join(", ", matchingPresets))}");
}

static string FormatDisplayList(IReadOnlyList<string>? values) =>
    values is { Count: > 0 }
        ? string.Join(", ", values.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
        : "none";

static void WriteConversionGuide()
{
    Console.WriteLine("SlideSmith conversion guide:");
    Console.WriteLine("  1) Pick a destination with --target <body> or --preset <name>.");
    Console.WriteLine("  2) If the source body is known, set --source <body> to improve mapping confidence.");
    Console.WriteLine("  3) Keep --physics auto unless you intentionally need none/cbpc/smp/smp+cbpc.");
    Console.WriteLine("  4) Use --skeleton-nif <path> with your real XPMSSE/target skeleton support for best bone mapping.");
    Console.WriteLine("  5) Use --list-bodies, --body-reference <body>, --list-presets, and --list-physics before converting.");
    Console.WriteLine();
    Console.WriteLine("Recommended command pattern:");
    Console.WriteLine("  SlideSmith --input <armor> --source <known body> --target <destination body> --physics auto --skeleton-nif <path> --output <folder>");
}

static void WritePostConversionGuidance(ConversionResult result)
{
    var qualityReport = TryReadConversionQualityReport(result);
    var targetBody = qualityReport?.TargetBody ?? "target body";
    var validationSummary = qualityReport?.ValidationSummary;
    if (validationSummary is null)
    {
        return;
    }

    Console.WriteLine(
        $"Validation: {ConversionValidationPresentation.GetGateLabel(validationSummary.Status)} " +
        $"(machine status: {validationSummary.Status}; " +
        $"score {validationSummary.Score}; " +
        $"high {validationSummary.HighSeverityCount}, " +
        $"medium {validationSummary.MediumSeverityCount}, " +
        $"low {validationSummary.LowSeverityCount})");
    Console.WriteLine(ConversionValidationPresentation.GetDispositionMessage(validationSummary.Status));

    var prioritizedIssues = ConversionValidationGuidance.PrioritizeIssues(validationSummary, maxIssues: 3);
    if (prioritizedIssues.Count > 0)
    {
        Console.WriteLine("Warnings:");
        foreach (var issue in prioritizedIssues)
        {
            Console.WriteLine($" ! [{issue.Severity.ToUpperInvariant()}] {issue.Message}");
        }
    }
    else if (ConversionValidationPresentation.GetGateRank(validationSummary.Status) ==
             ConversionValidationPresentation.GetGateRank("ready"))
    {
        Console.WriteLine("No immediate follow-up actions detected.");
    }

    var followUpActions = ConversionValidationGuidance.BuildFollowUpActions(
        validationSummary,
        targetBody,
        maxActions: 4);
    if (followUpActions.Count > 0)
    {
        Console.WriteLine("Next actions:");
        foreach (var action in followUpActions)
        {
            Console.WriteLine($" -> {action}");
        }
    }
}

static ConversionQualityReport? TryReadConversionQualityReport(ConversionResult result)
{
    var qualityPath = result.OutputFiles.FirstOrDefault(path =>
        path.EndsWith("conversion-quality.json", StringComparison.OrdinalIgnoreCase))
        ?? Path.Combine(result.OutputDirectory, "conversion-quality.json");

    if (string.IsNullOrWhiteSpace(qualityPath) || !File.Exists(qualityPath))
    {
        return null;
    }

    try
    {
        return JsonSerializer.Deserialize<ConversionQualityReport>(
            File.ReadAllText(qualityPath),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
    }
    catch (JsonException ex)
    {
        Console.Error.WriteLine($"Warning: could not read conversion-quality.json at '{qualityPath}': {ex.Message}");
        return null;
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"Warning: could not read conversion-quality.json at '{qualityPath}': {ex.Message}");
        return null;
    }
    catch (UnauthorizedAccessException ex)
    {
        Console.Error.WriteLine($"Warning: could not read conversion-quality.json at '{qualityPath}': {ex.Message}");
        return null;
    }
}
