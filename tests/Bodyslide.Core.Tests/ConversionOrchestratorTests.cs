using Bodyslide.Core;
using System.IO.Compression;

namespace Bodyslide.Core.Tests;

public sealed class ConversionOrchestratorTests
{
    [Fact]
    public async Task ConvertAsync_RunsFullPipelineAndExports()
    {
        var inputFile = Path.GetTempFileName();
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var exporter = new TestExporter();
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var orchestrator = BuildTestOrchestrator(exporter);
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            Assert.Equal(outputDirectory, result.OutputDirectory);
            Assert.Equal(outputDirectory, exporter.ExportPath);
            Assert.Contains(result.Steps, s => s.StartsWith("cage:", StringComparison.Ordinal));
            Assert.Contains(result.Steps, s => s.StartsWith("clipping:", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputFile);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_ThrowsWhenInputDoesNotExist()
    {
        var orchestrator = BuildTestOrchestrator();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            orchestrator.ConvertAsync(new ConversionRequest(Path.Combine(Path.GetTempPath(), "missing-input.nif"), "UNP")));
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_ThrowsWhenOutputDirectoryIsInvalid()
    {
        var inputFile = Path.GetTempFileName();

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            await Assert.ThrowsAnyAsync<Exception>(() =>
                orchestrator.ConvertAsync(new ConversionRequest(inputFile, "UNP", "invalid\0path")));
        }
        finally
        {
            File.Delete(inputFile);
        }
    }

    [Fact]
    public void Normalize_UsesPresetTargetBody()
    {
        var request = new ConversionRequest("/tmp/in.nif", string.Empty, Preset: "3BA Curvy");
        var normalized = RequestNormalizer.Normalize(request);

        Assert.Equal("3BA", normalized.Request.TargetBody);
        Assert.NotNull(normalized.Preset);
        Assert.Equal("smp+cbpc", normalized.Preset!.PhysicsProfile);
    }

    [Fact]
    public async Task BatchRunner_ConvertsAllNifsInDirectory()
    {
        var inputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(inputDirectory, "armor_one.nif"), "mesh");
            await File.WriteAllTextAsync(Path.Combine(inputDirectory, "armor_two.nif"), "mesh");

            var runner = new BatchConversionRunner(BuildTestOrchestrator(new TestExporter()));
            var results = await runner.ConvertAsync(new ConversionRequest(inputDirectory, "CBBE", outputDirectory));

            Assert.Equal(2, results.Count);
            Assert.All(results, result => Assert.StartsWith(outputDirectory, result.OutputDirectory, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BatchRunner_ConvertsAllNifsInZipArchive()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var sourceDirectory = Path.Combine(workingDirectory, "source");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var zipPath = Path.Combine(workingDirectory, "mod-pack.zip");
        Directory.CreateDirectory(sourceDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "armor_one.nif"), "mesh");
            await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "armor_two.nif"), "mesh");
            ZipFile.CreateFromDirectory(sourceDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            var runner = new BatchConversionRunner(BuildTestOrchestrator(new TestExporter()));
            var results = await runner.ConvertAsync(new ConversionRequest(zipPath, "CBBE", outputDirectory));

            Assert.Equal(2, results.Count);
            Assert.All(results, result => Assert.StartsWith(outputDirectory, result.OutputDirectory, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesConversionLearningCache()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var inputFile = Path.Combine(inputDirectory, "cuirass_3ba.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var cachePath = Path.Combine(outputDirectory, ".conversion-learning-cache.json");
            Assert.True(File.Exists(cachePath));
            var cacheContent = await File.ReadAllTextAsync(cachePath);
            Assert.Contains("\"TargetBody\": \"3BA\"", cacheContent, StringComparison.Ordinal);
            Assert.Contains("\"RegionalMorphing\"", cacheContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesDependencyMap()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var mesh0 = Path.Combine(inputDirectory, "cuirass_0.nif");
        var mesh1 = Path.Combine(inputDirectory, "cuirass_1.nif");
        await File.WriteAllTextAsync(mesh0, "mesh");
        await File.WriteAllTextAsync(mesh1, "mesh");
        await File.WriteAllBytesAsync(Path.Combine(inputDirectory, "cuirass.dds"), [0x44, 0x44, 0x53, 0x20]);
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, "cuirass.xml"), "<physics/>");
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, "cuirass_reference.tri"), "ref");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputDirectory, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var dependencyMapPath = Path.Combine(outputDirectory, "dependency-map.json");
            Assert.True(File.Exists(dependencyMapPath));

            var mapContent = await File.ReadAllTextAsync(dependencyMapPath);
            Assert.Contains("\"Mesh\": \"cuirass_0.nif\"", mapContent, StringComparison.Ordinal);
            Assert.Contains("\"Mesh\": \"cuirass_1.nif\"", mapContent, StringComparison.Ordinal);
            Assert.Contains("cuirass.dds", mapContent, StringComparison.Ordinal);
            Assert.Contains("cuirass.xml", mapContent, StringComparison.Ordinal);
            Assert.Contains("cuirass_reference.tri", mapContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_StepsIncludeSkeletonAndPartitions()
    {
        var inputFile = Path.GetTempFileName();
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var orchestrator = BuildTestOrchestrator(new TestExporter());
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.StartsWith("skeleton:", StringComparison.Ordinal));
            Assert.Contains(result.Steps, s => s.StartsWith("partitions:", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputFile);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithOutputZip_ProducesZipFile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory, OutputZip: true));

            Assert.True(result.Success);
            var zipPath = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
            Assert.True(File.Exists(zipPath), $"Expected ZIP at {zipPath}");
            Assert.Single(result.OutputFiles);
            Assert.EndsWith(".zip", result.OutputFiles[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_SkeletonStepReportsMapping()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var skeletonStep = result.Steps.FirstOrDefault(s => s.StartsWith("skeleton:", StringComparison.Ordinal));
            Assert.NotNull(skeletonStep);
            Assert.Contains("-mapped", skeletonStep, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_PartitionStepRebuildsSlots()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass_plate.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var partitionsStep = result.Steps.FirstOrDefault(s => s.StartsWith("partitions:", StringComparison.Ordinal));
            Assert.NotNull(partitionsStep);
            Assert.NotEqual("partitions:unchanged", partitionsStep, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("HIMBO")]
    [InlineData("UNP")]
    [InlineData("BHUNP")]
    [InlineData("TBD")]
    [InlineData("SAM")]
    [InlineData("SOS")]
    [InlineData("UBE")]
    public void BodyTransformationFieldCatalog_ResolvesAllKnownBodies(string targetBody)
    {
        var inputFile = Path.GetTempFileName();

        try
        {
            var normalized = RequestNormalizer.Normalize(new ConversionRequest(inputFile, targetBody));
            Assert.Equal(targetBody, normalized.Request.TargetBody);
        }
        finally
        {
            File.Delete(inputFile);
        }
    }

    [Fact]
    public void PresetCatalog_ContainsExpandedPresets()
    {
        var presets = PresetCatalog.All.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("BHUNP Curvy", presets);
        Assert.Contains("HIMBO Muscular", presets);
        Assert.Contains("3BA Slim", presets);
        Assert.Contains("UNP Athletic", presets);
    }

    [Fact]
    public async Task ConvertAsync_StepsIncludeBodySlideStep()
    {
        var inputFile = Path.GetTempFileName();
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var orchestrator = BuildTestOrchestrator(new TestExporter());
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.StartsWith("bodyslide:", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputFile);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDeformationProfile_StepRecordsProfile()
    {
        var inputFile = Path.GetTempFileName();
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var orchestrator = BuildTestOrchestrator(new TestExporter());
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory, DeformationProfile: "curvy"));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.Equals("deformation-profile:curvy", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputFile);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesOspFile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var ospFile = Directory.GetFiles(outputDirectory, "*.osp").FirstOrDefault();
            Assert.NotNull(ospFile);
            var ospContent = await File.ReadAllTextAsync(ospFile);
            Assert.Contains("<SliderSetInfo", ospContent, StringComparison.Ordinal);
            Assert.Contains("<Slider name=", ospContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("curvy", 1.15)]
    [InlineData("slim", 0.82)]
    [InlineData("petite", 0.75)]
    [InlineData("muscular", 1.25)]
    public void DeformationProfileModifier_ScalesDeltaCorrectly(string profile, double amplifier)
    {
        // Base chest delta = 0.08 (value 1.08 - 1.0).  After applying amplifier: 1 + (0.08 * amplifier).
        var field = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.08 };
        var result = DeformationProfileModifier.Apply(field, profile);

        var expected = 1.0 + (0.08 * amplifier);
        Assert.Equal(expected, result["chest"], precision: 10);
    }

    [Fact]
    public void DeformationProfileModifier_UnknownProfile_ReturnsUnchangedField()
    {
        var field = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.08 };
        var result = DeformationProfileModifier.Apply(field, "unknown-profile");
        Assert.Equal(1.08, result["chest"]);
    }

    [Fact]
    public void DeformationProfileModifier_All_ContainsExpectedProfiles()
    {
        var profiles = DeformationProfileModifier.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("curvy", profiles);
        Assert.Contains("slim", profiles);
        Assert.Contains("petite", profiles);
        Assert.Contains("muscular", profiles);
        Assert.Contains("lean", profiles);
        Assert.Contains("athletic", profiles);
    }

    [Fact]
    public async Task BodySlideOspProjectService_GeneratesValidXmlForFemaleBody()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var ospFile = Directory.GetFiles(outputDirectory, "*.osp").FirstOrDefault();
            Assert.NotNull(ospFile);
            var ospXml = await File.ReadAllTextAsync(ospFile);
            Assert.Contains("BreastsPhysics", ospXml, StringComparison.Ordinal);
            Assert.Contains("femalebody_0.nif", ospXml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BodySlideOspProjectService_GeneratesValidXmlForMaleBody()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "HIMBO", outputDirectory));

            Assert.True(result.Success);
            var ospFile = Directory.GetFiles(outputDirectory, "*.osp").FirstOrDefault();
            Assert.NotNull(ospFile);
            var ospXml = await File.ReadAllTextAsync(ospFile);
            Assert.Contains("Pecs", ospXml, StringComparison.Ordinal);
            Assert.Contains("malebody_0.nif", ospXml, StringComparison.Ordinal);
            Assert.Contains("malebody_1.nif", ospXml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static ConversionOrchestrator BuildTestOrchestrator(IExportService? exporter = null) =>
        new(
            new TestImporter(),
            new TestDetector(),
            new TestAnalyzer(),
            new TestCageGenerator(),
            new TestConverter(),
            new TestWeightTransfer(),
            new TestSkeletonMapper(),
            new TestMorphGenerator(),
            new TestPartitionRebuilder(),
            new TestClippingDetector(),
            new TestAutoCorrection(),
            new TestPhysicsSupport(),
            new TestBodySlideProjectService(),
            new TestTextureAnalysisService(),
            new TestPluginAnalysisService(),
            new TestVanillaArmorLookup(),
            new TestVoxelCollision(),
            exporter ?? new TestExporter());

    private sealed class TestImporter : IArmorImportService
    {
        public Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken) =>
            Task.FromResult(new ImportedArmor(inputPath, [inputPath], [], [], []));
    }

    private sealed class TestDetector : IBodyDetectionService
    {
        public Task<BodyDetectionReport> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken) =>
            Task.FromResult(new BodyDetectionReport("CBBE", 0.99, ["mesh:100%"]));
    }

