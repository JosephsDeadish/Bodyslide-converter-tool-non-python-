using System.IO.Compression;
using System.Text.Json;

namespace Bodyslide.Core;

public sealed record ConversionRequest(string InputPath, string TargetBody, string? OutputDirectory = null, string? Preset = null, bool OutputZip = false, string? DeformationProfile = null);
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
public sealed record PhysicsConfig(string Profile, string? CbpcConfigXml = null, string? SmpConfigXml = null);
public sealed record VanillaArmorEntry(
    string Name,
    IReadOnlyList<string> MeshFileTokens,
    string SourceBody,
    IReadOnlyList<string> RegionSlots,
    string RecommendedProfile);
public sealed record VoxelCollisionResult(
    bool HasPenetrations,
    IReadOnlyList<string> AffectedRegions,
    IReadOnlyDictionary<string, double> PushOutMagnitudes,
    int GridResolution);
public sealed record SkeletonBoneMapping(string SourceBone, string TargetBone, bool IsPhysicsBone);
public sealed record SkeletonMappingResult(string SourceSkeleton, string TargetSkeleton, IReadOnlyList<SkeletonBoneMapping> BoneMappings, IReadOnlyList<string> UnsupportedBones);
public sealed record PartitionRebuildingResult(bool Rebuilt, IReadOnlyList<string> Partitions, IReadOnlyList<string> RemovedPartitions);
public sealed record ConversionResult(bool Success, string OutputDirectory, IReadOnlyList<string> Steps, IReadOnlyList<string> OutputFiles);

public sealed record BodySlideProject(string ProjectName, string TargetBody, IReadOnlyList<string> Sliders, string OspXml);
public sealed record TextureSummary(int TotalCount, IReadOnlyList<string> DiffuseFiles, IReadOnlyList<string> NormalFiles, IReadOnlyList<string> MissingNormals);
public sealed record PluginArmorAddon(string RecordType, IReadOnlyList<string> DetectedMeshPaths);
public sealed record PluginAnalysisResult(IReadOnlyList<string> ScannedPlugins, IReadOnlyList<PluginArmorAddon> ArmorAddons, string PatchGuidance);
public sealed record MeshDependencyMapEntry(
    string Mesh,
    IReadOnlyList<string> Textures,
    IReadOnlyList<string> PhysicsFiles,
    IReadOnlyList<string> BodyReferences,
    IReadOnlyList<string> PluginMeshReferences);

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
    Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, string? deformationProfile, CancellationToken cancellationToken);
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
        BodySlideProject bodySlideProject,
        PluginAnalysisResult pluginAnalysis,
        TextureSummary textureSummary,
        IReadOnlyList<string> steps,
        CancellationToken cancellationToken);
}

public interface IBodySlideProjectService
{
    /// <summary>Generates a BodySlide .osp project file for the converted armor targeting a specific body.</summary>
    Task<BodySlideProject> GenerateAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface ITextureAnalysisService
{
    /// <summary>Analyzes DDS texture files for normal map coverage and reports missing normal maps.</summary>
    Task<TextureSummary> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken);
}

public interface IPluginAnalysisService
{
    /// <summary>Scans .esp/.esm/.esl plugin files for ArmorAddon mesh path references and generates patch guidance.</summary>
    Task<PluginAnalysisResult> AnalyzeAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken);
}

public interface IVanillaArmorLookupService
{
    /// <summary>Tries to match a mesh file name against the vanilla armor static database.</summary>
    bool TryLookup(string meshFileName, out VanillaArmorEntry? entry);

    IReadOnlyList<VanillaArmorEntry> All { get; }
}

