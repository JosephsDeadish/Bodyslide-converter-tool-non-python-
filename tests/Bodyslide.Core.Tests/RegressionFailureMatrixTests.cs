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

    private static ArmorPackValidationItem Sample(string? body) =>
        new("private-mesh.nif", "/private-pack/output", true, "needs-review", 75,
            ["missing-mesh"], ["private-message"],
            DetectedSourceBody: body, MeshType: "plate", SupportTier: "advanced-review-required");
}
