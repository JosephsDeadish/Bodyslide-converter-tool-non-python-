using Bodyslide.Core;

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
}
