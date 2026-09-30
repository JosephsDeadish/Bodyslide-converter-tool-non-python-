using Bodyslide.Core;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

ExecutionEnvironment.TryNormalizeCurrentDirectoryToExecutionRoot(
    Environment.ProcessPath,
    AppContext.BaseDirectory);

var shouldPauseOnExit = ShouldPauseOnExit(args);
var strictLauncherMode = IsStrictLauncherModeEnabled(args);
var startupDiagnosticsPath = ResolveStartupDiagnosticsPath(args);
WriteStartupDiagnostics(
    startupDiagnosticsPath,
    $"startup: strict-launcher-mode={strictLauncherMode}, exe={Environment.ProcessPath ?? "(unknown)"}, cwd={Environment.CurrentDirectory}, args=[{string.Join(", ", args)}]");

if (TryLaunchDesktopGuiOnWindows(args, startupDiagnosticsPath, strictLauncherMode))
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

    if (args.Length >= 2 &&
        !args[0].StartsWith("--", StringComparison.Ordinal) &&
        !StandaloneStartupRouting.IsLikelyLauncherPathArgument(args[0]))
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

static bool TryLaunchDesktopGuiOnWindows(string[] args, string? startupDiagnosticsPath, bool strictLauncherMode)
{
    if (!OperatingSystem.IsWindows())
    {
        return false;
    }

    const int ModManagerLaunchEarlyExitWaitMs = 1500;

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
        var launchDecision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            args,
            currentExeFullPath,
            Environment.CurrentDirectory,
            strictLauncherMode: strictLauncherMode);
        var explicitCliLaunch = launchDecision.ExplicitCliLaunchDetected;
        var launcherSignal = launchDecision.LauncherSignalDetected;
        var launchedFromModOrganizer = launchDecision.ModManagerLaunchDetected;
        var decisionDiagnosticsPath = ResolveLauncherDecisionDiagnosticsPath(args, startupDiagnosticsPath, strictLauncherMode);

        bool TryContinueAfterEarlyExit(Process? launchedProcess, string candidateKind, string candidatePath)
        {
            if (!launchedFromModOrganizer || launchedProcess is null)
            {
                return false;
            }

            try
            {
                if (launchedProcess.WaitForExit(ModManagerLaunchEarlyExitWaitMs) && launchedProcess.ExitCode != 0)
                {
                    WriteStartupDiagnostics(startupDiagnosticsPath, $"desktop-launch: {candidateKind} exited early with code {launchedProcess.ExitCode}: {candidatePath}");
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
            }

            return false;
        }

        WriteStartupDiagnostics(
            startupDiagnosticsPath,
            $"desktop-launch: launcherSignal={launcherSignal}, mo2={launchedFromModOrganizer}, cli={explicitCliLaunch}, exe={currentExeFullPath}");
        WriteStartupDiagnostics(
            startupDiagnosticsPath,
            $"desktop-launch: parsed-args {SummarizeLaunchArguments(args)}");
        if (!launchDecision.ShouldAttemptDesktopHandoff)
        {
            WriteStartupDiagnostics(startupDiagnosticsPath, "desktop-launch: skipped (non-launcher invocation)");
            WriteLauncherDecisionDiagnostics(
                decisionDiagnosticsPath,
                launchDecision,
                selectedMode: "cli",
                currentExeFullPath,
                Environment.CurrentDirectory,
                args);
            return false;
        }

        var desktopCandidateDirectories = DesktopLaunchPathResolver.GetLikelyDesktopCandidateDirectories(executableDirectory);
        foreach (var desktopExePath in EnumerateDesktopExeCandidates(desktopCandidateDirectories))
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
                if (TryContinueAfterEarlyExit(launched, "candidate", desktopExePath))
                {
                    continue;
                }

                WriteLauncherDecisionDiagnostics(
                    decisionDiagnosticsPath,
                    launchDecision,
                    selectedMode: "desktop",
                    currentExeFullPath,
                    Environment.CurrentDirectory,
                    args);
                return true;
            }
        }

        foreach (var desktopDllPath in EnumerateDesktopDllCandidates(desktopCandidateDirectories))
        {
            if (!File.Exists(desktopDllPath))
            {
                continue;
            }

            if (TryStartDesktopDllProcess(desktopDllPath, executableDirectory, launchedFromModOrganizer, args, startupDiagnosticsPath, out var launched))
            {
                if (TryContinueAfterEarlyExit(launched, "dll candidate", desktopDllPath))
                {
                    continue;
                }

                WriteLauncherDecisionDiagnostics(
                    decisionDiagnosticsPath,
                    launchDecision,
                    selectedMode: "desktop",
                    currentExeFullPath,
                    Environment.CurrentDirectory,
                    args);
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
    WriteLauncherDecisionDiagnostics(
        ResolveLauncherDecisionDiagnosticsPath(args, startupDiagnosticsPath, strictLauncherMode),
        StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            args,
            Environment.ProcessPath,
            Environment.CurrentDirectory,
            strictLauncherMode: strictLauncherMode),
        selectedMode: "cli",
        Environment.ProcessPath,
        Environment.CurrentDirectory,
        args);
    return false;
}

