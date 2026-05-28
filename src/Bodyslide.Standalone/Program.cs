using Bodyslide.Core;

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

    return;
}

if (!TryParseRequest(args, out var request, out var error))
{
    if (!string.IsNullOrEmpty(error))
    {
        Console.WriteLine(error);
    }

    Console.WriteLine("SlideSmith v0.1 — Bodyslide Armor Converter");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  SlideSmith <armor path> <target body> [output directory]");
    Console.WriteLine("  SlideSmith --input <armor path|folder|zip> [--target <body>] [--output <directory>] [--preset <name>] [--profile <profile>] [--source <body>] [--output-zip]");
    Console.WriteLine("  SlideSmith --list-presets");
    Console.WriteLine("  SlideSmith --list-profiles");
    Console.WriteLine("  SlideSmith --list-bodies");
    Console.WriteLine();
    Console.WriteLine("Drag a .nif file or folder onto SlideSmith.exe, or run it from a command prompt.");

    if (args.Length == 0)
    {
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey(intercept: true);
    }

    return;
}

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

static bool TryParseRequest(string[] args, out ConversionRequest request, out string error)
{
    request = default!;
    error = string.Empty;

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
    parsed.TryGetValue("profile", out var profile);
    parsed.TryGetValue("source", out var source);
    var outputZip = parsed.ContainsKey("output-zip");

    if (string.IsNullOrWhiteSpace(input))
    {
        error = "Missing required --input value.";
        return false;
    }

    if (string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(preset))
    {
        error = "Provide --target or --preset.";
        return false;
    }

    request = new ConversionRequest(input, target ?? string.Empty, output, preset, outputZip, profile, source);
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
