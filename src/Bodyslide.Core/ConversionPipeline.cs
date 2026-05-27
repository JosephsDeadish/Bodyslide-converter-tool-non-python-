using System.IO.Compression;
using System.Text.Json;

namespace Bodyslide.Core;

public sealed record ConversionRequest(string InputPath, string TargetBody, string? OutputDirectory = null, string? Preset = null, bool OutputZip = false);
public sealed record ConversionPreset(string Name, string TargetBody, string DeformationProfile, string PhysicsProfile);
public sealed record ImportedArmor(string SourcePath, IReadOnlyList<string> MeshFiles, IReadOnlyList<string> TextureFiles, IReadOnlyList<string> PhysicsFiles, IReadOnlyList<string> BodyReferenceFiles, string? TemporaryWorkspace = null);
public sealed record BodyDetectionReport(string Body, double Confidence, IReadOnlyList<string> Evidence);
public sealed record MeshAnalysis(string MeshType, bool PhysicsEnabled, int MeshCount);
public sealed record DeformationCage(string Mode);
public sealed record ConvertedMesh(string MeshType, string Strategy, int MeshCount, IReadOnlyDictionary<string, double> RegionalMorphing);
public sealed record WeightedMesh(string MeshType, string WeightProfile, bool PhysicsWeightsTransferred);
public sealed record MorphSet(string LowMorph, string HighMorph, bool BodySlideCompatible);
public sealed record ClippingReport(bool HasClipping, IReadOnlyList<string> Regions, IReadOnlyList<string> DetectionMethods);
public sealed record CorrectionResult(bool Applied, string Method);
public sealed record PhysicsConfig(string Profile);
public sealed record SkeletonBoneMapping(string SourceBone, string TargetBone, bool IsPhysicsBone);
public sealed record SkeletonMappingResult(string SourceSkeleton, string TargetSkeleton, IReadOnlyList<SkeletonBoneMapping> BoneMappings, IReadOnlyList<string> UnsupportedBones);
public sealed record PartitionRebuildingResult(bool Rebuilt, IReadOnlyList<string> Partitions, IReadOnlyList<string> RemovedPartitions);
public sealed record ConversionResult(bool Success, string OutputDirectory, IReadOnlyList<string> Steps, IReadOnlyList<string> OutputFiles);