    private sealed class TestAnalyzer : IMeshAnalysisService
    {
        public Task<MeshAnalysis> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken) =>
            Task.FromResult(new MeshAnalysis("mixed", false, 1));
    }

    private sealed class TestCageGenerator : ICageGenerationService
    {
        public Task<DeformationCage> BuildAsync(MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new DeformationCage("hybrid-cage"));
    }

    private sealed class TestConverter : IMeshConversionService
    {
        public Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, string? deformationProfile, CancellationToken cancellationToken) =>
            Task.FromResult(new ConvertedMesh("mixed", "hybrid", 1, new Dictionary<string, double> { { "chest", 1.0 } }));
    }

    private sealed class TestWeightTransfer : IWeightTransferService
    {
        public Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new WeightedMesh("mixed", "default", false));
    }

    private sealed class TestSkeletonMapper : ISkeletonMappingService
    {
        public Task<SkeletonMappingResult> MapAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new SkeletonMappingResult("xpmsse-vanilla", "xpmsse-vanilla", [], []));
    }

    private sealed class TestMorphGenerator : IMorphGenerationService
    {
        public Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new MorphSet("low", "high", true));
    }

    private sealed class TestPartitionRebuilder : IPartitionRebuildingService
    {
        public Task<PartitionRebuildingResult> RebuildAsync(WeightedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new PartitionRebuildingResult(true, ["32:Body"], []));
    }

    private sealed class TestClippingDetector : IClippingDetectionService
    {
        public Task<ClippingReport> DetectAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new ClippingReport(false, ["thighs"], ["pose-simulation"]));
    }

    private sealed class TestAutoCorrection : IAutoCorrectionService
    {
        public Task<CorrectionResult> CorrectAsync(ConvertedMesh mesh, ClippingReport clipping, CancellationToken cancellationToken) =>
            Task.FromResult(new CorrectionResult(false, "none"));
    }

    private sealed class TestPhysicsSupport : IPhysicsSupportService
    {
        public Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, string physicsProfile, CancellationToken cancellationToken) =>
            Task.FromResult(new PhysicsConfig(physicsProfile));
    }

    private sealed class TestBodySlideProjectService : IBodySlideProjectService
    {
        public Task<BodySlideProject> GenerateAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySlideProject("TestArmor", targetBody, ["Belly", "Butt"], "<SliderSetInfo />"));
    }

    private sealed class TestTextureAnalysisService : ITextureAnalysisService
    {
        public Task<TextureSummary> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken) =>
            Task.FromResult(new TextureSummary(0, [], [], []));
    }

    private sealed class TestPluginAnalysisService : IPluginAnalysisService
    {
        public Task<PluginAnalysisResult> AnalyzeAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new PluginAnalysisResult([], [], "no plugins"));
    }

    private sealed class TestVanillaArmorLookup : IVanillaArmorLookupService
    {
        public IReadOnlyList<VanillaArmorEntry> All => [];

        public bool TryLookup(string meshFileName, out VanillaArmorEntry? entry)
        {
            entry = null;
            return false;
        }
    }

    private sealed class TestVoxelCollision : IVoxelCollisionService
    {
        public Task<VoxelCollisionResult> ComputeAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new VoxelCollisionResult(false, [], new Dictionary<string, double>(), 8));
    }

    private sealed class TestExporter : IExportService
    {
        public string? ExportPath { get; private set; }

        public Task<(string OutputDirectory, IReadOnlyList<string> OutputFiles)> ExportAsync(
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
            ExportPath = request.OutputDirectory ?? throw new InvalidOperationException("Output should be provided for this test.");
            Directory.CreateDirectory(ExportPath);
            return Task.FromResult<(string, IReadOnlyList<string>)>((ExportPath, []));
        }
    }
}

