using System.Runtime.CompilerServices;
using System.Text.Json;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class FailingPackReadinessTests
{
    [Theory]
    [InlineData("RealisticFailureModPack", "3BA", true, "unsupported-nif-layout")]
    [InlineData("RealisticFailureNiLinesModPack", "3BA", true, "unsupported-nif-layout")]
    [InlineData("RealisticFailureCrossPluginLinkedTieModPack", "CBBE", false, "plugin-rewrite-ambiguous-filename")]
    [InlineData("RealisticFemaleOralTopologyModPack", "UBE", false, "topology-mismatch-risk")]
    public async Task ExistingRedistributableFailurePacks_PreserveReviewGatesAndArtifacts(
        string fixtureName, string targetBody, bool batch, string expectedCode)
    {
        using var workspace = new FixtureWorkspace(fixtureName);
        var outputDirectory = Path.Combine(workspace.Root, "output");
        var request = new ConversionRequest(workspace.Root, targetBody, outputDirectory);
        var orchestrator = StandaloneConversionModules.CreateDefault();
        IReadOnlyList<ConversionResult> results = batch
            ? await new BatchConversionRunner(orchestrator).ConvertAsync(request)
            : [await orchestrator.ConvertAsync(request)];

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.True(result.Success));
        var reports = new List<ConversionQualityReport>();
        foreach (var result in results)
        {
            foreach (var artifact in new[]
                     {
                         "conversion-quality.json", "conversion-manifest.json", "README.txt",
                         "preview.html", "preview-workbench.html", "in-game-validation.json",
                         "skeleton-compatibility.json"
                     })
            {
                AssertArtifact(result.OutputDirectory, artifact);
            }

            var report = JsonSerializer.Deserialize<ConversionQualityReport>(
                await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory, "conversion-quality.json")));
            Assert.NotNull(report);
            Assert.NotNull(report.ValidationSummary);
            Assert.Equal("embedded-built-in-catalog", report.TargetBodySupport!.MetadataProvenance);
            Assert.Equal("configured-not-externally-verified", report.TargetBodySupport.MetadataVerificationStatus);
            Assert.False(report.TargetBodySupport.IsExternallyVerified);
            reports.Add(report);
        }

        var affectedReports = reports.Where(report =>
            report.ValidationSummary!.Issues.Any(issue => issue.Code == expectedCode)).ToArray();
        Assert.True(affectedReports.Length > 0,
            $"Expected {expectedCode}; emitted: {string.Join(", ", reports.SelectMany(report => report.ValidationSummary!.Issues).Select(issue => issue.Code))}");
        Assert.All(affectedReports, report =>
        {
            Assert.Contains(report.ValidationSummary!.Status, new[] { "needs-review", "high-risk" });
            Assert.Contains(report.SupportTier, new[] { "advanced-review-required", "experimental-manual-cleanup" });
        });

        if (expectedCode == "plugin-rewrite-ambiguous-filename")
        {
            AssertArtifact(outputDirectory, "plugin-patches.json");
            using var patches = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(outputDirectory, "plugin-patches.json")));
            Assert.NotEmpty(patches.RootElement.GetProperty("UnresolvedTieGroups").EnumerateArray());
        }

        if (expectedCode == "topology-mismatch-risk")
        {
            AssertArtifact(outputDirectory, "topology-correspondence.json");
            Assert.All(affectedReports, report =>
            {
                Assert.True(report.TopologyMismatchRisk);
                Assert.True(report.ManualCleanupLikely);
                Assert.True(report.RuntimeVerificationRequired);
                Assert.Contains(report.ValidationSummary!.Issues, issue => issue.Code == "manual-cleanup-likely");
            });
        }

        if (!batch)
        {
            return;
        }

        AssertArtifact(outputDirectory, "armor-pack-validation.json");
        AssertArtifact(outputDirectory, "regression-failure-matrix.json");
        var validation = JsonSerializer.Deserialize<ArmorPackValidationReport>(
            await File.ReadAllTextAsync(Path.Combine(outputDirectory, "armor-pack-validation.json")));
        var matrix = JsonSerializer.Deserialize<RegressionFailureMatrixReport>(
            await File.ReadAllTextAsync(Path.Combine(outputDirectory, "regression-failure-matrix.json")));
        Assert.NotNull(validation);
        Assert.NotNull(matrix);
        Assert.Contains(validation.PackReadinessStatus, new[] { "needs-review", "high-risk" });
        Assert.Equal(results.Count, matrix.SampleCount);
        Assert.True(matrix.SamplesRequiringReview > 0);
        Assert.Contains(matrix.Cells, cell => cell.IssueCode == expectedCode && cell.Count > 0);

        var expectedMatrix = RegressionFailureMatrix.Build(targetBody, validation.Items);
        Assert.Equal(JsonSerializer.Serialize(expectedMatrix), JsonSerializer.Serialize(matrix));
    }

    private static void AssertArtifact(string outputDirectory, string artifact)
    {
        var path = Path.Combine(outputDirectory, artifact);
        Assert.True(File.Exists(path), $"Missing artifact: {path}");
        Assert.True(new FileInfo(path).Length > 0, $"Empty artifact: {path}");
    }

    private sealed class FixtureWorkspace : IDisposable
    {
        public string Root { get; }

        public FixtureWorkspace(string fixtureName, [CallerFilePath] string currentFilePath = "")
        {
            var testDirectory = Path.GetDirectoryName(currentFilePath)!;
            var source = Path.Combine(testDirectory, "Fixtures", fixtureName);
            Root = Path.Combine(Path.GetTempPath(), "SlideSmith.Tests", "FailingPackReadiness", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(Root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