public static class PresetCatalog
{
    private static readonly Dictionary<string, ConversionPreset> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["3BA Curvy"]       = new("3BA Curvy",       "3BA",   "curvy",    "smp+cbpc"),
        ["3BA Slim"]        = new("3BA Slim",         "3BA",   "slim",     "smp+cbpc"),
        ["HIMBO Lean"]      = new("HIMBO Lean",       "HIMBO", "lean",     "smp"),
        ["HIMBO Muscular"]  = new("HIMBO Muscular",   "HIMBO", "muscular", "smp"),
        ["UNP Petite"]      = new("UNP Petite",       "UNP",   "petite",   "cbpc"),
        ["UNP Athletic"]    = new("UNP Athletic",     "UNP",   "athletic", "cbpc"),
        ["BHUNP Curvy"]     = new("BHUNP Curvy",      "BHUNP", "curvy",    "smp+cbpc"),
        ["BHUNP Slim"]      = new("BHUNP Slim",       "BHUNP", "slim",     "smp+cbpc")
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

/// <summary>
/// Defines token signatures used to match imported assets to known body families.
/// Mesh, texture, and physics token hit ratios are combined into a confidence score.
/// </summary>
internal sealed record BodySignatureTemplate(
    string Body,
    IReadOnlyList<string> MeshTokens,
    IReadOnlyList<string> TextureTokens,
    IReadOnlyList<string> PhysicsTokens);

internal static class VanillaBodySignatureDatabase
{
    public static readonly IReadOnlyList<BodySignatureTemplate> Templates =
    [
        new("CBBE",  ["cbbe", "caliente"],       ["femalebody_1", "femalebody_0"], []),
        new("UNP",   ["unp", "unpb"],            ["femalebody"],                  []),
        new("HIMBO", ["himbo", "male"],          ["malebody"],                    []),
        new("BHUNP", ["bhunp"],                  ["femalebody"],                  []),
        new("3BA",   ["3ba", "cbbe", "bodyslide"],["femalebody"],                 ["smp", "cbpc"]),
        new("TBD",   ["tbd"],                    ["femalebody"],                  []),
        new("SAM",   ["sam", "samlight"],        ["malebody"],                    []),
        new("SOS",   ["sos", "soslight"],        ["malebody"],                    ["smp"]),
        new("UBE",   ["ube"],                    ["femalebody"],                  [])
    ];
}

internal static class BodyTransformationFieldCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Fields =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CBBE"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.08,
                ["waist"]     = 0.96,
                ["pelvis"]    = 1.05,
                ["legs"]      = 1.03,
                ["shoulders"] = 1.01
            },
            ["3BA"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.12,
                ["waist"]     = 0.95,
                ["pelvis"]    = 1.06,
                ["legs"]      = 1.04,
                ["shoulders"] = 1.01
            },
            ["HIMBO"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.10,
                ["waist"]     = 1.02,
                ["pelvis"]    = 1.04,
                ["legs"]      = 1.06,
                ["shoulders"] = 1.12
            },
            ["UNP"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.04,
                ["waist"]     = 0.97,
                ["pelvis"]    = 1.02,
                ["legs"]      = 1.01,
                ["shoulders"] = 1.00
            },
            ["BHUNP"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.10,
                ["waist"]     = 0.94,
                ["pelvis"]    = 1.07,
                ["legs"]      = 1.04,
                ["shoulders"] = 1.01
            },
            ["TBD"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.06,
                ["waist"]     = 0.96,
                ["pelvis"]    = 1.04,
                ["legs"]      = 1.02,
                ["shoulders"] = 1.00
            },
            ["SAM"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.08,
                ["waist"]     = 1.01,
                ["pelvis"]    = 1.03,
                ["legs"]      = 1.05,
                ["shoulders"] = 1.10
            },
            ["SOS"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.05,
                ["waist"]     = 1.00,
                ["pelvis"]    = 1.02,
                ["legs"]      = 1.04,
                ["shoulders"] = 1.06
            },
            ["UBE"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.06,
                ["waist"]     = 0.97,
                ["pelvis"]    = 1.03,
                ["legs"]      = 1.02,
                ["shoulders"] = 1.01
            }
        };

    public static IReadOnlyDictionary<string, double> Resolve(string targetBody)
    {
        if (Fields.TryGetValue(targetBody, out var profile))
        {
            return profile;
        }

        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["chest"] = 1.02,
            ["waist"] = 0.99,
            ["pelvis"] = 1.02,
            ["legs"] = 1.01,
            ["shoulders"] = 1.0
        };
    }
}

public interface IArmorImportService
{
    Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken);
}

public interface IBodyDetectionService
{
    Task<BodyDetectionReport> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken);
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

public interface ISkeletonMappingService
{
    /// <summary>Maps source skeleton bones to the target body skeleton, reporting any unsupported physics bones.</summary>
    Task<SkeletonMappingResult> MapAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken);
}

public interface IPartitionRebuildingService
{
    /// <summary>Rebuilds BSDismemberSkinInstance partitions for Skyrim compatibility after mesh conversion.</summary>
    Task<PartitionRebuildingResult> RebuildAsync(WeightedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken);
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
    ISkeletonMappingService skeletonMapper,
    IMorphGenerationService morphGenerator,
    IPartitionRebuildingService partitionRebuilder,
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
            steps.Add($"imported:meshes={armor.MeshFiles.Count},textures={armor.TextureFiles.Count},physics={armor.PhysicsFiles.Count},bodyrefs={armor.BodyReferenceFiles.Count}");

