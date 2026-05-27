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

        var orchestrator = new ConversionOrchestrator(
            new TestImporter(),
            new TestDetector(),
            new TestAnalyzer(),
            new TestConverter(),
            new TestWeightTransfer(),
            new TestMorphGenerator(),
            new TestPhysicsSupport(),
            exporter);

        var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

        Assert.True(result.Success);
        Assert.Equal(outputDirectory, result.OutputDirectory);
        Assert.Equal(outputDirectory, exporter.ExportPath);
        Assert.Contains(result.Steps, s => s.StartsWith("mesh-converted:", StringComparison.Ordinal));

        File.Delete(inputFile);
    }

    [Fact]
    public async Task ConvertAsync_ThrowsWhenInputDoesNotExist()
    {
        var orchestrator = new ConversionOrchestrator(
            new TestImporter(),
            new TestDetector(),
            new TestAnalyzer(),
            new TestConverter(),
            new TestWeightTransfer(),
            new TestMorphGenerator(),
            new TestPhysicsSupport(),
            new TestExporter());

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            orchestrator.ConvertAsync(new ConversionRequest(Path.Combine(Path.GetTempPath(), "missing-input.nif"), "UNP")));
    }

    private sealed class TestImporter : IArmorImportService
    {
        public Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken) => Task.FromResult(new ImportedArmor(inputPath));
    }

    private sealed class TestDetector : IBodyDetectionService
    {
        public Task<string> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken) => Task.FromResult("CBBE");
    }

    private sealed class TestAnalyzer : IMeshAnalysisService
    {
        public Task<MeshAnalysis> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken) => Task.FromResult(new MeshAnalysis("mixed"));
    }

    private sealed class TestConverter : IMeshConversionService
    {
        public Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) => Task.FromResult(new ConvertedMesh("mixed", "hybrid"));
    }

    private sealed class TestWeightTransfer : IWeightTransferService
    {
        public Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) => Task.FromResult(new WeightedMesh("mixed", "default"));
    }

    private sealed class TestMorphGenerator : IMorphGenerationService
    {
        public Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) => Task.FromResult(new MorphSet("low", "high"));
    }

    private sealed class TestPhysicsSupport : IPhysicsSupportService
    {
        public Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) => Task.FromResult(new PhysicsConfig("smp"));
    }

    private sealed class TestExporter : IExportService
    {
        public string? ExportPath { get; private set; }

        public Task<string> ExportAsync(ConversionRequest request, ConvertedMesh mesh, MorphSet morphs, PhysicsConfig physics, IReadOnlyList<string> steps, CancellationToken cancellationToken)
        {
            ExportPath = request.OutputDirectory ?? throw new InvalidOperationException("Output should be provided for this test.");
            return Task.FromResult(ExportPath);
        }
    }
}
