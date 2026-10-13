using Bodyslide.Core;
using System.Text.Json;

namespace Bodyslide.Core.Tests;

public sealed class ExternalProofReadinessTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("missing-proof-report")]
    [InlineData("planned-only")]
    [InlineData("executed-pass")]
    [InlineData("executed-incomplete")]
    public void ArmorPackStatus_DowngradesReadyUntilAllExternalProofPasses(string? proofStatus)
    {
        var status = BatchConversionRunner.DeriveArmorPackValidationStatus(
            validationStatus: "ready",
            supportTier: "mainstream-automatic",
            manualCleanupLikely: false,
            runtimeVerificationRequired: false,
            canSafelyAnimate: true,
            proofExecutionStatus: proofStatus);

        Assert.Equal("needs-review", status);
    }

    [Fact]
    public void ArmorPackStatus_PreservesReadyOnlyForCompletedExternalProof()
    {
        var status = BatchConversionRunner.DeriveArmorPackValidationStatus(
            validationStatus: "ready",
            supportTier: "mainstream-automatic",
            manualCleanupLikely: false,
            runtimeVerificationRequired: false,
            canSafelyAnimate: true,
            proofExecutionStatus: "executed-complete");

        Assert.Equal("ready", status);
    }

    [Theory]
    [InlineData("high-risk", "planned-only", "high-risk")]
    [InlineData("needs-review", "planned-only", "needs-review")]
    public void ArmorPackStatus_DoesNotDowngradeExistingMoreSevereStatus(
        string validationStatus,
        string proofStatus,
        string expectedStatus)
    {
        var status = BatchConversionRunner.DeriveArmorPackValidationStatus(
            validationStatus,
            supportTier: "mainstream-automatic",
            manualCleanupLikely: false,
            runtimeVerificationRequired: false,
            canSafelyAnimate: true,
            proofExecutionStatus: proofStatus);

        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public void ExternalCompatibilityProof_RequiresBuildInspectionAndDeformationObservations()
    {
        var proof = ExternalProofHarnessSupport.BuildExternalCompatibilityProof(
            "1.2",
            [
                CreateObservation("bodyslide-build", "proof-evidence/validation/bodyslide-build/build.log"),
                CreateObservation("output-inspection", "proof-evidence/validation/output-inspection/inspection.txt")
            ]);

        Assert.False(proof.StrictProofSatisfied);
        Assert.Equal("executed-incomplete", proof.ExecutedStatus);
        Assert.Contains("observation:deformation-observation:missing-or-duplicate", proof.MissingItems);
    }

    [Fact]
    public void ExternalCompatibilityProof_RejectsAutomatedInspectionAsDeformationEvidence()
    {
        var proof = ExternalProofHarnessSupport.BuildExternalCompatibilityProof(
            "1.2",
            [
                CreateObservation("bodyslide-build", "proof-evidence/validation/bodyslide-build/build.log"),
                CreateObservation("output-inspection", "proof-evidence/validation/output-inspection/inspection.txt"),
                CreateObservation("deformation-observation", "proof-evidence/validation/output-inspection/automated-report.json")
            ]);

        Assert.False(proof.StrictProofSatisfied);
        Assert.Equal("executed-incomplete", proof.ExecutedStatus);
        Assert.Contains("observation:deformation-observation:categorized-evidence", proof.MissingItems);
    }

    [Fact]
    public void ExternalCompatibilityProof_AcceptsOnlyVersionedCategorizedObservations()
    {
        var proof = ExternalProofHarnessSupport.BuildExternalCompatibilityProof(
            "1.2",
            [
                CreateObservation("bodyslide-build", "proof-evidence/validation/bodyslide-build/build.log"),
                CreateObservation("output-inspection", "proof-evidence/validation/output-inspection/inspection.txt"),
                CreateObservation("deformation-observation", "proof-evidence/validation/deformation-observation/observation.txt")
            ]);

        Assert.True(proof.StrictProofSatisfied);
        Assert.Equal("executed-pass", proof.ExecutedStatus);
        Assert.Empty(proof.MissingItems);
    }

    [Fact]
    public void ExternalCompatibilityProof_RejectsPassingObservationsWithoutDetails()
    {
        var proof = ExternalProofHarnessSupport.BuildExternalCompatibilityProof(
            "1.2",
            [
                CreateObservation("bodyslide-build", "proof-evidence/validation/bodyslide-build/build.log") with { Details = null },
                CreateObservation("output-inspection", "proof-evidence/validation/output-inspection/inspection.txt"),
                CreateObservation("deformation-observation", "proof-evidence/validation/deformation-observation/observation.txt")
            ]);

        Assert.False(proof.StrictProofSatisfied);
        Assert.Contains("observation:bodyslide-build:required-details", proof.MissingItems);
    }

    private static ImportedProofValidationObservation CreateObservation(string type, string evidencePath) =>
        new(
            type,
            "pass",
            "ExternalValidationTool",
            "1.0",
            "2026-10-09T12:00:00Z",
            type is "bodyslide-build" or "output-inspection"
                ? [evidencePath, "output.nif"]
                : [evidencePath],
            [],
            JsonSerializer.SerializeToElement<object>(type switch
            {
                "bodyslide-build" => new
                {
                    OutfitName = "TestOutfit",
                    PresetName = "TestPreset",
                    Arguments = new[] { "--build", "TestOutfit" },
                    TargetDirectoryWasEmpty = true,
                    ExitCode = 0,
                    Outputs = new[] { ProofFile("output.nif") }
                },
                "output-inspection" => new
                {
                    InspectedFiles = new[] { ProofFile("output.nif") },
                    SourceAndOutputHashesCompared = true,
                    ShapeTargetsVerified = true,
                    OsdRecordsVerified = true,
                    VertexIndicesVerified = true,
                    Findings = Array.Empty<string>()
                },
                "deformation-observation" => new
                {
                    ShapeName = "Body",
                    SliderName = "TestSlider",
                    LowEndpointValue = 0,
                    HighEndpointValue = 100,
                    LowEndpointDeformedTarget = true,
                    HighEndpointDeformedTarget = true,
                    LowEndpointEvidence = evidencePath,
                    HighEndpointEvidence = evidencePath
                },
                _ => new { }
            }));

    private static object ProofFile(string path) => new
    {
        Path = path,
        SizeBytes = 1,
        Sha256 = new string('a', 64)
    };
}
