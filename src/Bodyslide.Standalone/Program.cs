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

if (!TryParseRequest(args, out var request, out var error))
{
    Console.WriteLine(error);
    Console.WriteLine("Usage:");
    Console.WriteLine("  Bodyslide.Standalone <armor path> <target body> [output directory]");
    Console.WriteLine("  Bodyslide.Standalone --input <armor path> [--target <body>] [--output <directory>] [--preset <name>]");
    Console.WriteLine("  Bodyslide.Standalone --list-presets");
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

    request = new ConversionRequest(input, target ?? string.Empty, output, preset);
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
