using System.Text.Json;

namespace Bodyslide.Core.Tests;

public sealed class RegressionFailureMatrixTests
{
    [Fact]
    public void Build_GroupsAliasesAndCountsEachIssueOncePerSample()
    {
        var first = Sample("3BBB") with { IssueCodes = ["missing-mesh", "MISSING-MESH"] };
        var second = Sample("3BA");
        var report = RegressionFailureMatrix.Build("Caliente's Beautiful Bodies Enhancer", [first, second]);
        var cell = Assert.Single(report.Cells, static cell => cell.IssueCode == "missing-mesh");

        Assert.Equal("3ba", cell.SourceBody);
        Assert.Equal("cbbe", cell.TargetBody);
        Assert.Equal(2, cell.Count);
        Assert.Equal(["sample-0001", "sample-0002"], cell.SampleIds);
        Assert.Equal(2, report.SamplesRequiringReview);
    }

    [Fact]
    public void Build_ReadySampleHasNoFailureCells()
    {
        var item = Sample("CBBE") with
        {
            IssueCodes = [],
            ValidationStatus = "ready",
            SupportTier = "mainstream-automatic"
        };
        var report = RegressionFailureMatrix.Build("CBBE", [item]);

        Assert.Equal(1, report.SampleCount);
        Assert.Equal(0, report.SamplesRequiringReview);
        Assert.Empty(report.Cells);
    }

    [Theory]
    [InlineData(false, false, false, "conversion-failed")]
    [InlineData(true, true, false, "manual-cleanup-required")]
    [InlineData(true, false, true, "runtime-verification-required")]
    public void Build_PreservesReviewRequirementsWithoutIssueCodes(bool success, bool cleanup, bool runtime, string expectedCode)
    {
        var item = Sample(null) with
        {
            Success = success,
            IssueCodes = [],
            ManualCleanupLikely = cleanup,
            RuntimeVerificationRequired = runtime
        };
        var report = RegressionFailureMatrix.Build("CBBE", [item]);

        Assert.Contains(report.Cells, cell => cell.IssueCode == expectedCode);
        Assert.All(report.Cells, cell => Assert.Equal("unknown", cell.SourceBody));
    }

    [Fact]
    public void Build_DoesNotCopyPackPathsMeshNamesOrIssueMessages()
    {
        var report = RegressionFailureMatrix.Build("CBBE", [Sample("CBBE")]);
        var serialized = JsonSerializer.Serialize(report);

        Assert.DoesNotContain("private-pack", serialized);
        Assert.DoesNotContain("private-mesh", serialized);
        Assert.DoesNotContain("private-message", serialized);
    }

    [Fact]
    public void Build_SameBodyIssueWithDifferentFrameworks_ProducesSeparateCells()
    {
        var first = Sample("CBBE") with { OutputDirectory = "/sample/one" };
        var second = Sample("CBBE") with { OutputDirectory = "/sample/two" };
        var proofs = new[]
        {
            Proof(first.OutputDirectory, "digitigrade-beast", "soft-body"),
            Proof(second.OutputDirectory, "ube-extended", "static")
        };

        var report = RegressionFailureMatrix.Build("CBBE", [first, second], proofs);
        var cells = report.Cells.Where(static cell => cell.IssueCode == "missing-mesh").ToArray();

        Assert.Equal(2, cells.Length);
        Assert.Contains(cells, static cell => cell.SkeletonFamily == "digitigrade-beast" && cell.PhysicsMode == "soft-body");
        Assert.Contains(cells, static cell => cell.SkeletonFamily == "ube-extended" && cell.PhysicsMode == "static");
        Assert.All(cells, static cell =>
        {
            Assert.Equal("oral-or-genital", cell.TopologyFamily);
            Assert.Equal("linked-family", cell.PluginFamily);
            Assert.Equal(1, cell.Count);
        });
    }

    [Fact]
    public void Build_MissingProof_IsExplicitlyUnknown()
    {
        var report = RegressionFailureMatrix.Build("CBBE", [Sample("CBBE")]);

        Assert.All(report.Cells, static cell =>
        {
            Assert.Equal("unknown", cell.SkeletonFamily);
            Assert.Equal("unknown", cell.PhysicsMode);
            Assert.Equal("unknown", cell.TopologyFamily);
            Assert.Equal("unknown", cell.PluginFamily);
        });
    }

    private static ConversionMatrixPackProofItem Proof(string outputDirectory, string skeleton, string physics) =>
        new("private-mesh.nif", outputDirectory, "CBBE", "female", "advanced-review-required",
            "private-coordinate-key",
            [$"source-skeleton-family:{skeleton}", $"runtime-physics:{physics}", "topology-family:oral-or-genital", "plugin-family:linked-family"],
            "partial", "planned-only", false, ["runtime"], ["private-gap-message"]);

    private static ArmorPackValidationItem Sample(string? body) =>
        new("private-mesh.nif", "/private-pack/output", true, "needs-review", 75,
            ["missing-mesh"], ["private-message"],
            DetectedSourceBody: body, MeshType: "plate", SupportTier: "advanced-review-required");
}