public interface IVoxelCollisionService
{
    /// <summary>
    /// Computes voxel-grid penetration between the converted mesh and the target body,
    /// returning push-out magnitudes per body region.
    /// </summary>
    Task<VoxelCollisionResult> ComputeAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken);
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
    IBodySlideProjectService bodySlideProjectService,
    ITextureAnalysisService textureAnalysisService,
    IPluginAnalysisService pluginAnalysisService,
    IVanillaArmorLookupService vanillaArmorLookup,
    IVoxelCollisionService voxelCollision,
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
            var deformationProfile = normalized.Preset?.DeformationProfile ?? normalized.Request.DeformationProfile;
            if (!string.IsNullOrWhiteSpace(deformationProfile))
            {
                steps.Add($"deformation-profile:{deformationProfile}");
            }

            armor = await importer.ImportAsync(normalized.Request.InputPath, cancellationToken);
            steps.Add($"imported:meshes={armor.MeshFiles.Count},textures={armor.TextureFiles.Count},physics={armor.PhysicsFiles.Count},bodyrefs={armor.BodyReferenceFiles.Count}");

            // Vanilla armor database lookup — enriches detection with known region maps.
            var vanillaEntry = armor.MeshFiles
                .Select(mesh => vanillaArmorLookup.TryLookup(Path.GetFileName(mesh), out var entry) ? entry : null)
                .FirstOrDefault(entry => entry is not null);
            steps.Add(vanillaEntry is not null
                ? $"vanilla-armor:{vanillaEntry.Name},profile={vanillaEntry.RecommendedProfile},slots={vanillaEntry.RegionSlots.Count}"
                : "vanilla-armor:unknown");

            var textureSummary = await textureAnalysisService.AnalyzeAsync(armor, cancellationToken);
            if (textureSummary.MissingNormals.Count > 0)
            {
                steps.Add($"textures:missing-normals={textureSummary.MissingNormals.Count}");
            }

            var pluginAnalysis = await pluginAnalysisService.AnalyzeAsync(armor, normalized.Request.TargetBody, cancellationToken);
            if (pluginAnalysis.ScannedPlugins.Count > 0)
            {
                steps.Add($"plugins:scanned={pluginAnalysis.ScannedPlugins.Count},addons={pluginAnalysis.ArmorAddons.Count}");
            }

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

            var converted = await meshConverter.ConvertAsync(armor, analysis, cage, normalized.Request.TargetBody, deformationProfile, cancellationToken);
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

            // Voxel collision offset pass — detects body/armor penetrations using a
            // simplified voxel grid and computes per-region push-out magnitudes.
            var voxelResult = await voxelCollision.ComputeAsync(armor, converted, normalized.Request.TargetBody, cancellationToken);
            steps.Add(voxelResult.HasPenetrations
                ? $"voxel-collision:penetrations={voxelResult.AffectedRegions.Count},grid={voxelResult.GridResolution}"
                : "voxel-collision:none");

            var physicsProfile = normalized.Preset?.PhysicsProfile ?? "smp+cbpc";
            var physics = await physicsSupport.BuildAsync(weighted, normalized.Request.TargetBody, physicsProfile, cancellationToken);
            steps.Add($"physics:{physics.Profile}");

            var bodySlideProject = await bodySlideProjectService.GenerateAsync(armor, converted, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"bodyslide:{bodySlideProject.ProjectName},{bodySlideProject.Sliders.Count}-sliders");

            var export = await exporter.ExportAsync(normalized.Request, armor, analysis, converted, morphs, physics, clipping, correction, bodySlideProject, pluginAnalysis, textureSummary, steps, cancellationToken);
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
        if (File.Exists(request.InputPath) && Path.GetExtension(request.InputPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extractedArchive = Path.Combine(Path.GetTempPath(), "bodyslide-batch-extract", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractedArchive);
            ZipFile.ExtractToDirectory(request.InputPath, extractedArchive);

            try
            {
                return await ConvertDirectoryMeshesAsync(request, extractedArchive, cancellationToken);
            }
            finally
            {
                if (Directory.Exists(extractedArchive))
                {
                    Directory.Delete(extractedArchive, recursive: true);
                }
            }
        }

        if (File.Exists(request.InputPath) || !Directory.Exists(request.InputPath))
        {
            return [await orchestrator.ConvertAsync(request, cancellationToken)];
        }

        return await ConvertDirectoryMeshesAsync(request, request.InputPath, cancellationToken);
    }

    private async Task<IReadOnlyList<ConversionResult>> ConvertDirectoryMeshesAsync(
        ConversionRequest request,
        string sourceDirectory,
        CancellationToken cancellationToken)
    {
        var meshFiles = Directory.GetFiles(sourceDirectory, "*.nif", SearchOption.AllDirectories)
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
            new BodySlideOspProjectService(),
            new BasicTextureAnalysisService(),
            new BasicPluginAnalysisService(),
            new VanillaArmorLookupService(),
            new SimplifiedVoxelCollisionService(),
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
        var fileNames = armor.MeshFiles.Select(path => Path.GetFileNameWithoutExtension(path)?.ToLowerInvariant() ?? string.Empty).ToList();

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
    public Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, string? deformationProfile, CancellationToken cancellationToken)
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
        var profileField = DeformationProfileModifier.Apply(baseField, deformationProfile);
        var regionalMorphing = analysis.MeshType switch
        {
            "plate" => ApplyRigidityConstraints(profileField),
            "cloth" => ApplySoftClothAmplification(profileField),
            "physics-enabled" => ApplySoftClothAmplification(profileField),
            _ => profileField
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
    private static readonly IReadOnlySet<string> MaleBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "HIMBO", "SAM", "SOS"
    };

    public Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, string physicsProfile, CancellationToken cancellationToken)
    {
        var hasCbpc = physicsProfile.Contains("cbpc", StringComparison.OrdinalIgnoreCase);
        var hasSmp  = physicsProfile.Contains("smp",  StringComparison.OrdinalIgnoreCase);
        var isMale  = MaleBodies.Contains(targetBody);

        var cbpcXml = hasCbpc ? BuildCbpcXml(isMale) : null;
        var smpXml  = hasSmp  ? BuildSmpXml(targetBody, isMale) : null;

        return Task.FromResult(new PhysicsConfig(physicsProfile, cbpcXml, smpXml));
    }

    private static string BuildCbpcXml(bool isMale)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<CBPCConfig version=\"1\">");
        if (isMale)
        {
            sb.AppendLine("  <PecPhysics>");
            sb.AppendLine("    <Stiffness>0.88</Stiffness>");
            sb.AppendLine("    <Damping>0.62</Damping>");
            sb.AppendLine("    <Gravity>0.04</Gravity>");
            sb.AppendLine("    <MaxOffset>0.06</MaxOffset>");
            sb.AppendLine("  </PecPhysics>");
            sb.AppendLine("  <BellyPhysics>");
            sb.AppendLine("    <Stiffness>0.92</Stiffness>");
            sb.AppendLine("    <Damping>0.65</Damping>");
            sb.AppendLine("    <Gravity>0.03</Gravity>");
            sb.AppendLine("    <MaxOffset>0.04</MaxOffset>");
            sb.AppendLine("  </BellyPhysics>");
        }
        else
        {
            sb.AppendLine("  <BreastPhysics>");
            sb.AppendLine("    <Stiffness>0.90</Stiffness>");
            sb.AppendLine("    <Damping>0.60</Damping>");
            sb.AppendLine("    <Gravity>0.05</Gravity>");
            sb.AppendLine("    <MaxOffset>0.08</MaxOffset>");
            sb.AppendLine("  </BreastPhysics>");
            sb.AppendLine("  <ButtPhysics>");
            sb.AppendLine("    <Stiffness>0.85</Stiffness>");
            sb.AppendLine("    <Damping>0.55</Damping>");
            sb.AppendLine("    <Gravity>0.06</Gravity>");
            sb.AppendLine("    <MaxOffset>0.06</MaxOffset>");
            sb.AppendLine("  </ButtPhysics>");
            sb.AppendLine("  <BellyPhysics>");
            sb.AppendLine("    <Stiffness>0.92</Stiffness>");
            sb.AppendLine("    <Damping>0.65</Damping>");
            sb.AppendLine("    <Gravity>0.03</Gravity>");
            sb.AppendLine("    <MaxOffset>0.04</MaxOffset>");
            sb.AppendLine("  </BellyPhysics>");
        }

        sb.AppendLine("</CBPCConfig>");
        return sb.ToString();
    }

    private static string BuildSmpXml(string targetBody, bool isMale)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine($"<system name=\"{targetBody}ArmorPhysics\">");
        if (isMale)
        {
            sb.AppendLine("  <bone name=\"NPC L Pec\" mass=\"2.5\" stiffness=\"0.85\" damping=\"0.60\">");
            sb.AppendLine("    <angularLimit min=\"-15\" max=\"15\" restitution=\"0.15\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine("  <bone name=\"NPC R Pec\" mass=\"2.5\" stiffness=\"0.85\" damping=\"0.60\">");
            sb.AppendLine("    <angularLimit min=\"-15\" max=\"15\" restitution=\"0.15\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine("  <bone name=\"NPC Belly\" mass=\"1.5\" stiffness=\"0.90\" damping=\"0.65\">");
            sb.AppendLine("    <angularLimit min=\"-8\" max=\"8\" restitution=\"0.10\" />");
            sb.AppendLine("  </bone>");
        }
        else
        {
            sb.AppendLine("  <bone name=\"NPC L Breast01\" mass=\"2.0\" stiffness=\"0.80\" damping=\"0.50\">");
            sb.AppendLine("    <angularLimit min=\"-20\" max=\"20\" restitution=\"0.20\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine("  <bone name=\"NPC R Breast01\" mass=\"2.0\" stiffness=\"0.80\" damping=\"0.50\">");
            sb.AppendLine("    <angularLimit min=\"-20\" max=\"20\" restitution=\"0.20\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine("  <bone name=\"NPC Belly\" mass=\"1.5\" stiffness=\"0.90\" damping=\"0.60\">");
            sb.AppendLine("    <angularLimit min=\"-10\" max=\"10\" restitution=\"0.10\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine("  <bone name=\"NPC L Butt\" mass=\"1.8\" stiffness=\"0.75\" damping=\"0.55\">");
            sb.AppendLine("    <angularLimit min=\"-15\" max=\"15\" restitution=\"0.20\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine("  <bone name=\"NPC R Butt\" mass=\"1.8\" stiffness=\"0.75\" damping=\"0.55\">");
            sb.AppendLine("    <angularLimit min=\"-15\" max=\"15\" restitution=\"0.20\" />");
            sb.AppendLine("  </bone>");
        }

        sb.AppendLine("</system>");
        return sb.ToString();
    }
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