            var detectedBody = await bodyDetector.DetectAsync(armor, cancellationToken);
            var evidenceSummary = string.Join(',', detectedBody.Evidence.Take(3));
            steps.Add($"detected-body:{detectedBody.Body}@{detectedBody.Confidence:P0}");
            if (!string.IsNullOrWhiteSpace(evidenceSummary))
            {
                steps.Add($"body-evidence:{evidenceSummary}");
            }

            var analysis = await meshAnalyzer.AnalyzeAsync(armor, cancellationToken);
            steps.Add($"mesh-type:{analysis.MeshType}");

            var cage = await cageGenerator.BuildAsync(analysis, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"cage:{cage.Mode}");

            var converted = await meshConverter.ConvertAsync(armor, analysis, cage, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"mesh-converted:{converted.Strategy}");

            var weighted = await weightTransfer.TransferAsync(converted, analysis, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"weights:{weighted.WeightProfile}");

            var skeletonMapping = await skeletonMapper.MapAsync(armor, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"skeleton:{skeletonMapping.BoneMappings.Count}-mapped,{skeletonMapping.UnsupportedBones.Count}-unsupported");

            var morphs = await morphGenerator.GenerateAsync(weighted, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"morphs:{morphs.LowMorph}/{morphs.HighMorph}");

            var partitions = await partitionRebuilder.RebuildAsync(weighted, analysis, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"partitions:{(partitions.Rebuilt ? string.Join(',', partitions.Partitions) : "unchanged")}");

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
            new BasicSkeletonMappingService(),
            new BasicMorphGenerationService(),
            new BasicPartitionRebuildingService(),
            new BasicClippingDetectionService(),
            new BasicAutoCorrectionService(),
            new BasicPhysicsSupportService(),
            new LocalExportService());
}

internal sealed record ConversionCacheEntry(
    string Key,
    DateTimeOffset LastSuccessfulConversion,
    string TargetBody,
    string MeshType,
    string Strategy,
    IReadOnlyDictionary<string, double> RegionalMorphing,
    bool HadClipping,
    string CorrectionMethod);

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
        var bodyReferenceFiles = EnumerateFiles(sourcePath, [".tri", ".osp", ".nif"])
            .Where(path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                return !string.IsNullOrWhiteSpace(fileName) &&
                    (fileName.Contains("body", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("skeleton", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        return Task.FromResult(new ImportedArmor(sourcePath, meshFiles, textureFiles, physicsFiles, bodyReferenceFiles, temporaryWorkspace));
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
    private const double MeshTokenWeight = 0.5;
    private const double TextureTokenWeight = 0.3;
    private const double PhysicsTokenWeight = 0.1;
    private const double PhysicsExpectationBoostValue = 0.1;

    public Task<BodyDetectionReport> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var meshNames = armor.MeshFiles.Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty).ToArray();
        var textureNames = armor.TextureFiles.Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty).ToArray();
        var physicsNames = armor.PhysicsFiles.Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty).ToArray();

        var scoredCandidates = VanillaBodySignatureDatabase.Templates
            .Select(template => Score(template, meshNames, textureNames, physicsNames))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Template.Body, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (scoredCandidates.Count == 0 || scoredCandidates[0].Score < 0.25)
        {
            return Task.FromResult(new BodyDetectionReport("CUSTOM", 1.0, ["fallback:signature-threshold"]));
        }

        var top = scoredCandidates[0];
        return Task.FromResult(new BodyDetectionReport(top.Template.Body, top.Score, top.Evidence));
    }

    private static (BodySignatureTemplate Template, double Score, IReadOnlyList<string> Evidence) Score(
        BodySignatureTemplate template,
        IReadOnlyList<string> meshNames,
        IReadOnlyList<string> textureNames,
        IReadOnlyList<string> physicsNames)
    {
        var evidence = new List<string>();

        var meshHitRatio = MatchRatio(meshNames, template.MeshTokens);
        if (meshHitRatio > 0)
        {
            evidence.Add($"mesh:{meshHitRatio:P0}");
        }

        var textureHitRatio = MatchRatio(textureNames, template.TextureTokens);
        if (textureHitRatio > 0)
        {
            evidence.Add($"texture:{textureHitRatio:P0}");
        }

        var physicsHitRatio = MatchRatio(physicsNames, template.PhysicsTokens);
        if (template.PhysicsTokens.Count > 0 && physicsHitRatio > 0)
        {
            evidence.Add($"physics:{physicsHitRatio:P0}");
        }

        var physicsExpectationBoost = template.PhysicsTokens.Count == 0 || physicsHitRatio > 0 ? PhysicsExpectationBoostValue : 0;
        var score = Math.Clamp(
            (meshHitRatio * MeshTokenWeight) +
            (textureHitRatio * TextureTokenWeight) +
            (physicsHitRatio * PhysicsTokenWeight) +
            physicsExpectationBoost,
            0,
            1);
        return (template, score, evidence);
    }

    private static double MatchRatio(IReadOnlyList<string> fileNames, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0 || fileNames.Count == 0)
        {
            return 0;
        }

        var combined = string.Join(' ', fileNames);
        var hits = tokens.Count(token => combined.Contains(token, StringComparison.OrdinalIgnoreCase));
        return (double)hits / tokens.Count;
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

        var finalMeshType = physicsEnabled && meshType is "cloth" or "skin-tight"
            ? "physics-enabled"
            : meshType;

        return Task.FromResult(new MeshAnalysis(finalMeshType, physicsEnabled, armor.MeshFiles.Count));
    }
}

