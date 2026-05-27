using System.IO.Compression;
using System.Text.Json;

namespace Bodyslide.Core;

public sealed record ConversionRequest(string InputPath, string TargetBody, string? OutputDirectory = null, string? Preset = null);
public sealed record ConversionPreset(string Name, string TargetBody, string DeformationProfile, string PhysicsProfile);
public sealed record ImportedArmor(string SourcePath, IReadOnlyList<string> MeshFiles, IReadOnlyList<string> TextureFiles, IReadOnlyList<string> PhysicsFiles, string? TemporaryWorkspace = null);
public sealed record MeshAnalysis(string MeshType, bool PhysicsEnabled, int MeshCount);
public sealed record DeformationCage(string Mode);
public sealed record ConvertedMesh(string MeshType, string Strategy, int MeshCount);
public sealed record WeightedMesh(string MeshType, string WeightProfile, bool PhysicsWeightsTransferred);
public sealed record MorphSet(string LowMorph, string HighMorph, bool BodySlideCompatible);
public sealed record ClippingReport(bool HasClipping, IReadOnlyList<string> Regions);
public sealed record CorrectionResult(bool Applied, string Method);
public sealed record PhysicsConfig(string Profile);
public sealed record ConversionResult(bool Success, string OutputDirectory, IReadOnlyList<string> Steps, IReadOnlyList<string> OutputFiles);

public static class PresetCatalog
{
    private static readonly Dictionary<string, ConversionPreset> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["3BA Curvy"] = new("3BA Curvy", "3BA", "curvy", "smp+cbpc"),
        ["HIMBO Lean"] = new("HIMBO Lean", "HIMBO", "lean", "smp"),
        ["UNP Petite"] = new("UNP Petite", "UNP", "petite", "cbpc")
    };

    public static IReadOnlyCollection<ConversionPreset> All => Presets.Values;

    public static bool TryGet(string presetName, out ConversionPreset preset) =>
        Presets.TryGetValue(presetName, out preset!);
}

public static class RequestNormalizer
{
    public static (ConversionRequest Request, ConversionPreset? Preset) Normalize(ConversionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Preset) && PresetCatalog.TryGet(request.Preset, out var preset))
        {
            return (request with { TargetBody = preset.TargetBody }, preset);
        }

        return (request, null);
    }
}

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

public interface ICageGenerationService
{
    Task<DeformationCage> BuildAsync(MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken);
}

public interface IMeshConversionService
{
    Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, CancellationToken cancellationToken);
}

public interface IWeightTransferService
{
    Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken);
}