public sealed class VanillaArmorLookupTests
{
    [Fact]
    public void TryLookup_KnownIronArmorMesh_ReturnsEntry()
    {
        var service = new VanillaArmorLookupService();

        var found = service.TryLookup("ironarmor_0.nif", out var entry);

        Assert.True(found);
        Assert.NotNull(entry);
        Assert.Equal("Iron Armor", entry!.Name);
        Assert.Equal("Vanilla", entry.SourceBody);
        Assert.Contains("32:Body", entry.RegionSlots);
    }

    [Fact]
    public void TryLookup_KnownIronArmorMesh_WeightSuffixStripped()
    {
        var service = new VanillaArmorLookupService();

        var found0 = service.TryLookup("ironarmor_0.nif", out var entry0);
        var found1 = service.TryLookup("ironarmor_1.nif", out var entry1);

        Assert.True(found0);
        Assert.True(found1);
        Assert.Equal(entry0!.Name, entry1!.Name);
    }

    [Fact]
    public void TryLookup_UnknownMesh_ReturnsFalse()
    {
        var service = new VanillaArmorLookupService();

        var found = service.TryLookup("modded_fancy_armor.nif", out var entry);

        Assert.False(found);
        Assert.Null(entry);
    }

    [Fact]
    public void All_ContainsCoreVanillaArmors()
    {
        var service = new VanillaArmorLookupService();
        var names = service.All.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Iron Armor",      names);
        Assert.Contains("Daedric Armor",   names);
        Assert.Contains("Glass Armor",     names);
        Assert.Contains("Nightingale Armor", names);
        Assert.Contains("Mage Robes",      names);
    }