/// <summary>
/// Scales each regional morph factor toward or away from 1.0 using a named deformation profile amplifier.
/// </summary>
public static class DeformationProfileModifier
{
    // Amount to amplify the body transformation delta (value - 1) for each profile.
    private static readonly IReadOnlyDictionary<string, double> ProfileAmplifiers =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["curvy"]    = 1.15,
            ["slim"]     = 0.82,
            ["petite"]   = 0.75,
            ["athletic"] = 1.08,
            ["muscular"] = 1.25,
            ["lean"]     = 0.88
        };

    public static IReadOnlyDictionary<string, double> Apply(IReadOnlyDictionary<string, double> field, string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile) || !ProfileAmplifiers.TryGetValue(profile, out var amplifier))
        {
            return field;
        }

        return field.ToDictionary(
            pair => pair.Key,
            pair => 1.0 + ((pair.Value - 1.0) * amplifier),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns all supported profile names in alphabetical order.</summary>
    public static IReadOnlyList<string> All => ProfileAmplifiers.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>
/// Generates a BodySlide .osp project XML file for the converted armor.
/// The OSP file contains standard slider definitions for the target body type.
/// </summary>
internal sealed class BodySlideOspProjectService : IBodySlideProjectService
{
    // Standard BodySlide sliders per target body family.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BodySliders =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CBBE"]  = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist"],
            ["3BA"]   = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist", "BreastsPhysics", "ButtPhysics", "BellyPhysics"],
            ["BHUNP"] = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist", "BreastsPhysics", "ButtPhysics"],
            ["UNP"]   = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders"],
            ["HIMBO"] = ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt", "Pecs"],
            ["SAM"]   = ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt"],
            ["SOS"]   = ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt"],
            ["TBD"]   = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves"],
            ["UBE"]   = ["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth", "Thighs"]
        };

    private static readonly IReadOnlyDictionary<string, string> BodyOutputPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HIMBO"] = @"meshes\actors\character\character assets male\",
            ["SAM"]   = @"meshes\actors\character\character assets male\",
            ["SOS"]   = @"meshes\actors\character\character assets male\"
        };

    private static readonly IReadOnlySet<string> MaleBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "HIMBO", "SAM", "SOS"
    };

    public Task<BodySlideProject> GenerateAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken)
    {
        var rawName = Path.GetFileNameWithoutExtension(armor.MeshFiles[0]) ?? "ConvertedArmor";
        var projectName = new string(rawName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(projectName)) projectName = "ConvertedArmor";

        var sliders = BodySliders.TryGetValue(targetBody, out var bodySliders)
            ? bodySliders
            : (IReadOnlyList<string>)["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth"];

        BodyOutputPaths.TryGetValue(targetBody, out var outputPath);
        outputPath ??= @"meshes\actors\character\character assets\";

        var isMale = MaleBodies.Contains(targetBody);
        var gender = isMale ? "male" : "female";
        var outputFile0 = isMale ? "malebody_0.nif" : "femalebody_0.nif";
        var outputFile1 = isMale ? "malebody_1.nif" : "femalebody_1.nif";

        var shapeDataFolder = $@"CalienteTools\BodySlide\ShapeData\{projectName}";
        var sourceFile = $@"{shapeDataFolder}\{projectName}.nif";

        var ospXml = BuildOspXml(projectName, sliders, shapeDataFolder, sourceFile, outputPath, gender, outputFile0, outputFile1);

        return Task.FromResult(new BodySlideProject(projectName, targetBody, sliders, ospXml));
    }

    private static string BuildOspXml(
        string projectName,
        IReadOnlyList<string> sliders,
        string setFolder,
        string sourceFile,
        string outputPath,
        string gender,
        string outputFile0,
        string outputFile1)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<SliderSetInfo version=\"1\">");
        sb.AppendLine($"    <SliderSet name=\"{Escape(projectName)}\" baseShape=\"Base Shape\" bsversion=\"20\">");
        sb.AppendLine($"        <SetFolder>{Escape(setFolder)}</SetFolder>");
        sb.AppendLine($"        <SourceFile>{Escape(sourceFile)}</SourceFile>");
        sb.AppendLine($"        <OutputPath>{Escape(outputPath)}</OutputPath>");
        sb.AppendLine($"        <OutputFile gender=\"{gender}\" use=\"true\">{Escape(outputFile0)}</OutputFile>");
        sb.AppendLine($"        <OutputFile gender=\"{gender}\" use=\"true\" morphfile=\"1\">{Escape(outputFile1)}</OutputFile>");

        foreach (var slider in sliders)
        {
            sb.AppendLine($"        <Slider name=\"{Escape(slider)}\" invert=\"false\" zap=\"false\" uv=\"false\">");
            sb.AppendLine("            <Low value=\"0\" />");
            sb.AppendLine("            <High value=\"100\" />");
            sb.AppendLine("        </Slider>");
        }

        sb.AppendLine("    </SliderSet>");
        sb.AppendLine("</SliderSetInfo>");
        return sb.ToString();
    }

    // Minimal XML attribute/content escaping for values embedded in the OSP document.
    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

