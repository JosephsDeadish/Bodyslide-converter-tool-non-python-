using System.Text.Json;

namespace Bodyslide.Core;

public sealed record ConversionRequest(string InputPath, string TargetBody, string? OutputDirectory = null);
public sealed record ImportedArmor(string SourcePath);
public sealed record MeshAnalysis(string MeshType);
public sealed record ConvertedMesh(string MeshType, string Strategy);
public sealed record WeightedMesh(string MeshType, string WeightProfile);
public sealed record MorphSet(string LowMorph, string HighMorph);
public sealed record PhysicsConfig(string Profile);
public sealed record ConversionResult(bool Success, string OutputDirectory, IReadOnlyList<string> Steps);

public interface IArmorImportService
{
    Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken);
}

public interface IBodyDetectionService
{
    Task<string> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken);
}

public interface IMeshAnalysisService
{
    Task<MeshAnalysis> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken);
}

public interface IMeshConversionService
{
    Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken);
}

public interface IWeightTransferService
{
    Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface IMorphGenerationService
{
    Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface IPhysicsSupportService
{
    Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface IExportService
{
    Task<string> ExportAsync(ConversionRequest request, ConvertedMesh mesh, MorphSet morphs, PhysicsConfig physics, IReadOnlyList<string> steps, CancellationToken cancellationToken);
}

public sealed class ConversionOrchestrator(
    IArmorImportService importer,
    IBodyDetectionService bodyDetector,
    IMeshAnalysisService meshAnalyzer,
    IMeshConversionService meshConverter,
    IWeightTransferService weightTransfer,
    IMorphGenerationService morphGenerator,
    IPhysicsSupportService physicsSupport,
    IExportService exporter)
{
    public async Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InputPath))
        {
            throw new ArgumentException("Input path is required.", nameof(request));
        }

        if (!File.Exists(request.InputPath) && !Directory.Exists(request.InputPath))
        {
            throw new FileNotFoundException("Input path was not found.", request.InputPath);
        }

        if (string.IsNullOrWhiteSpace(request.TargetBody))
        {
            throw new ArgumentException("Target body is required.", nameof(request));
        }

        var steps = new List<string>();
        var armor = await importer.ImportAsync(request.InputPath, cancellationToken);
        steps.Add("imported");

        var detectedBody = await bodyDetector.DetectAsync(armor, cancellationToken);
        steps.Add($"detected-body:{detectedBody}");

        var analysis = await meshAnalyzer.AnalyzeAsync(armor, cancellationToken);
        steps.Add($"mesh-type:{analysis.MeshType}");

        var converted = await meshConverter.ConvertAsync(armor, analysis, request.TargetBody, cancellationToken);
        steps.Add($"mesh-converted:{converted.Strategy}");

        var weighted = await weightTransfer.TransferAsync(converted, request.TargetBody, cancellationToken);
        steps.Add($"weights:{weighted.WeightProfile}");

        var morphs = await morphGenerator.GenerateAsync(weighted, request.TargetBody, cancellationToken);
        steps.Add($"morphs:{morphs.LowMorph}/{morphs.HighMorph}");

        var physics = await physicsSupport.BuildAsync(weighted, request.TargetBody, cancellationToken);
        steps.Add($"physics:{physics.Profile}");

        var outputDirectory = await exporter.ExportAsync(request, converted, morphs, physics, steps, cancellationToken);
        steps.Add($"exported:{outputDirectory}");

        return new ConversionResult(true, outputDirectory, steps);
    }
}

public static class StandaloneConversionModules
{
    public static ConversionOrchestrator CreateDefault() =>
        new(
            new LocalArmorImportService(),
            new SignatureBodyDetectionService(),
            new BasicMeshAnalysisService(),
            new StrategyMeshConversionService(),
            new BasicWeightTransferService(),
            new BasicMorphGenerationService(),
            new BasicPhysicsSupportService(),
            new LocalExportService());
}

internal sealed class LocalArmorImportService : IArmorImportService
{
    public Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken) =>
        Task.FromResult(new ImportedArmor(Path.GetFullPath(inputPath)));
}

internal sealed class SignatureBodyDetectionService : IBodyDetectionService
{
    private static readonly string[] KnownBodies = ["CBBE", "UNP", "HIMBO", "BHUNP", "3BA", "TBD"];

    public Task<string> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var source = armor.SourcePath.ToUpperInvariant();
        var detected = KnownBodies.FirstOrDefault(source.Contains) ?? "CUSTOM";
        return Task.FromResult(detected);
    }
}

internal sealed class BasicMeshAnalysisService : IMeshAnalysisService
{
    public Task<MeshAnalysis> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(armor.SourcePath).ToLowerInvariant();
        var meshType = fileName.Contains("plate") ? "plate" : fileName.Contains("cloth") ? "cloth" : "mixed";
        return Task.FromResult(new MeshAnalysis(meshType));
    }
}

internal sealed class StrategyMeshConversionService : IMeshConversionService
{
    public Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken)
    {
        var strategy = analysis.MeshType switch
        {
            "cloth" => "shrinkwrap+smooth-projection",
            "plate" => "rigid-islands+normal-preservation",
            _ => "hybrid-cage-deformation"
        };

        return Task.FromResult(new ConvertedMesh(analysis.MeshType, strategy));
    }
}

internal sealed class BasicWeightTransferService : IWeightTransferService
{
    public Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
        Task.FromResult(new WeightedMesh(mesh.MeshType, "nearest-triangle+smoothed"));
}

internal sealed class BasicMorphGenerationService : IMorphGenerationService
{
    public Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
        Task.FromResult(new MorphSet("low-weight", "high-weight"));
}

internal sealed class BasicPhysicsSupportService : IPhysicsSupportService
{
    public Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
        Task.FromResult(new PhysicsConfig("smp+cbpc"));
}

internal sealed class LocalExportService : IExportService
{
    public async Task<string> ExportAsync(ConversionRequest request, ConvertedMesh mesh, MorphSet morphs, PhysicsConfig physics, IReadOnlyList<string> steps, CancellationToken cancellationToken)
    {
        var outputDirectory = Path.GetFullPath(request.OutputDirectory ?? Path.Combine(Environment.CurrentDirectory, "output", request.TargetBody));
        Directory.CreateDirectory(outputDirectory);

        var manifest = new
        {
            request.InputPath,
            request.TargetBody,
            mesh.MeshType,
            mesh.Strategy,
            Morphs = morphs,
            Physics = physics,
            Steps = steps
        };

        var manifestPath = Path.Combine(outputDirectory, "conversion-manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return outputDirectory;
    }
}