    [Theory]
    [InlineData("steelarmor_0.nif",       "Steel Armor")]
    [InlineData("daedricarmor_0.nif",     "Daedric Armor")]
    [InlineData("glassarmor_1.nif",       "Glass Armor")]
    [InlineData("nightingalearmor_0.nif", "Nightingale Armor")]
    public void TryLookup_KnownArmorVariants_ReturnsCorrectEntry(string meshFile, string expectedName)
    {
        var service = new VanillaArmorLookupService();

        var found = service.TryLookup(meshFile, out var entry);

        Assert.True(found);
        Assert.Equal(expectedName, entry!.Name);
    }
}

public sealed class VoxelCollisionTests
{
    [Fact]
    public async Task ComputeAsync_PhysicsEnabledMeshWithHighMorphs_DetectsPenetrations()
    {
        var service = new SimplifiedVoxelCollisionService();
        var armor = new ImportedArmor("/tmp/armor.nif", ["/tmp/armor.nif"], [], [], []);
        // cloth mesh with chest morph above the soft threshold of 1.04
        var mesh = new ConvertedMesh("cloth", "cage+shrinkwrap", 1,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.09, ["waist"] = 0.95 });

        var result = await service.ComputeAsync(armor, mesh, "3BA", CancellationToken.None);

        Assert.True(result.HasPenetrations);
        Assert.Contains("chest", result.AffectedRegions);
        Assert.True(result.PushOutMagnitudes["chest"] > 0);
    }

    [Fact]
    public async Task ComputeAsync_PlateArmorWithLowMorphs_NoPenetrations()
    {
        var service = new SimplifiedVoxelCollisionService();
        var armor = new ImportedArmor("/tmp/armor.nif", ["/tmp/armor.nif"], [], [], []);
        // plate mesh; all morphs below the plate threshold of 1.10
        var mesh = new ConvertedMesh("plate", "cage+rigid-islands", 1,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.04, ["shoulders"] = 1.06 });

        var result = await service.ComputeAsync(armor, mesh, "CBBE", CancellationToken.None);

        Assert.False(result.HasPenetrations);
        Assert.Empty(result.AffectedRegions);
    }

    [Fact]
    public async Task ComputeAsync_ReturnsExpectedGridResolution()
    {
        var service = new SimplifiedVoxelCollisionService();
        var armor = new ImportedArmor("/tmp/armor.nif", ["/tmp/armor.nif"], [], [], []);
        var mesh = new ConvertedMesh("mixed", "hybrid", 1, new Dictionary<string, double>());

        var result = await service.ComputeAsync(armor, mesh, "UNP", CancellationToken.None);

        Assert.Equal(8, result.GridResolution);
    }

    [Fact]
    public async Task ComputeAsync_PushOutMagnitude_ScalesWithExcess()
    {
        var service = new SimplifiedVoxelCollisionService();
        var armor = new ImportedArmor("/tmp/armor.nif", ["/tmp/armor.nif"], [], [], []);
        // cloth threshold is 1.04; morphFactor = 1.08 → excess = 0.04 → pushOut = 0.04 * 8 = 0.32
        var mesh = new ConvertedMesh("cloth", "cage+shrinkwrap", 1,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.08 });

        var result = await service.ComputeAsync(armor, mesh, "3BA", CancellationToken.None);

        Assert.True(result.HasPenetrations);
        Assert.True(result.PushOutMagnitudes["chest"] > 0 && result.PushOutMagnitudes["chest"] < 1.0);
    }
}