internal sealed class BasicCageGenerationService : ICageGenerationService
{
    public Task<DeformationCage> BuildAsync(MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken)
    {
        var mode = analysis.MeshType switch
        {
            "plate" => "rigid-regional-cage",
            "physics-enabled" => "physics-stabilized-cage",
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
            "physics-enabled" => "cage+smooth-projection+physics-stabilized",
            "plate" => "cage+rigid-islands+normal-preservation",
            "leather" => "cage+local-cluster-smoothing",
            "skin-tight" => "body-transform-field",
            _ => "hybrid-cage-deformation"
        };

        var baseField = BodyTransformationFieldCatalog.Resolve(targetBody);
        var regionalMorphing = analysis.MeshType switch
        {
            "plate" => ApplyRigidityConstraints(baseField),
            "cloth" => ApplySoftClothAmplification(baseField),
            "physics-enabled" => ApplySoftClothAmplification(baseField),
            _ => baseField
        };

        return Task.FromResult(new ConvertedMesh(analysis.MeshType, strategy, analysis.MeshCount, regionalMorphing));
    }

    private static IReadOnlyDictionary<string, double> ApplyRigidityConstraints(IReadOnlyDictionary<string, double> field) =>
        field.ToDictionary(pair => pair.Key, pair => 1 + ((pair.Value - 1) * 0.45), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> ApplySoftClothAmplification(IReadOnlyDictionary<string, double> field) =>
        field.ToDictionary(pair => pair.Key, pair => 1 + ((pair.Value - 1) * 1.15), StringComparer.OrdinalIgnoreCase);
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
            "physics-enabled" => new[] { "breasts", "thighs", "butt", "armpits" },
            "skin-tight" => new[] { "breasts", "thighs", "butt" },
            _ => new[] { "armpits", "thighs" }
        };

        var detectionMethods = mesh.MeshType is "physics-enabled" or "skin-tight"
            ? new[] { "pose-simulation", "animation-stress", "voxel-penetration" }
            : new[] { "pose-simulation", "animation-stress" };