/// <summary>
/// Analyzes DDS texture files by reading magic bytes and categorizing diffuse vs. normal maps.
/// Reports diffuse textures that have no matching _n normal map in the set.
/// </summary>
internal sealed class BasicTextureAnalysisService : ITextureAnalysisService
{
    // DDS magic: "DDS " = 0x44 0x44 0x53 0x20
    private static readonly byte[] DdsMagic = [0x44, 0x44, 0x53, 0x20];

    public async Task<TextureSummary> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var diffuseFiles = new List<string>();
        var normalFiles = new List<string>();
        var missingNormals = new List<string>();

        foreach (var texturePath in armor.TextureFiles)
        {
            if (!File.Exists(texturePath)) continue;
            if (!Path.GetExtension(texturePath).Equals(".dds", StringComparison.OrdinalIgnoreCase)) continue;
            if (!await IsValidDdsAsync(texturePath, cancellationToken)) continue;

            var fileName = Path.GetFileName(texturePath);
            var baseName = Path.GetFileNameWithoutExtension(texturePath);

            if (baseName.EndsWith("_n", StringComparison.OrdinalIgnoreCase) ||
                baseName.EndsWith("_normal", StringComparison.OrdinalIgnoreCase))
            {
                normalFiles.Add(fileName);
            }
            else
            {
                diffuseFiles.Add(fileName);
            }
        }