public interface IMorphGenerationService
{
    Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface IClippingDetectionService
{
    Task<ClippingReport> DetectAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface IAutoCorrectionService
{
    Task<CorrectionResult> CorrectAsync(ConvertedMesh mesh, ClippingReport clipping, CancellationToken cancellationToken);
}

public interface IPhysicsSupportService
{
    Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, string physicsProfile, CancellationToken cancellationToken);
}

public interface IExportService
{
    Task<(string OutputDirectory, IReadOnlyList<string> OutputFiles)> ExportAsync(
        ConversionRequest request,
        ImportedArmor armor,
        MeshAnalysis analysis,
        ConvertedMesh mesh,
        MorphSet morphs,
        PhysicsConfig physics,
        ClippingReport clipping,
        CorrectionResult correction,
        IReadOnlyList<string> steps,
        CancellationToken cancellationToken);
}

public sealed class ConversionOrchestrator(
    IArmorImportService importer,
    IBodyDetectionService bodyDetector,
    IMeshAnalysisService meshAnalyzer,
    ICageGenerationService cageGenerator,
    IMeshConversionService meshConverter,
    IWeightTransferService weightTransfer,
    IMorphGenerationService morphGenerator,
    IClippingDetectionService clippingDetector,
    IAutoCorrectionService autoCorrection,
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

        var normalized = RequestNormalizer.Normalize(request);
        if (string.IsNullOrWhiteSpace(normalized.Request.TargetBody))
        {
            throw new ArgumentException("Target body or preset is required.", nameof(request));
        }

        var steps = new List<string>();
        ImportedArmor? armor = null;

        try
        {
            armor = await importer.ImportAsync(normalized.Request.InputPath, cancellationToken);
            steps.Add($"imported:meshes={armor.MeshFiles.Count},textures={armor.TextureFiles.Count},physics={armor.PhysicsFiles.Count}");

            var detectedBody = await bodyDetector.DetectAsync(armor, cancellationToken);
            steps.Add($"detected-body:{detectedBody}");

            var analysis = await meshAnalyzer.AnalyzeAsync(armor, cancellationToken);
            steps.Add($"mesh-type:{analysis.MeshType}");

            var cage = await cageGenerator.BuildAsync(analysis, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"cage:{cage.Mode}");

            var converted = await meshConverter.ConvertAsync(armor, analysis, cage, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"mesh-converted:{converted.Strategy}");

            var weighted = await weightTransfer.TransferAsync(converted, analysis, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"weights:{weighted.WeightProfile}");

            var morphs = await morphGenerator.GenerateAsync(weighted, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"morphs:{morphs.LowMorph}/{morphs.HighMorph}");

            var clipping = await clippingDetector.DetectAsync(converted, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"clipping:{(clipping.HasClipping ? "detected" : "none")}");

            var correction = await autoCorrection.CorrectAsync(converted, clipping, cancellationToken);
            steps.Add($"correction:{(correction.Applied ? correction.Method : "not-required")}");

            var physicsProfile = normalized.Preset?.PhysicsProfile ?? "smp+cbpc";
            var physics = await physicsSupport.BuildAsync(weighted, normalized.Request.TargetBody, physicsProfile, cancellationToken);
            steps.Add($"physics:{physics.Profile}");

            var export = await exporter.ExportAsync(normalized.Request, armor, analysis, converted, morphs, physics, clipping, correction, steps, cancellationToken);
            steps.Add($"exported:{export.OutputDirectory}");

            return new ConversionResult(true, export.OutputDirectory, steps, export.OutputFiles);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(armor?.TemporaryWorkspace) && Directory.Exists(armor.TemporaryWorkspace))
            {
                Directory.Delete(armor.TemporaryWorkspace, recursive: true);
            }
        }
    }
}

public sealed class BatchConversionRunner(ConversionOrchestrator orchestrator)
{
    public async Task<IReadOnlyList<ConversionResult>> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default)
    {
        if (File.Exists(request.InputPath) || !Directory.Exists(request.InputPath))
        {
            return [await orchestrator.ConvertAsync(request, cancellationToken)];
        }

        var meshFiles = Directory.GetFiles(request.InputPath, "*.nif", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (meshFiles.Count == 0)
        {
            throw new InvalidDataException($"No .nif files were found in '{request.InputPath}'.");
        }

        var rootOutput = request.OutputDirectory ??
            Path.Combine(Environment.CurrentDirectory, "output", request.TargetBody, "batch");

        var results = new List<ConversionResult>(meshFiles.Count);
        foreach (var meshFile in meshFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var perArmorOutput = Path.Combine(rootOutput, Path.GetFileNameWithoutExtension(meshFile));
            var perArmorRequest = request with { InputPath = meshFile, OutputDirectory = perArmorOutput };
            results.Add(await orchestrator.ConvertAsync(perArmorRequest, cancellationToken));
        }

        return results;
    }
}

public static class StandaloneConversionModules
{
    public static ConversionOrchestrator CreateDefault() =>
        new(
            new LocalArmorImportService(),
            new SignatureBodyDetectionService(),
            new BasicMeshAnalysisService(),
            new BasicCageGenerationService(),
            new StrategyMeshConversionService(),
            new BasicWeightTransferService(),
            new BasicMorphGenerationService(),
            new BasicClippingDetectionService(),
            new BasicAutoCorrectionService(),
            new BasicPhysicsSupportService(),
            new LocalExportService());
}

internal sealed class LocalArmorImportService : IArmorImportService
{
    public Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        var sourcePath = fullInputPath;
        string? temporaryWorkspace = null;

