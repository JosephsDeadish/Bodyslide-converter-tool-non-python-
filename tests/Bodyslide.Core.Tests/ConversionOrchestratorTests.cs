using Bodyslide.Core;

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
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
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
            new TestMorphGenerator(),
            new TestClippingDetector(),
            new TestAutoCorrection(),
            new TestPhysicsSupport(),
            exporter ?? new TestExporter());

    private sealed class TestImporter : IArmorImportService
    {
        public Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken) =>
            Task.FromResult(new ImportedArmor(inputPath, [inputPath], [], []));
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
        public Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new ConvertedMesh("mixed", "hybrid", 1, new Dictionary<string, double> { ["chest"] = 1.0 }));
    }

    private sealed class TestWeightTransfer : IWeightTransferService
    {
        public Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new WeightedMesh("mixed", "default", false));
    }

    private sealed class TestMorphGenerator : IMorphGenerationService
    {
        public Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new MorphSet("low", "high", true));
    }

    private sealed class TestClippingDetector : IClippingDetectionService
    {
        public Task<ClippingReport> DetectAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new ClippingReport(false, ["thighs"]));
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
            IReadOnlyList<string> steps,
            CancellationToken cancellationToken)
        {
            ExportPath = request.OutputDirectory ?? throw new InvalidOperationException("Output should be provided for this test.");
            Directory.CreateDirectory(ExportPath);
            return Task.FromResult<(string, IReadOnlyList<string>)>((ExportPath, []));
        }
    }
}