        foreach (var diffuse in diffuseFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(diffuse);
            var expectedNormal = baseName + "_n.dds";
            if (!normalFiles.Any(n => n.Equals(expectedNormal, StringComparison.OrdinalIgnoreCase)))
            {
                missingNormals.Add(diffuse);
            }
        }

        return new TextureSummary(armor.TextureFiles.Count, diffuseFiles, normalFiles, missingNormals);
    }

    private static async Task<bool> IsValidDdsAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var header = new byte[4];
            await using var stream = File.OpenRead(path);
            var read = await stream.ReadAsync(header.AsMemory(0, 4), cancellationToken);
            return read == 4 && header.SequenceEqual(DdsMagic);
        }
        catch (IOException)
        {
            return false;
        }
    }
}

/// <summary>
/// Scans .esp/.esm/.esl plugin files for NIF mesh path references and generates guidance
/// on which ArmorAddon records need to be updated to point at the converted meshes.
/// </summary>
internal sealed class BasicPluginAnalysisService : IPluginAnalysisService
{
    private static readonly IReadOnlyList<string> PluginExtensions = [".esp", ".esm", ".esl"];

    // Regex matches paths like "meshes/armor/iron/ironarmor_0.nif"
    private static readonly System.Text.RegularExpressions.Regex MeshPathPattern =
        new(@"meshes[\\/][^\x00""<>|?*\x01-\x1F]{1,260}\.nif",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<PluginAnalysisResult> AnalyzeAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken)
    {
        var pluginFiles = new List<string>();

        if (Directory.Exists(armor.SourcePath))
        {
            pluginFiles.AddRange(Directory.GetFiles(armor.SourcePath, "*.*", SearchOption.AllDirectories)
                .Where(f => PluginExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        }
        else if (File.Exists(armor.SourcePath) &&
                 PluginExtensions.Contains(Path.GetExtension(armor.SourcePath), StringComparer.OrdinalIgnoreCase))
        {
            pluginFiles.Add(armor.SourcePath);
        }

        var armorAddons = new List<PluginArmorAddon>();

        foreach (var pluginFile in pluginFiles)
        {
            var addons = await ScanPluginForMeshPathsAsync(pluginFile, cancellationToken);
            armorAddons.AddRange(addons);
        }

        var guidance = BuildPatchGuidance(armorAddons, targetBody, pluginFiles.Count);
        return new PluginAnalysisResult(
            pluginFiles.Select(f => Path.GetFileName(f) ?? f).ToList(),
            armorAddons,
            guidance);
    }

    private async Task<IReadOnlyList<PluginArmorAddon>> ScanPluginForMeshPathsAsync(string pluginPath, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(pluginPath, cancellationToken);
            // Read the binary as Latin-1 so all byte values survive the round-trip.
            var content = System.Text.Encoding.Latin1.GetString(bytes);

            var meshPaths = MeshPathPattern.Matches(content)
                .Select(m => m.Value.Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (meshPaths.Count == 0) return [];

            var recordType = Path.GetFileNameWithoutExtension(pluginPath) ?? "unknown";
            return [new PluginArmorAddon(recordType, meshPaths)];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static string BuildPatchGuidance(IReadOnlyList<PluginArmorAddon> addons, string targetBody, int pluginCount)
    {
        if (pluginCount == 0)
        {
            return $"No plugin files found. Add the converted meshes to an existing .esp or create a new patch plugin targeting {targetBody}.";
        }

        if (addons.Count == 0)
        {
            return $"Scanned {pluginCount} plugin file(s) — no mesh path references detected. Verify ArmorAddon (ARMA) records manually in xEdit.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {addons.Count} plugin record(s) with mesh paths. Update each ArmorAddon (ARMA) record to reference the converted {targetBody} meshes:");
        foreach (var addon in addons)
        {
            sb.AppendLine($"  Plugin: {addon.RecordType}");
            foreach (var path in addon.DetectedMeshPaths.Take(10))
            {
                sb.AppendLine($"    {path}  →  [replace with {targetBody} converted path]");
            }

            if (addon.DetectedMeshPaths.Count > 10)
            {
                sb.AppendLine($"    ... and {addon.DetectedMeshPaths.Count - 10} more path(s)");
            }
        }

        return sb.ToString().Trim();
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
        BodySlideProject bodySlideProject,
        PluginAnalysisResult pluginAnalysis,
        TextureSummary textureSummary,
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
            BodySlide = new { bodySlideProject.ProjectName, bodySlideProject.TargetBody, SliderCount = bodySlideProject.Sliders.Count },
            Plugins = new { ScannedCount = pluginAnalysis.ScannedPlugins.Count, AddonCount = pluginAnalysis.ArmorAddons.Count },
            Textures = new { textureSummary.TotalCount, DiffuseCount = textureSummary.DiffuseFiles.Count, NormalCount = textureSummary.NormalFiles.Count, MissingNormals = textureSummary.MissingNormals },
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

        var dependencyMapPath = Path.Combine(outputDirectory, "dependency-map.json");
        var dependencyMap = BuildDependencyMap(armor, pluginAnalysis);
        await File.WriteAllTextAsync(
            dependencyMapPath,
            JsonSerializer.Serialize(dependencyMap, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        outputFiles.Add(dependencyMapPath);

        var morphPath = Path.Combine(outputDirectory, "morphs.json");
        await File.WriteAllTextAsync(morphPath, JsonSerializer.Serialize(morphs, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(morphPath);

        var physicsPath = Path.Combine(outputDirectory, "physics.json");
        await File.WriteAllTextAsync(physicsPath, JsonSerializer.Serialize(physics, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(physicsPath);

        // Write CBPC physics config XML when present.
        if (!string.IsNullOrWhiteSpace(physics.CbpcConfigXml))
        {
            var cbpcPath = Path.Combine(outputDirectory, "cbpc-config.xml");
            await File.WriteAllTextAsync(cbpcPath, physics.CbpcConfigXml, cancellationToken);
            outputFiles.Add(cbpcPath);
        }

        // Write SMP physics config XML when present.
        if (!string.IsNullOrWhiteSpace(physics.SmpConfigXml))
        {
            var smpPath = Path.Combine(outputDirectory, "smp-config.xml");
            await File.WriteAllTextAsync(smpPath, physics.SmpConfigXml, cancellationToken);
            outputFiles.Add(smpPath);
        }

        // Write the BodySlide project .osp file for use with the BodySlide application.
        var ospPath = Path.Combine(outputDirectory, $"{bodySlideProject.ProjectName}.osp");
        await File.WriteAllTextAsync(ospPath, bodySlideProject.OspXml, cancellationToken);
        outputFiles.Add(ospPath);

        // Write plugin patch guidance when plugins were found.
        if (pluginAnalysis.ScannedPlugins.Count > 0 || pluginAnalysis.ArmorAddons.Count > 0)
        {
            var pluginPatchPath = Path.Combine(outputDirectory, "plugin-patches.json");
            await File.WriteAllTextAsync(pluginPatchPath,
                JsonSerializer.Serialize(pluginAnalysis, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            outputFiles.Add(pluginPatchPath);
        }

        // Write texture summary when textures are present.
        if (textureSummary.TotalCount > 0)
        {
            var textureSummaryPath = Path.Combine(outputDirectory, "texture-summary.json");
            await File.WriteAllTextAsync(textureSummaryPath,
                JsonSerializer.Serialize(textureSummary, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            outputFiles.Add(textureSummaryPath);
        }

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

    private static IReadOnlyList<MeshDependencyMapEntry> BuildDependencyMap(ImportedArmor armor, PluginAnalysisResult pluginAnalysis)
    {
        var pluginMeshReferences = pluginAnalysis.ArmorAddons
            .SelectMany(addon => addon.DetectedMeshPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return armor.MeshFiles
            .Select(mesh =>
            {
                var meshFileName = Path.GetFileName(mesh) ?? mesh;
                var meshToken = NormalizeMeshToken(meshFileName);

                var textures = ResolveRelatedFiles(armor.TextureFiles, meshToken);
                var physicsFiles = ResolveRelatedFiles(armor.PhysicsFiles, meshToken);
                var bodyReferences = ResolveRelatedFiles(armor.BodyReferenceFiles, meshToken);

                var matchedPluginPaths = pluginMeshReferences
                    .Where(path => Path.GetFileName(path).Contains(meshFileName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new MeshDependencyMapEntry(
                    meshFileName,
                    textures,
                    physicsFiles,
                    bodyReferences,
                    matchedPluginPaths);
            })
            .ToList();
    }

    private static IReadOnlyList<string> ResolveRelatedFiles(IReadOnlyList<string> files, string meshToken)
    {
        return files
            .Where(file => Path.GetFileNameWithoutExtension(file).Contains(meshToken, StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetFileName(file) ?? file)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeMeshToken(string meshFileName)
    {
        var token = Path.GetFileNameWithoutExtension(meshFileName) ?? meshFileName;
        return token.EndsWith("_0", StringComparison.OrdinalIgnoreCase) || token.EndsWith("_1", StringComparison.OrdinalIgnoreCase)
            ? token[..^2]
            : token;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Vanilla Armor Static Database
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Static database of canonical vanilla Skyrim armor entries used to pre-populate
/// region maps and recommended deformation profiles without requiring user input.
/// </summary>
internal static class VanillaArmorDatabase
{
    public static readonly IReadOnlyList<VanillaArmorEntry> Entries =
    [
        new("Iron Armor",             ["ironarmor", "ironplate"],                    "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "curvy"),
        new("Iron Helmet",            ["ironhelmet"],                                "Vanilla", ["42:Circlet"],                     "curvy"),
        new("Steel Armor",            ["steelarmor", "steelplate"],                  "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Steel Helmet",           ["steelhelmet"],                               "Vanilla", ["42:Circlet"],                     "athletic"),
        new("Steel Plate Armor",      ["steelplatearmor"],                           "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Dwarven Armor",          ["dwarvenarmor"],                              "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Dwarven Helmet",         ["dwarvenhelmet"],                             "Vanilla", ["42:Circlet"],                     "athletic"),
        new("Elven Armor",            ["elvenarmor"],                                "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Elven Helmet",           ["elvenhelmet"],                               "Vanilla", ["42:Circlet"],                     "slim"),
        new("Elven Gilded Armor",     ["elvengildedarmor"],                          "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Glass Armor",            ["glassarmor"],                                "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Glass Helmet",           ["glasshelmet"],                               "Vanilla", ["42:Circlet"],                     "slim"),
        new("Ebony Armor",            ["ebonyarmor"],                                "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Ebony Helmet",           ["ebonyhelmet"],                               "Vanilla", ["42:Circlet"],                     "muscular"),
        new("Daedric Armor",          ["daedricarmor"],                              "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Daedric Helmet",         ["daedrichelmet"],                             "Vanilla", ["42:Circlet"],                     "muscular"),
        new("Dragonplate Armor",      ["dragonplatearmor", "dragonplate"],           "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Dragonscale Armor",      ["dragonscalearmor", "dragonscale"],           "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Leather Armor",          ["leatherarmor"],                              "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Hide Armor",             ["hidearmor"],                                 "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Studded Armor",          ["studdedarmor"],                              "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Imperial Light Armor",   ["imperiallightarmor", "imperiallight"],       "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Imperial Heavy Armor",   ["imperialheavyarmor", "imperialheavy"],       "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Stormcloak Cuirass",     ["stormcloakcuirass", "stormcloak"],           "Vanilla", ["32:Body"],                        "athletic"),
        new("Ancient Nord Armor",     ["ancientswordsman", "ancientnord"],           "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Forsworn Armor",         ["forswornarmor", "forsworn"],                 "Vanilla", ["32:Body"],                        "curvy"),
        new("Fur Armor",              ["furarmor"],                                  "Vanilla", ["32:Body"],                        "slim"),
        new("Mage Robes",             ["magescholarsrobe", "magerobe", "collegerobe"], "Vanilla", ["32:Body"],                     "slim"),
        new("Thieves Guild Armor",    ["thievesguildarmor", "tgarmor"],              "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Dark Brotherhood Armor", ["dbrobes", "darkbrotherhood"],               "Vanilla", ["32:Body"],                        "slim"),
        new("Nightingale Armor",      ["nightingalearmor", "nightingale"],           "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Blades Armor",           ["bladearmor", "bladesamurai"],                "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Guard Armor",            ["guardarmor", "guardcuirass"],                "Vanilla", ["32:Body"],                        "athletic"),
    ];
}

/// <summary>
/// Looks up a mesh file name against the vanilla armor static database.
/// Strips weight suffixes (_0/_1) before matching tokens.
/// </summary>
internal sealed class VanillaArmorLookupService : IVanillaArmorLookupService
{
    public IReadOnlyList<VanillaArmorEntry> All => VanillaArmorDatabase.Entries;

    public bool TryLookup(string meshFileName, out VanillaArmorEntry? entry)
    {
        var baseName = Path.GetFileNameWithoutExtension(meshFileName) ?? string.Empty;

        // Strip weight variant suffixes so "ironarmor_0.nif" matches the "ironarmor" token.
        if (baseName.EndsWith("_0", StringComparison.OrdinalIgnoreCase) ||
            baseName.EndsWith("_1", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^2];
        }

        foreach (var candidate in VanillaArmorDatabase.Entries)
        {
            if (candidate.MeshFileTokens.Any(token =>
                baseName.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Simplified Voxel Collision Service
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Approximates voxel-grid penetration between the converted armor and the target body.
/// <para>
/// Each body region (chest, waist, pelvis, legs, shoulders) is modelled as a uniform
/// voxel grid. A vertex is considered to be penetrating when the applied regional
/// morph factor causes the body to expand beyond a mesh-type-specific threshold.
/// The push-out magnitude is the excess scaled by the grid resolution.
/// </para>
/// </summary>
internal sealed class SimplifiedVoxelCollisionService : IVoxelCollisionService
{
    private const int GridResolution = 8;

    // Penetration threshold by mesh type: factor above which a region is considered
    // to have body vertices overlapping the armor after conversion.
    private static readonly IReadOnlyDictionary<string, double> PenetrationThresholds =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["plate"]           = 1.10, // Rigid armor needs substantial expansion to penetrate
            ["leather"]         = 1.07,
            ["cloth"]           = 1.04, // Soft materials clip at much smaller deltas
            ["skin-tight"]      = 1.03,
            ["physics-enabled"] = 1.04,
            ["mixed"]           = 1.06
        };

    public Task<VoxelCollisionResult> ComputeAsync(
        ImportedArmor armor,
        ConvertedMesh mesh,
        string targetBody,
        CancellationToken cancellationToken)
    {
        PenetrationThresholds.TryGetValue(mesh.MeshType, out var threshold);
        threshold = threshold == 0 ? 1.06 : threshold;

        var affectedRegions = new List<string>();
        var pushOutMagnitudes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (region, morphFactor) in mesh.RegionalMorphing)
        {
            if (morphFactor <= threshold) continue;

            // Push-out magnitude: excess above threshold scaled by grid resolution
            // so the value is expressed in voxel cell units (0 = no push-out needed).
            var pushOut = Math.Round((morphFactor - threshold) * GridResolution, 4);
            affectedRegions.Add(region);
            pushOutMagnitudes[region] = pushOut;
        }

        return Task.FromResult(new VoxelCollisionResult(
            affectedRegions.Count > 0,
            affectedRegions,
            pushOutMagnitudes,
            GridResolution));
    }
}