public sealed class PhysicsXmlTests
{
    [Fact]
    public async Task BuildAsync_CbpcProfile_GeneratesCbpcXml()
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh("cloth", "default", true);

        var config = await service.BuildAsync(mesh, "3BA", "smp+cbpc", CancellationToken.None);

        Assert.NotNull(config.CbpcConfigXml);
        Assert.Contains("<CBPCConfig", config.CbpcConfigXml, StringComparison.Ordinal);
        Assert.Contains("<BreastPhysics>", config.CbpcConfigXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_SmpProfile_GeneratesSmpXml()
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh("cloth", "default", true);

        var config = await service.BuildAsync(mesh, "3BA", "smp+cbpc", CancellationToken.None);

        Assert.NotNull(config.SmpConfigXml);
        Assert.Contains("<system name=", config.SmpConfigXml, StringComparison.Ordinal);
        Assert.Contains("NPC L Breast01", config.SmpConfigXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_MaleBody_GeneratesMalePecXml()
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh("mixed", "default", false);

        var config = await service.BuildAsync(mesh, "HIMBO", "smp", CancellationToken.None);

        Assert.NotNull(config.SmpConfigXml);
        Assert.Contains("NPC L Pec", config.SmpConfigXml, StringComparison.Ordinal);
        Assert.DoesNotContain("NPC L Breast01", config.SmpConfigXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_CbpcOnlyProfile_NoSmpXml()
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh("cloth", "default", true);

        var config = await service.BuildAsync(mesh, "UNP", "cbpc", CancellationToken.None);

        Assert.NotNull(config.CbpcConfigXml);
        Assert.Null(config.SmpConfigXml);
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesCbpcAndSmpFiles()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cloth_robe.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "cbpc-config.xml")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "smp-config.xml")));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class VanillaArmorPipelineTests
{
    [Fact]
    public async Task ConvertAsync_WithDefaultModules_VanillaLookupStepPresent()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "ironarmor_0.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var vanillaStep = result.Steps.FirstOrDefault(s => s.StartsWith("vanilla-armor:", StringComparison.Ordinal));
            Assert.NotNull(vanillaStep);
            Assert.DoesNotContain("unknown", vanillaStep, StringComparison.Ordinal);
            Assert.Contains("Iron Armor", vanillaStep, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_UnknownArmorReportsUnknown()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "modded_custom_gear.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.Equals("vanilla-armor:unknown", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_VoxelCollisionStepPresent()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.StartsWith("voxel-collision:", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}