        if (File.Exists(fullInputPath) && Path.GetExtension(fullInputPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            temporaryWorkspace = Path.Combine(Path.GetTempPath(), "bodyslide-extract", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryWorkspace);
            ZipFile.ExtractToDirectory(fullInputPath, temporaryWorkspace);
            sourcePath = temporaryWorkspace;
        }

        var meshFiles = EnumerateFiles(sourcePath, [".nif"]);
        if (meshFiles.Count == 0)
        {
            throw new InvalidDataException("No .nif mesh files were found in the input.");
        }

        var textureFiles = EnumerateFiles(sourcePath, [".dds", ".png", ".tga"]);
        var physicsFiles = EnumerateFiles(sourcePath, [".xml", ".hkx"]);

        return Task.FromResult(new ImportedArmor(sourcePath, meshFiles, textureFiles, physicsFiles, temporaryWorkspace));
    }

    private static IReadOnlyList<string> EnumerateFiles(string path, IReadOnlyCollection<string> extensions)
    {
        if (File.Exists(path))
        {
            return extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                ? [Path.GetFullPath(path)]
                : [];
        }

        return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

internal sealed class SignatureBodyDetectionService : IBodyDetectionService
{
    private static readonly string[] KnownBodies = ["CBBE", "UNP", "HIMBO", "BHUNP", "3BA", "TBD"];

    public Task<string> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var signatures = string.Join(' ', armor.MeshFiles.Select(Path.GetFileNameWithoutExtension)).ToUpperInvariant();
        var detected = KnownBodies.FirstOrDefault(signatures.Contains) ?? "CUSTOM";
        return Task.FromResult(detected);
    }
}

internal sealed class BasicMeshAnalysisService : IMeshAnalysisService
{
    public Task<MeshAnalysis> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var fileNames = armor.MeshFiles.Select(path => Path.GetFileNameWithoutExtension(path).ToLowerInvariant()).ToList();

        var meshType = fileNames.Any(name => name.Contains("plate") || name.Contains("cuirass") || name.Contains("pauldron")) ? "plate" :
            fileNames.Any(name => name.Contains("leather") || name.Contains("hide")) ? "leather" :
            fileNames.Any(name => name.Contains("cloth") || name.Contains("robe") || name.Contains("skirt")) ? "cloth" :
            fileNames.Any(name => name.Contains("tight") || name.Contains("bodysuit") || name.Contains("catsuit")) ? "skin-tight" :
            "mixed";

        var physicsEnabled = armor.PhysicsFiles.Count > 0 ||
            fileNames.Any(name => name.Contains("smp") || name.Contains("cbpc"));

        return Task.FromResult(new MeshAnalysis(meshType, physicsEnabled, armor.MeshFiles.Count));
    }
}

internal sealed class BasicCageGenerationService : ICageGenerationService
{
    public Task<DeformationCage> BuildAsync(MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken)
    {
        var mode = analysis.MeshType switch
        {
            "plate" => "rigid-regional-cage",
            "cloth" => "smooth-adaptive-cage",
            "skin-tight" => "body-field-cage",
            _ => "hybrid-cage"
        };

        return Task.FromResult(new DeformationCage(mode));
    }
}

internal sealed class StrategyMeshConversionService : IMeshConversionService
{
    public Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, CancellationToken cancellationToken)
    {
        var strategy = analysis.MeshType switch
        {
            "cloth" => "cage+shrinkwrap+curvature-preserve",
            "plate" => "cage+rigid-islands+normal-preservation",
            "leather" => "cage+local-cluster-smoothing",
            "skin-tight" => "body-transform-field",
            _ => "hybrid-cage-deformation"
        };

        return Task.FromResult(new ConvertedMesh(analysis.MeshType, strategy, analysis.MeshCount));
    }
}

