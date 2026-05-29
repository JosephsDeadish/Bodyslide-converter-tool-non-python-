using Bodyslide.Core;
using System.Reflection;

var shouldPauseOnExit = ShouldPauseOnExit(args);

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
    Console.WriteLine("Supported body types (signature detection):");
    foreach (var body in BodyTypeCatalog.All)
    {
        var vcRange = body.VertexCountMin > 0
            ? $"  vertices: {body.VertexCountMin}–{body.VertexCountMax}"
            : string.Empty;
        Console.WriteLine($" - {body.Name,-8} tokens: [{string.Join(", ", body.DetectionTokens)}]{vcRange}");
    }
    Console.WriteLine(" - all/any/*  alias: convert to every supported body type");

    return;
}

if (!TryParseRequest(args, out var request, out var error, out var cachePath))
{
    if (!string.IsNullOrEmpty(error))
    {
        Console.WriteLine(error);
    }

    Console.WriteLine($"SlideSmith v{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0"} — Bodyslide Armor Converter");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  SlideSmith <armor path> <target body> [output directory]");
    Console.WriteLine("  SlideSmith --input <armor path|folder|archive(.zip/.tar/.tar.gz/.tgz)> [--target <body|all>] [--targets <body1,body2|all>] [--output <directory>] [--preset <name>] [--presets <preset1,preset2>] [--profile <profile>] [--source <body>] [--output-zip] [--cache-path <path>]");
    Console.WriteLine("  SlideSmith --list-presets");
    Console.WriteLine("  SlideSmith --list-profiles");
    Console.WriteLine("  SlideSmith --list-bodies");
    Console.WriteLine();
    Console.WriteLine("Drag a .nif file, supported archive (.zip/.tar/.tar.gz/.tgz), or folder onto SlideSmith.exe, or run it from a command prompt.");

    if (shouldPauseOnExit)
    {
        PauseBeforeExit();
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
    var outputZip = parsed.ContainsKey("output-zip");
    var selectedTargets = CombineSelections(target, ParseDelimitedValues(targetsValue));
    var selectedPresets = CombineSelections(preset, ParseDelimitedValues(presetsValue));

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

    request = new ConversionRequest(
        InputPath: input,
        TargetBody: selectedTargets.FirstOrDefault() ?? string.Empty,
        OutputDirectory: output,
        Preset: selectedPresets.FirstOrDefault(),
        OutputZip: outputZip,
        DeformationProfile: profile,
        SourceBodyOverride: source,
        TargetBodies: selectedTargets.Count > 1 ? selectedTargets : null,
        Presets: selectedPresets.Count > 1 ? selectedPresets : null);

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

static void PauseBeforeExit()
{
    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey(intercept: true);
}