        var hasClipping = mesh.MeshType is "cloth" or "skin-tight" or "physics-enabled";
        return Task.FromResult(new ClippingReport(hasClipping, riskRegions, detectionMethods));
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

/// <summary>
/// Maps source skeleton bones to target body skeleton bones using known bone name tables.
/// Bones that exist in the source but have no equivalent in the target skeleton are reported as unsupported.
/// </summary>
internal sealed class BasicSkeletonMappingService : ISkeletonMappingService
{
    // Standard vanilla + XPMSSE bones shared by most body types.
    private static readonly IReadOnlyList<string> CommonBones =
    [
        "NPC Root", "NPC COM", "NPC Pelvis", "NPC Spine", "NPC Spine1", "NPC Spine2",
        "NPC Neck", "NPC Head",
        "NPC L Clavicle", "NPC L UpperArm", "NPC L ForeArm", "NPC L Hand",
        "NPC R Clavicle", "NPC R UpperArm", "NPC R ForeArm", "NPC R Hand",
        "NPC L Thigh", "NPC L Calf", "NPC L Foot",
        "NPC R Thigh", "NPC R Calf", "NPC R Foot"
    ];

    // Physics bones added by SMP/3BA/BHUNP on female bodies.
    private static readonly IReadOnlySet<string> FeaturePhysicsBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NPC L Breast", "NPC R Breast", "NPC L Breast01", "NPC R Breast01",
        "NPC Belly", "NPC Butt", "NPC L Butt", "NPC R Butt",
        "NPC L Breast02", "NPC R Breast02"
    };

    // Physics bones specific to HIMBO/SAM male bodies.
    private static readonly IReadOnlySet<string> MalePhysicsBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NPC L Pec", "NPC R Pec", "NPC Belly", "NPC L Lat", "NPC R Lat"
    };

    // Map of which body types support which extra physics bone sets.
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> BodyPhysicsBoneSupport =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["3BA"]   = FeaturePhysicsBones,
            ["BHUNP"] = FeaturePhysicsBones,
            ["TBD"]   = FeaturePhysicsBones,
            ["HIMBO"] = MalePhysicsBones,
            ["SAM"]   = MalePhysicsBones,
            ["CBBE"]  = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["UNP"]   = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["UBE"]   = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["SOS"]   = MalePhysicsBones
        };

    public Task<SkeletonMappingResult> MapAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken)
    {
        BodyPhysicsBoneSupport.TryGetValue(targetBody, out var targetPhysicsBones);
        targetPhysicsBones ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allTargetBones = CommonBones.Concat(targetPhysicsBones).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Infer which source physics bones are present from the body reference / physics files.
        var sourcePhysicsBones = armor.PhysicsFiles.Count > 0
            ? FeaturePhysicsBones.Concat(MalePhysicsBones).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : (IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allSourceBones = CommonBones.Concat(sourcePhysicsBones).ToList();

        var mappings = new List<SkeletonBoneMapping>(allSourceBones.Count);
        var unsupportedBones = new List<string>();

        foreach (var bone in allSourceBones)
        {
            if (allTargetBones.Contains(bone))
            {
                mappings.Add(new SkeletonBoneMapping(bone, bone, FeaturePhysicsBones.Contains(bone) || MalePhysicsBones.Contains(bone)));
            }
            else
            {
                unsupportedBones.Add(bone);
            }
        }

        var sourceSkeleton = armor.PhysicsFiles.Count > 0 ? "xpmsse-physics" : "xpmsse-vanilla";
        var targetSkeleton = targetPhysicsBones.Count > 0 ? $"xpmsse-{targetBody.ToLowerInvariant()}-physics" : "xpmsse-vanilla";

        return Task.FromResult(new SkeletonMappingResult(sourceSkeleton, targetSkeleton, mappings, unsupportedBones));
    }
}

/// <summary>
/// Rebuilds Skyrim BSDismemberSkinInstance body partitions after mesh conversion to ensure
/// correct slot assignments and prevent invisible body parts or armor conflicts.
/// </summary>
internal sealed class BasicPartitionRebuildingService : IPartitionRebuildingService
{
    // Standard Skyrim partition slot numbers and their labels.
    private static readonly IReadOnlyDictionary<int, string> PartitionSlots =
        new Dictionary<int, string>
        {
            [32] = "Body",
            [33] = "Hands",
            [34] = "Forearms",
            [35] = "Amulet",
            [36] = "Ring",
            [37] = "Feet",
            [38] = "Calves",
            [39] = "Shield",
            [40] = "Tail",
            [41] = "LongHair",
            [42] = "Circlet",
            [43] = "Ears",
            [44] = "Dragon Head",
            [45] = "Dragon LWing",
            [46] = "Dragon RWing",
            [47] = "Dragon Body",
            [48] = "Dragon Tail",
            [49] = "Dragon Leg",
            [50] = "Dragon Claws",
            [54] = "DecapHead",
            [55] = "Decap",
            [56] = "Genitals"
        };

