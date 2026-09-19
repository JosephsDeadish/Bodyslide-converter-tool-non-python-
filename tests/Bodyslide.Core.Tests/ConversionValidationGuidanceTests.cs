using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class ConversionValidationGuidanceTests
{
    [Fact]
    public void BuildFollowUpActions_CoversBodyDetectionAndBodySlideFailureCases()
    {
        var summary = new ConversionValidationSummary(
            "REVIEW",
            62,
            1,
            2,
            0,
            [
                new ConversionValidationIssue("low-body-match", "high", "Body match confidence stayed low."),
                new ConversionValidationIssue("bodyslide-incompatible", "medium", "Generated BodySlide payload is incomplete."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "3BA");

        Assert.Contains(actions, action => action.Contains("source-body override", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("slider export disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFollowUpActions_DeduplicatesSharedPluginMasterChainGuidance()
    {
        var summary = new ConversionValidationSummary(
            "REVIEW",
            54,
            2,
            0,
            0,
            [
                new ConversionValidationIssue("plugin-patch-missing-master-chain", "high", "Generated patch is missing required masters."),
                new ConversionValidationIssue("plugin-patch-master-order-mismatch", "high", "Generated patch master order is wrong."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "CBBE");

        Assert.Single(actions);
        Assert.Contains("*_SlidesmithPatch.esp", actions[0], StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFollowUpActions_CoversPackagingAndZipFailureCases()
    {
        var summary = new ConversionValidationSummary(
            "UNSAFE",
            31,
            2,
            1,
            0,
            [
                new ConversionValidationIssue("missing-staged-mesh-output", "high", "Staged meshes are missing."),
                new ConversionValidationIssue("missing-output-zip", "medium", "Requested output zip was not generated."),
                new ConversionValidationIssue("zip-missing-plugin-patch-report", "medium", "Zip is missing plugin-patches.json."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "HIMBO");

        Assert.Contains(actions, action => action.Contains("dependency-map.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("output-zip enabled", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("armor-pack-validation.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFollowUpActions_CoversTopologyHeelAndLinkedPluginFailureCases()
    {
        var summary = new ConversionValidationSummary(
            "REVIEW",
            47,
            2,
            1,
            0,
            [
                new ConversionValidationIssue("topology-mismatch-risk", "high", "Source and target topology differ too much."),
                new ConversionValidationIssue("heel-offset-review", "medium", "Heel offset needs review."),
                new ConversionValidationIssue("plugin-link-unsupported-nif-layout", "high", "Linked ARMA mesh used an unsupported NIF layout."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "BHUNP");

        Assert.Contains(actions, action => action.Contains("Outfit Studio", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("ground contact", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("linked ARMA meshes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFollowUpActions_DeepensRaceAndUnsupportedNifGuidance()
    {
        var summary = new ConversionValidationSummary(
            "REVIEW",
            39,
            3,
            0,
            0,
            [
                new ConversionValidationIssue("unsupported-nif-layout", "high", "Source mesh used an unsupported NIF layout."),
                new ConversionValidationIssue("race-compatibility-warning", "high", "Race-specific compatibility needs review."),
                new ConversionValidationIssue("plugin-link-unsupported-nif-layout", "high", "Linked ARMA mesh used an unsupported NIF layout."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "Vanilla Beast", maxActions: 6);

        Assert.Contains(actions, action => action.Contains("geometry-family", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("vampire/child", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("world, first-person, female/male", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFollowUpActions_CoversPartialOutputAndMasterChainCases()
    {
        var summary = new ConversionValidationSummary(
            "UNSAFE",
            28,
            3,
            2,
            0,
            [
                new ConversionValidationIssue("plugin-link-partial-family-failure", "high", "Linked family only partially rewrote."),
                new ConversionValidationIssue("plugin-link-unscanned-master-reference", "high", "Missing masters prevented linked ARMA verification."),
                new ConversionValidationIssue("missing-bodyslide-reference-nif", "medium", "Reference NIF was not written."),
                new ConversionValidationIssue("missing-preview-workbench", "medium", "Workbench preview is missing."),
                new ConversionValidationIssue("zip-missing-staged-mesh-output", "medium", "Zip is missing staged meshes."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "3BA", maxActions: 8);

        Assert.Contains(actions, action => action.Contains("linked armor family", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("full master chain", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("ShapeData", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("preview-workbench.html", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("mod managers install", StringComparison.OrdinalIgnoreCase));
    }
}
