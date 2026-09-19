using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class ConversionValidationGuidanceTests
{
    [Theory]
    [InlineData("ready", "PASS", "Install-ready")]
    [InlineData("needs-review", "REVIEW REQUIRED", "Review required before install/share")]
    [InlineData("high-risk", "FAIL", "Do not install/share yet")]
    public void ConversionValidationPresentation_UsesClearGateMessaging(string status, string expectedGate, string expectedMessageFragment)
    {
        Assert.Equal(expectedGate, ConversionValidationPresentation.GetGateLabel(status));
        Assert.Contains(expectedMessageFragment, ConversionValidationPresentation.GetDispositionMessage(status), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConversionValidationPresentation_BuildOutcomeSummary_UsesGateCountsAndPreviewHint()
    {
        var summary = new ConversionValidationSummary(
            "needs-review",
            52,
            1,
            2,
            3,
            []);

        var withPreview = ConversionValidationPresentation.BuildOutcomeSummary(summary, previewAvailable: true);
        Assert.Contains("REVIEW REQUIRED", withPreview, StringComparison.Ordinal);
        Assert.Contains("Open Preview for the final visual pass", withPreview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 high, 2 medium, 3 low", withPreview, StringComparison.OrdinalIgnoreCase);

        var withoutPreview = ConversionValidationPresentation.BuildOutcomeSummary("high-risk", 2, 1, 0, previewAvailable: false);
        Assert.Contains("FAIL", withoutPreview, StringComparison.Ordinal);
        Assert.Contains("Preview files are missing", withoutPreview, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 high, 1 medium, 0 low", withoutPreview, StringComparison.OrdinalIgnoreCase);
    }

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
    public void BuildFollowUpActions_CoversMissingAcceptanceAndPluginHandoffArtifacts()
    {
        var summary = new ConversionValidationSummary(
            "UNSAFE",
            26,
            3,
            2,
            0,
            [
                new ConversionValidationIssue("missing-readme", "medium", "README.txt was not generated."),
                new ConversionValidationIssue("missing-dependency-map", "medium", "dependency-map.json was not generated."),
                new ConversionValidationIssue("missing-race-compatibility-report", "medium", "race-compatibility.json was not generated."),
                new ConversionValidationIssue("missing-pose-report", "low", "pose-simulation-report.json was not generated."),
                new ConversionValidationIssue("missing-world-physics-report", "low", "world-physics.json was not generated."),
                new ConversionValidationIssue("missing-plugin-patch-report", "medium", "plugin-patches.json was not generated."),
                new ConversionValidationIssue("missing-xedit-script", "medium", "patch-armor.pas was not generated."),
                new ConversionValidationIssue("missing-root-plugin", "high", "The root plugin file is missing."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "3BA", maxActions: 8);

        Assert.Contains(actions, action => action.Contains("README.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("dependency-map.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("race-compatibility.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("pose-simulation-report.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("world-physics.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("plugin-patches.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("patch-armor.pas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("package root", StringComparison.OrdinalIgnoreCase));
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
    public void BuildFollowUpActions_CoversPhysicsCapabilityMismatchAndRemapCases()
    {
        var summary = new ConversionValidationSummary(
            "REVIEW",
            44,
            1,
            1,
            0,
            [
                new ConversionValidationIssue("physics-bone-missing", "high", "Required target physics bones were missing."),
                new ConversionValidationIssue("physics-bone-remap", "medium", "Physics chains were remapped."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "Vanilla Beast", maxActions: 6);
        var artifacts = ConversionValidationGuidance.BuildReviewArtifacts(summary, maxArtifacts: 6);

        Assert.Contains(actions, action => action.Contains("requested physics profile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("remapped physics chains", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("skeleton-compatibility.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("world-physics.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFollowUpActions_CoversPartitionLossAndAmbiguousPluginTieCases()
    {
        var summary = new ConversionValidationSummary(
            "REVIEW",
            43,
            2,
            1,
            0,
            [
                new ConversionValidationIssue("missing-source-partitions", "high", "Source slot signal was dropped."),
                new ConversionValidationIssue("missing-plugin-partitions", "medium", "Plugin slot signal was dropped."),
                new ConversionValidationIssue("plugin-rewrite-ambiguous-filename", "high", "Plugin mesh path matched multiple source meshes."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "3BA", maxActions: 6);

        Assert.Contains(actions, action => action.Contains("BSDismember partitions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("BOD2/BODT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("trailing path context", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void BuildValidationPreviewPanelHtml_UsesPassReviewFailLabels()
    {
        var exportServiceType = typeof(ConversionOrchestrator).Assembly.GetType("Bodyslide.Core.LocalExportService");
        Assert.NotNull(exportServiceType);

        var method = exportServiceType!.GetMethod(
            "BuildValidationPreviewPanelHtml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        static string Render(System.Reflection.MethodInfo methodInfo, string status)
        {
            var summary = new ConversionValidationSummary(status, 70, status.Equals("high-risk", StringComparison.OrdinalIgnoreCase) ? 1 : 0, 0, 0, []);
            return Assert.IsType<string>(methodInfo.Invoke(null, [summary, "3BA"]));
        }

        var readyHtml = Render(method!, "ready");
        Assert.Contains("PASS", readyHtml, StringComparison.Ordinal);
        Assert.Contains("Install-ready after one final preview pass", readyHtml, StringComparison.OrdinalIgnoreCase);

        var reviewHtml = Render(method, "needs-review");
        Assert.Contains("REVIEW REQUIRED", reviewHtml, StringComparison.Ordinal);
        Assert.Contains("Review required before install/share", reviewHtml, StringComparison.OrdinalIgnoreCase);

        var failHtml = Render(method, "high-risk");
        Assert.Contains("FAIL", failHtml, StringComparison.Ordinal);
        Assert.Contains("Do not install/share yet", failHtml, StringComparison.OrdinalIgnoreCase);
    }
}