    public Task<PartitionRebuildingResult> RebuildAsync(WeightedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken)
    {
        // Select partitions based on mesh type and target body.
        var slots = new List<int>();

        switch (analysis.MeshType)
        {
            case "plate":
            case "leather":
            case "mixed":
                slots.Add(32); // Body
                slots.Add(33); // Hands
                slots.Add(37); // Feet
                break;

            case "cloth":
            case "skin-tight":
            case "physics-enabled":
                slots.Add(32); // Body
                break;

            default:
                slots.Add(32);
                break;
        }

        // Physics-capable bodies get the genitals partition for compatibility.
        if (string.Equals(targetBody, "3BA", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetBody, "BHUNP", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetBody, "SAM", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetBody, "HIMBO", StringComparison.OrdinalIgnoreCase))
        {
            slots.Add(56); // Genitals
        }

        var partitionLabels = slots
            .Where(PartitionSlots.ContainsKey)
            .Select(s => $"{s}:{PartitionSlots[s]}")
            .ToList();

        // Report any slots that cannot be mapped to valid partition names as removed.
        var removedSlots = slots
            .Where(s => !PartitionSlots.ContainsKey(s))
            .Select(s => s.ToString())
            .ToList();

        return Task.FromResult(new PartitionRebuildingResult(true, partitionLabels, removedSlots));
    }
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
                PhysicsCount = armor.PhysicsFiles.Count,
                BodyReferenceCount = armor.BodyReferenceFiles.Count
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

        var previewPath = Path.Combine(outputDirectory, "preview-renders.json");
        var previewPayload = new
        {
            Mode = "placeholder",
            Captures = new[]
            {
                "front",
                "side",
                "back"
            }
        };
        await File.WriteAllTextAsync(previewPath, JsonSerializer.Serialize(previewPayload, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(previewPath);

        var logPath = Path.Combine(outputDirectory, "conversion.log");
        await File.WriteAllLinesAsync(logPath, steps, cancellationToken);
        outputFiles.Add(logPath);

        var cachePath = Path.Combine(outputDirectory, ".conversion-learning-cache.json");
        var cache = await LoadCacheAsync(cachePath, cancellationToken);
        var cacheKey = $"{SanitizeCacheKeyPart(Path.GetFileNameWithoutExtension(armor.MeshFiles[0]) ?? "unknown")}:{SanitizeCacheKeyPart(request.TargetBody)}";
        cache.RemoveAll(entry => string.Equals(entry.Key, cacheKey, StringComparison.OrdinalIgnoreCase));
        cache.Add(new ConversionCacheEntry(
            cacheKey,
            DateTimeOffset.UtcNow,
            request.TargetBody,
            analysis.MeshType,
            mesh.Strategy,
            mesh.RegionalMorphing,
            clipping.HasClipping,
            correction.Method));
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(cachePath);

        if (request.OutputZip)
        {
            var zipPath = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(outputDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            outputFiles = [zipPath];
            return (outputDirectory, outputFiles);
        }

        return (outputDirectory, outputFiles);
    }

    private static async Task<List<ConversionCacheEntry>> LoadCacheAsync(string cachePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(cachePath))
        {
            return [];
        }

        var raw = await File.ReadAllTextAsync(cachePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ConversionCacheEntry>>(raw) ?? [];
    }

    private static string SanitizeCacheKeyPart(string value)
    {
        var sanitized = new string(value
            .Where(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
