using Bodyslide.Core;

if (args.Length < 2)
{
    Console.WriteLine("Usage: Bodyslide.Standalone <armor path> <target body> [output directory]");
    return;
}

var request = new ConversionRequest(args[0], args[1], args.Length > 2 ? args[2] : null);
var orchestrator = StandaloneConversionModules.CreateDefault();
var result = await orchestrator.ConvertAsync(request);

Console.WriteLine($"Conversion completed. Output: {result.OutputDirectory}");
foreach (var step in result.Steps)
{
    Console.WriteLine($" - {step}");
}