static IReadOnlyList<string> EnumerateDesktopExeCandidates(IReadOnlyList<string> candidateDirectories)
{
    var preferred = new[]
    {
        Path.Combine(candidateDirectories[0], "SlideSmith.exe"),
        Path.Combine(candidateDirectories[0], "SlideSmith.Desktop.exe"),
        Path.Combine(candidateDirectories[0], "Bodyslide.Desktop.exe"),
        Path.Combine(candidateDirectories[0], "SlideSmith-Desktop.exe")
    };

    var discovered = candidateDirectories
        .SelectMany(static directory => EnumerateDirectoryCandidates(directory, "*.exe"))
        .Where(IsDesktopExecutableCandidate);

    return preferred
        .Concat(discovered)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static IReadOnlyList<string> EnumerateDesktopDllCandidates(IReadOnlyList<string> candidateDirectories)
{
    var preferred = new[]
    {
        Path.Combine(candidateDirectories[0], "SlideSmith.Desktop.dll"),
        Path.Combine(candidateDirectories[0], "Bodyslide.Desktop.dll"),
        Path.Combine(candidateDirectories[0], "SlideSmith.dll")
    };

    var discovered = candidateDirectories
        .SelectMany(static directory => EnumerateDirectoryCandidates(directory, "*.dll"))
        .Where(IsDesktopDllCandidate);

    return preferred
        .Concat(discovered)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static bool IsDesktopExecutableCandidate(string path)
{
    var fileName = Path.GetFileName(path);
    if (string.IsNullOrWhiteSpace(fileName))
    {
        return false;
    }

    if (fileName.Contains("desktop", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return fileName.Equals("SlideSmith.exe", StringComparison.OrdinalIgnoreCase) &&
           Path.GetFileName(Path.GetDirectoryName(path))?.Equals("desktop", StringComparison.OrdinalIgnoreCase) == true;
}

static bool IsDesktopDllCandidate(string path)
{
    var fileName = Path.GetFileName(path);
    if (string.IsNullOrWhiteSpace(fileName))
    {
        return false;
    }

    if (fileName.Contains("desktop", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return fileName.Equals("SlideSmith.dll", StringComparison.OrdinalIgnoreCase) &&
           Path.GetFileName(Path.GetDirectoryName(path))?.Equals("desktop", StringComparison.OrdinalIgnoreCase) == true;
}

static IReadOnlyList<string> EnumerateDirectoryCandidates(string directory, string pattern)
{
    if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
    {
        return [];
    }

    try
    {
        return Directory
            .EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .ToArray();
    }
    catch
    {
        return [];
    }
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
            if (!launchedFromModOrganizer)
            {
                launchedProcess = null;
                return false;
            }
        }
        launchedProcess = null;
        return false;
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
        WriteStartupDiagnostics(
            startupDiagnosticsPath,
            $"desktop-launch: handoff-args[{startInfo.ArgumentList.Count}]={FormatArgumentList(startInfo.ArgumentList)}");
        return;
    }

    var alreadyTagged = forwardedArgs.Any(StandaloneStartupRouting.IsModManagerLauncherArgument);
    if (!alreadyTagged)
    {
        startInfo.ArgumentList.Add("--mo2-launcher");
    }

    WriteStartupDiagnostics(
        startupDiagnosticsPath,
        $"desktop-launch: handoff-args[{startInfo.ArgumentList.Count}]={FormatArgumentList(startInfo.ArgumentList)}");
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

static bool IsStrictLauncherModeEnabled(IReadOnlyList<string> args)
{
    if (TryReadStandaloneBooleanOption(args, "strict-launcher-mode", out var parsed))
    {
        return parsed;
    }

    return IsStandaloneDiagnosticsFlagEnabled("SLIDESMITH_STRICT_LAUNCHER_MODE");
}

static bool TryReadStandaloneBooleanOption(IReadOnlyList<string> args, string optionName, out bool parsed)
{
    parsed = false;
    for (var index = 0; index < args.Count; index++)
    {
        var arg = args[index];
        if (string.IsNullOrWhiteSpace(arg))
        {
            continue;
        }

        if (arg.StartsWith($"--{optionName}=", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith($"--{optionName}:", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = arg.IndexOfAny(['=', ':']);
            if (separatorIndex >= 0 && separatorIndex < arg.Length - 1)
            {
                return TryParseBooleanOption(arg[(separatorIndex + 1)..], out parsed);
            }

            parsed = true;
            return true;
        }

        if (!IsStandaloneOptionMatch(arg, optionName))
        {
            continue;
        }

        if (index + 1 < args.Count &&
            !TryReadLongOptionName(args[index + 1], out _) &&
            TryParseBooleanOption(args[index + 1], out parsed))
        {
            return true;
        }

        parsed = true;
        return true;
    }

    return false;
}

static string? ResolveLauncherDecisionDiagnosticsPath(
    IReadOnlyList<string> args,
    string? startupDiagnosticsPath,
    bool strictLauncherMode)
{
    for (var index = 0; index < args.Count; index++)
    {
        var arg = args[index];
        if (string.IsNullOrWhiteSpace(arg))
        {
            continue;
        }

        if (arg.StartsWith("--launcher-decision-diagnostics=", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeDiagnosticsPath(arg["--launcher-decision-diagnostics=".Length..]);
        }

        if (!IsStandaloneOptionMatch(arg, "launcher-decision-diagnostics"))
        {
            continue;
        }

        if (index + 1 < args.Count && !TryReadLongOptionName(args[index + 1], out _))
        {
            return NormalizeDiagnosticsPath(args[index + 1]);
        }
    }

    var fromEnvironment = NormalizeDiagnosticsPath(Environment.GetEnvironmentVariable("SLIDESMITH_LAUNCHER_DECISION_DIAGNOSTICS"));
    if (!string.IsNullOrWhiteSpace(fromEnvironment))
    {
        return fromEnvironment;
    }

    if (!strictLauncherMode)
    {
        return null;
    }

    if (!string.IsNullOrWhiteSpace(startupDiagnosticsPath))
    {
        return Path.ChangeExtension(startupDiagnosticsPath, ".decision.json");
    }

    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (!string.IsNullOrWhiteSpace(localAppData))
    {
        return Path.Combine(localAppData, "SlideSmith", "startup-launch-decision.json");
    }

    return Path.Combine(Path.GetTempPath(), "SlideSmith", "startup-launch-decision.json");
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
    return IsStandaloneDiagnosticsFlagEnabled("SLIDESMITH_STARTUP_DIAGNOSTICS");
}

static bool IsStandaloneDiagnosticsFlagEnabled(string variableName)
{
    var flag = Environment.GetEnvironmentVariable(variableName);
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

static void WriteLauncherDecisionDiagnostics(
    string? diagnosticsPath,
    StandaloneDesktopLaunchDecision decision,
    string selectedMode,
    string? executablePath,
    string? workingDirectory,
    IReadOnlyList<string> args)
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

        var payload = new
        {
            SelectedMode = selectedMode,
            decision.ShouldAttemptDesktopHandoff,
            decision.LauncherSignalDetected,
            decision.ModManagerLaunchDetected,
            decision.ExplicitCliLaunchDetected,
            decision.StrictLauncherModeEnabled,
            decision.RoutingReason,
            ExecutablePath = executablePath,
            WorkingDirectory = workingDirectory,
            ArgumentSummary = SummarizeLaunchArguments(args),
            Arguments = args,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
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

static string SummarizeLaunchArguments(IReadOnlyList<string> args)
{
    if (args.Count == 0)
    {
        return "none";
    }

    var modManagerSwitches = args.Count(StandaloneStartupRouting.IsModManagerLauncherArgument);
    var launcherPathTokens = args.Count(StandaloneStartupRouting.IsLikelyLauncherPathArgument);
    var startupDiagnosticsSwitches = args.Count(static arg => IsStandaloneOptionMatch(arg, "startup-diagnostics"));
    var quotedTokens = args.Count(static arg => !string.IsNullOrWhiteSpace(arg) && arg.Contains('"'));
    return $"count={args.Count}, mod-manager-switches={modManagerSwitches}, launcher-path-tokens={launcherPathTokens}, startup-diagnostics-switches={startupDiagnosticsSwitches}, quoted={quotedTokens}";
}

static string FormatArgumentList(System.Collections.Generic.IReadOnlyList<string> args)
{
    if (args.Count == 0)
    {
        return "(none)";
    }

    return string.Join(", ", args.Select(static value => $"\"{value}\""));
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