internal sealed class BasicWeightTransferService : IWeightTransferService
{
    public Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken)
    {
        var profile = analysis.PhysicsEnabled
            ? "nearest-triangle+heatmap+normalized-smoothing+physics-weights"
            : "nearest-triangle+heatmap+normalized-smoothing";

        return Task.FromResult(new WeightedMesh(mesh.MeshType, profile, analysis.PhysicsEnabled));
    }
}

internal sealed class BasicMorphGenerationService : IMorphGenerationService
{
    public Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
        Task.FromResult(new MorphSet("low-weight", "high-weight", true));
}

internal sealed class BasicClippingDetectionService : IClippingDetectionService
{
    public Task<ClippingReport> DetectAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken)
    {
        var riskRegions = mesh.MeshType switch
        {
            "plate" => new[] { "shoulders", "armpits" },
            "cloth" => new[] { "thighs", "butt" },
            "skin-tight" => new[] { "breasts", "thighs", "butt" },
            _ => new[] { "armpits", "thighs" }
        };

        var hasClipping = mesh.MeshType is "cloth" or "skin-tight";
        return Task.FromResult(new ClippingReport(hasClipping, riskRegions));
    }
}

internal sealed class BasicAutoCorrectionService : IAutoCorrectionService
{
    public Task<CorrectionResult> CorrectAsync(ConvertedMesh mesh, ClippingReport clipping, CancellationToken cancellationToken)
    {
        if (!clipping.HasClipping)
        {
            return Task.FromResult(new CorrectionResult(false, "none"));
        }

        return Task.FromResult(new CorrectionResult(true, "local-inflation+adaptive-normal-offset"));
    }
}

internal sealed class BasicPhysicsSupportService : IPhysicsSupportService
{
    public Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, string physicsProfile, CancellationToken cancellationToken) =>
        Task.FromResult(new PhysicsConfig(physicsProfile));
}

internal sealed class LocalExportService : IExportService
{
    public async Task<(string OutputDirectory, IReadOnlyList<string> OutputFiles)> ExportAsync(
        ConversionRequest request,
        ImportedArmor armor,
        MeshAnalysis analysis,
        ConvertedMesh mesh,
        MorphSet morphs,
        PhysicsConfig physics,
        ClippingReport clipping,
        CorrectionResult correction,
        IReadOnlyList<string> steps,
        CancellationToken cancellationToken)
    {
        var defaultOutput = Path.Combine(Environment.CurrentDirectory, "output", request.TargetBody, Path.GetFileNameWithoutExtension(armor.MeshFiles[0]));
        var outputDirectory = Path.GetFullPath(request.OutputDirectory ?? defaultOutput);
        Directory.CreateDirectory(outputDirectory);

        var outputFiles = new List<string>();

        var manifest = new
        {
            Request = request,
            Imported = new
            {
                armor.SourcePath,
                MeshCount = armor.MeshFiles.Count,
                TextureCount = armor.TextureFiles.Count,
                PhysicsCount = armor.PhysicsFiles.Count
            },
            Analysis = analysis,
            Converted = mesh,
            Morphs = morphs,
            Physics = physics,
            Clipping = clipping,
            Correction = correction,
            Steps = steps
        };

        var manifestPath = Path.Combine(outputDirectory, "conversion-manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(manifestPath);

        var convertedMeshListPath = Path.Combine(outputDirectory, "converted-meshes.txt");
        await File.WriteAllLinesAsync(
            convertedMeshListPath,
            armor.MeshFiles.Select(file => Path.GetFileName(file) ?? file),
            cancellationToken);
        outputFiles.Add(convertedMeshListPath);

        var morphPath = Path.Combine(outputDirectory, "morphs.json");
        await File.WriteAllTextAsync(morphPath, JsonSerializer.Serialize(morphs, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(morphPath);

        var physicsPath = Path.Combine(outputDirectory, "physics.json");
        await File.WriteAllTextAsync(physicsPath, JsonSerializer.Serialize(physics, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(physicsPath);

        var logPath = Path.Combine(outputDirectory, "conversion.log");
        await File.WriteAllLinesAsync(logPath, steps, cancellationToken);
        outputFiles.Add(logPath);

        return (outputDirectory, outputFiles);
    }
}
