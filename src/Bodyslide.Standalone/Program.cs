using Bodyslide.Core;
using System.Diagnostics;
using System.Reflection;

var shouldPauseOnExit = ShouldPauseOnExit(args);

if (TryLaunchDesktopGuiOnWindows(args))
{
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
    Console.WriteLine("Alias accepted: soft-body => smp+cbpc");
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
        if (!skeletonNif.EndsWith(".nif", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Invalid --skeleton-nif value '{skeletonNif}'. Provide a path to a .nif file.";
            return false;
        }

        if (!File.Exists(skeletonNif))
        {
            error = $"Could not find skeleton file '{skeletonNif}'. Check the path and try again.";
            return false;
        }
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

static bool TryLaunchDesktopGuiOnWindows(string[] args)
{
    if (args.Length != 0 || !OperatingSystem.IsWindows())
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
        foreach (var desktopExePath in new[]
                 {
                     Path.Combine(executableDirectory, "SlideSmith.exe"),
                     Path.Combine(executableDirectory, "SlideSmith-Desktop.exe")
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

            Process.Start(new ProcessStartInfo
            {
                FileName = desktopExePath,
                WorkingDirectory = executableDirectory,
                UseShellExecute = true
            });

            return true;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Could not auto-launch desktop GUI: {ex.Message}");
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
    Console.WriteLine("  SlideSmith --input <armor path|folder|archive(.zip/.7z/.tar/.tar.gz/.tgz)> [--target <body|all>] [--targets <body1,body2|all>] [--output <directory>] [--preset <name>] [--presets <preset1,preset2>] [--profile <profile>] [--source <body>] [--physics <auto|none|cbpc|smp|smp+cbpc>] [--world-mode <auto|static|rigid-proxy>] [--build-sliders <true|false>] [--skeleton-nif <path to skeleton.nif>] [--output-zip] [--cache-path <path>]");
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

    var body = BodyTypeCatalog.All.FirstOrDefault(b => string.Equals(b.Name, requested, StringComparison.OrdinalIgnoreCase));
    if (body is null)
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
        Console.WriteLine($" - Physics bones    : {(profile.SupportsPhysics ? string.Join(", ", profile.RequiredPhysicsBones) : "none")}");
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

static void WriteConversionGuide()
{
    Console.WriteLine("SlideSmith conversion guide:");
    Console.WriteLine("  1) Pick a destination with --target <body> or --preset <name>.");
    Console.WriteLine("  2) If the source body is known, set --source <body> to improve mapping confidence.");
    Console.WriteLine("  3) Keep --physics auto unless you intentionally need none/cbpc/smp/smp+cbpc.");
    Console.WriteLine("  4) Use --skeleton-nif <path> with your real XPMSSE/target skeleton for best bone mapping.");
    Console.WriteLine("  5) Use --list-bodies, --body-reference <body>, --list-presets, and --list-physics before converting.");
    Console.WriteLine();
    Console.WriteLine("Recommended command pattern:");
    Console.WriteLine("  SlideSmith --input <armor> --source <known body> --target <destination body> --physics auto --skeleton-nif <path> --output <folder>");
}
