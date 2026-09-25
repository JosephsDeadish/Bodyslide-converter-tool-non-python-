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
    public void DesktopSmokeTestContract_EmitsRequiredStatusAndCountFields()
    {
        var json = DesktopSmokeTestContract.Serialize(
            "SlideSmith Desktop",
            DesktopSmokeTestContract.ExpectedPresetCount,
            DesktopSmokeTestContract.ExpectedTargetCount,
            DesktopSmokeTestContract.ExpectedProfileCount,
            DesktopSmokeTestContract.ExpectedPhysicsCount,
            DesktopSmokeTestContract.ExpectedDesktopTabCount);

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(DesktopSmokeTestContract.ReadyStatus, root.GetProperty("status").GetString());
        Assert.Equal("SlideSmith Desktop", root.GetProperty("title").GetString());
        Assert.Equal(DesktopSmokeTestContract.ExpectedPresetCount, root.GetProperty("presets").GetInt32());
        Assert.Equal(DesktopSmokeTestContract.ExpectedTargetCount, root.GetProperty("targets").GetInt32());
        Assert.Equal(DesktopSmokeTestContract.ExpectedProfileCount, root.GetProperty("profiles").GetInt32());
        Assert.Equal(DesktopSmokeTestContract.ExpectedPhysicsCount, root.GetProperty("physics").GetInt32());
        Assert.Equal(DesktopSmokeTestContract.ExpectedDesktopTabCount, root.GetProperty("tabs").GetInt32());
        Assert.Equal(DesktopSmokeTestContract.ExpectedDesktopTabCount, root.GetProperty("expectedTabs").GetInt32());
    }

    [Fact]
    public void DesktopSmokeTestContract_ManualSerialize_UsesMismatchStatusWhenTabsDoNotMatchExpectedDesktopLayout()
    {
        var json = DesktopSmokeTestContract.Serialize(
            "SlideSmith Desktop",
            DesktopSmokeTestContract.ExpectedPresetCount,
            DesktopSmokeTestContract.ExpectedTargetCount,
            DesktopSmokeTestContract.ExpectedProfileCount,
            DesktopSmokeTestContract.ExpectedPhysicsCount,
            DesktopSmokeTestContract.ExpectedDesktopTabCount - 1);

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(DesktopSmokeTestContract.LayoutMismatchStatus, root.GetProperty("status").GetString());
        Assert.Equal(DesktopSmokeTestContract.ExpectedDesktopTabCount - 1, root.GetProperty("tabs").GetInt32());
        Assert.Equal(DesktopSmokeTestContract.ExpectedDesktopTabCount, root.GetProperty("expectedTabs").GetInt32());
    }

    [Fact]
    public void DesktopSmokeTestContract_ManualSerialize_UsesMismatchStatusWhenCountsDoNotMatchExpectedDesktopOptions()
    {
        var json = DesktopSmokeTestContract.Serialize(
            "SlideSmith Desktop",
            DesktopSmokeTestContract.ExpectedPresetCount,
            DesktopSmokeTestContract.ExpectedTargetCount - 1,
            DesktopSmokeTestContract.ExpectedProfileCount,
            DesktopSmokeTestContract.ExpectedPhysicsCount,
            DesktopSmokeTestContract.ExpectedDesktopTabCount);

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(DesktopSmokeTestContract.LayoutMismatchStatus, root.GetProperty("status").GetString());
        Assert.Equal(DesktopSmokeTestContract.ExpectedTargetCount - 1, root.GetProperty("targets").GetInt32());
        Assert.Equal(DesktopSmokeTestContract.ExpectedDesktopTabCount, root.GetProperty("tabs").GetInt32());
    }

    [Fact]
    public void DesktopSmokeTestContract_CreateReady_UsesCatalogCountsAndExpectedDesktopTabCount()
    {
        const int expectedDesktopTabs = DesktopSmokeTestContract.ExpectedDesktopTabCount;

        var payload = DesktopSmokeTestContract.CreateReady("SlideSmith Desktop", expectedDesktopTabs);
        var json = DesktopSmokeTestContract.Serialize(payload);

        Assert.Equal(DesktopSmokeTestContract.ReadyStatus, payload.Status);
        Assert.Equal("SlideSmith Desktop", payload.Title);
        Assert.Equal(PresetCatalog.All.Count, payload.Presets);
        Assert.Equal(BodyTypeCatalog.All.Count, payload.Targets);
        Assert.Equal(DeformationProfileModifier.All.Count, payload.Profiles);
        Assert.Equal(PhysicsProfileCatalog.All.Count, payload.Physics);
        Assert.Equal(expectedDesktopTabs, payload.Tabs);
        Assert.Equal(expectedDesktopTabs, payload.ExpectedTabs);

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(PresetCatalog.All.Count, root.GetProperty("presets").GetInt32());
        Assert.Equal(BodyTypeCatalog.All.Count, root.GetProperty("targets").GetInt32());
        Assert.Equal(DeformationProfileModifier.All.Count, root.GetProperty("profiles").GetInt32());
        Assert.Equal(PhysicsProfileCatalog.All.Count, root.GetProperty("physics").GetInt32());
        Assert.Equal(expectedDesktopTabs, root.GetProperty("tabs").GetInt32());
        Assert.Equal(expectedDesktopTabs, root.GetProperty("expectedTabs").GetInt32());
    }

    [Fact]
    public void DesktopSmokeTestContract_Create_UsesMismatchStatusWhenTabsDoNotMatchExpectedDesktopLayout()
    {
        var payload = DesktopSmokeTestContract.Create("SlideSmith Desktop", DesktopSmokeTestContract.ExpectedDesktopTabCount - 1);

        Assert.Equal(DesktopSmokeTestContract.LayoutMismatchStatus, payload.Status);
        Assert.Equal(DesktopSmokeTestContract.ExpectedDesktopTabCount - 1, payload.Tabs);
        Assert.Equal(DesktopSmokeTestContract.ExpectedDesktopTabCount, payload.ExpectedTabs);
    }

    [Fact]
    public void DesktopSmokeTestContract_Create_UsesMismatchStatusWhenCountsDoNotMatchExpectedDesktopLayout()
    {
        var payload = DesktopSmokeTestContract.Create(
            "SlideSmith Desktop",
            DesktopSmokeTestContract.ExpectedPresetCount,
            DesktopSmokeTestContract.ExpectedTargetCount,
            DesktopSmokeTestContract.ExpectedProfileCount - 1,
            DesktopSmokeTestContract.ExpectedPhysicsCount,
            DesktopSmokeTestContract.ExpectedDesktopTabCount);

        Assert.Equal(DesktopSmokeTestContract.LayoutMismatchStatus, payload.Status);
        Assert.Equal(DesktopSmokeTestContract.ExpectedProfileCount - 1, payload.Profiles);
        Assert.Equal(DesktopSmokeTestContract.ExpectedDesktopTabCount, payload.Tabs);
    }

    [Fact]
    public void DesktopSmokeTestContract_Create_UsesMismatchStatusWhenOptionNamesDoNotMatchExpectedCatalog()
    {
        var presets = PresetCatalog.All.Select(static preset => preset.Name).ToArray();
        var targets = BodyTypeCatalog.All.Select(static body => body.Name).ToArray();
        var profiles = DeformationProfileModifier.All.ToArray();
        var physics = PhysicsProfileCatalog.All
            .Select(PhysicsProfileCatalog.ToDisplayName)
            .ToArray();

        targets[0] = "DefinitelyNotARealTarget";

        var payload = DesktopSmokeTestContract.Create(
            "SlideSmith Desktop",
            presets,
            targets,
            profiles,
            physics,
            DesktopSmokeTestContract.ExpectedDesktopTabCount);

        Assert.Equal(DesktopSmokeTestContract.LayoutMismatchStatus, payload.Status);
        Assert.Equal(BodyTypeCatalog.All.Count, payload.Targets);
    }

    [Fact]
    public void DesktopSmokeTestContract_Create_UsesMismatchStatusWhenOptionListContainsDuplicateInsteadOfFullCatalog()
    {
        var presets = PresetCatalog.All.Select(static preset => preset.Name).ToArray();
        var targets = BodyTypeCatalog.All.Select(static body => body.Name).ToArray();
        var profiles = DeformationProfileModifier.All.ToArray();
        var physics = PhysicsProfileCatalog.All
            .Select(PhysicsProfileCatalog.ToDisplayName)
            .ToArray();

        physics[^1] = physics[0];

        var payload = DesktopSmokeTestContract.Create(
            "SlideSmith Desktop",
            presets,
            targets,
            profiles,
            physics,
            DesktopSmokeTestContract.ExpectedDesktopTabCount);

        Assert.Equal(DesktopSmokeTestContract.LayoutMismatchStatus, payload.Status);
        Assert.Equal(PhysicsProfileCatalog.All.Count, payload.Physics);
    }

    [Fact]
    public void FixtureBodyProfiles_ParseAndMatchTheirBodySlideOutputPaths()
    {
        var fixtureRoot = GetFixtureRoot();
        foreach (var profilePath in Directory.EnumerateFiles(fixtureRoot, "*.slidesmith-body.json", SearchOption.AllDirectories))
        {
            using var profile = System.Text.Json.JsonDocument.Parse(File.ReadAllText(profilePath));
            if (!profile.RootElement.TryGetProperty("bodyOutputPath", out var bodyOutputPathElement))
            {
                continue;
            }

            var sliderSetDirectory = Path.Combine(Path.GetDirectoryName(profilePath)!, "CalienteTools", "BodySlide", "SliderSets");
            if (!Directory.Exists(sliderSetDirectory))
            {
                continue;
            }

            var ospPaths = Directory.EnumerateFiles(sliderSetDirectory, "*.osp", SearchOption.TopDirectoryOnly).ToArray();
            Assert.NotEmpty(ospPaths);

            var bodyOutputPath = bodyOutputPathElement.GetString();
            Assert.False(string.IsNullOrWhiteSpace(bodyOutputPath));

            foreach (var ospPath in ospPaths)
            {
                var ospText = File.ReadAllText(ospPath);
                var match = System.Text.RegularExpressions.Regex.Match(ospText, "<OutputPath>(.*?)</OutputPath>");
                Assert.True(match.Success, $"Missing OutputPath in {ospPath}.");
                Assert.Equal(
                    match.Groups[1].Value,
                    bodyOutputPath,
                    ignoreCase: true);
            }
        }
    }

    [Fact]
    public void BuildRaceCompatibilityStep_PreservesWarningOnlyCases()
    {
        var step = LocalExportService.BuildRaceCompatibilityStep(new RaceCompatibilityReport(
            true,
            ["Argonian variant uses Vanilla Beast compatibility and still needs runtime review."],
            []));

        Assert.StartsWith("race-compat:warnings=", step, StringComparison.Ordinal);
        Assert.Contains("Argonian variant", step, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("race-compat:ok", step, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequiresMixedModStackValidation_IgnoresMeshOnlyExoticTargetsWithoutPluginOrRaceEvidence()
    {
        Assert.False(LocalExportService.RequiresMixedModStackValidation(
            pluginAnalysis: null,
            raceCompatibility: null,
            targetBody: "Equine Humanoid"));

        Assert.True(LocalExportService.RequiresMixedModStackValidation(
            new PluginAnalysisResult(
                ScannedPlugins: ["equineHarness.esp"],
                ArmorAddons: [],
                PatchGuidance: string.Empty),
            raceCompatibility: null,
            targetBody: "Equine Humanoid"));
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
                new ConversionValidationIssue("physics-profile-unsupported", "high", "Requested physics profile is not supported by the target body."),
                new ConversionValidationIssue("physics-config-mismatch", "medium", "Required runtime physics configs were not generated."),
                new ConversionValidationIssue("physics-bone-missing", "high", "Required target physics bones were missing."),
                new ConversionValidationIssue("physics-bone-remap", "medium", "Physics chains were remapped."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "Vanilla Beast", maxActions: 6);
        var artifacts = ConversionValidationGuidance.BuildReviewArtifacts(summary, maxArtifacts: 6);

        Assert.Contains(actions, action => action.Contains("set Physics to None", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("missing runtime config outputs", StringComparison.OrdinalIgnoreCase));
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
    public void BuildFollowUpActions_CoversAmbiguousPluginLayoutCases()
    {
        var summary = new ConversionValidationSummary(
            "REVIEW",
            41,
            1,
            1,
            0,
            [
                new ConversionValidationIssue("plugin-ambiguous-layout", "medium", "A plugin used an ambiguous ESL/ESPFE layout."),
                new ConversionValidationIssue("plugin-link-unscanned-master-reference", "high", "Missing masters prevented linked ARMA verification."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "CBBE", maxActions: 6);
        var artifacts = ConversionValidationGuidance.BuildReviewArtifacts(summary, maxArtifacts: 6);

        Assert.Contains(actions, action => action.Contains("ESPFE", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("xEdit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("full master chain", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("plugin-patches.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("patch-armor.pas", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetFixtureRoot([System.Runtime.CompilerServices.CallerFilePath] string currentFilePath = "")
    {
        return Path.Combine(
            Path.GetDirectoryName(currentFilePath)!,
            "Fixtures");
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
    public void BuildFollowUpActions_CoversExtremeTopologyAdaptationArtifacts()
    {
        var summary = new ConversionValidationSummary(
            "REVIEW",
            36,
            1,
            1,
            0,
            [
                new ConversionValidationIssue("extreme-topology-adaptation", "medium", "Reused morphs needed extreme structural adaptation."),
                new ConversionValidationIssue("retargeted-morph-reuse", "low", "Source deltas were reused through retargeting."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "3BA", maxActions: 6);
        var artifacts = ConversionValidationGuidance.GetIssueReviewArtifacts(
            new ConversionValidationIssue("extreme-topology-adaptation", "medium", "Reused morphs needed extreme structural adaptation."));

        Assert.Contains(actions, action => action.Contains("morphs.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("split parts", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("straps", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("morphs.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Contains("ShapeData", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildFollowUpActions_CoversCompoundHardcaseCertaintyArtifacts()
    {
        var summary = new ConversionValidationSummary(
            "UNSAFE",
            24,
            2,
            0,
            0,
            [
                new ConversionValidationIssue("compound-hardcase-certainty", "high", "Topology drift, custom-rig uncertainty, and mixed plugin/load-order context overlap."),
                new ConversionValidationIssue("unsafe-skeleton-remap", "high", "Automatic skeleton remap safety is unsafe.")
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "Alien Hybrid", maxActions: 6);
        var artifacts = ConversionValidationGuidance.GetIssueReviewArtifacts(
            new ConversionValidationIssue("compound-hardcase-certainty", "high", "Topology drift, custom-rig uncertainty, and mixed plugin/load-order context overlap."));

        Assert.Contains(actions, action => action.Contains("topology-correspondence.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("mod-stack-cross-validation.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("hardest-case", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("topology-correspondence.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("mod-stack-cross-validation.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("preview-workbench.html", StringComparison.OrdinalIgnoreCase));
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
    public void BuildFollowUpActions_CoversMissingFomodMetadataAndRootSupportFiles()
    {
        var summary = new ConversionValidationSummary(
            "UNSAFE",
            24,
            1,
            2,
            0,
            [
                new ConversionValidationIssue("missing-fomod-module-config", "medium", "fomod/ModuleConfig.xml was not generated."),
                new ConversionValidationIssue("missing-fomod-info", "medium", "fomod/info.xml was not generated."),
                new ConversionValidationIssue("missing-root-support-file", "medium", "A root support report is missing."),
                new ConversionValidationIssue("zip-missing-root-support-file", "medium", "The distributable ZIP is missing a root support report."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "Spriggan", maxActions: 8);
        var artifacts = ConversionValidationGuidance.BuildReviewArtifacts(summary, maxArtifacts: 8);

        Assert.Contains(actions, action => action.Contains("fomod/ModuleConfig.xml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("fomod/info.xml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("root support report", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actions, action => action.Contains("armor-pack-validation.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("fomod/ModuleConfig.xml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("fomod/info.xml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("armor-pack-validation.json", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void ConversionValidationPresentation_BuildDesktopUiLabels_ReflectGateAndPreviewState()
    {
        Assert.Equal("Preview (REVIEW REQUIRED)", ConversionValidationPresentation.BuildDesktopResultTabTitle("Preview", "needs-review"));
        Assert.Equal("Next actions", ConversionValidationPresentation.BuildDesktopResultTabTitle("Next actions", null));
        Assert.Contains("Preview", ConversionValidationPresentation.BuildDesktopStatusLabel("needs-review", previewAvailable: true), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("preview missing", ConversionValidationPresentation.BuildDesktopStatusLabel("needs-review", previewAvailable: false), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("smoke test", ConversionValidationPresentation.BuildDesktopStatusLabel("ready", previewAvailable: true), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blocking conversion issues", ConversionValidationPresentation.BuildDesktopStatusLabel("high-risk", previewAvailable: false), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildReviewArtifacts_AndActions_IncludeInGameValidationForRuntimeSensitiveIssues()
    {
        var summary = new ConversionValidationSummary(
            "needs-review",
            44,
            2,
            2,
            0,
            [
                new ConversionValidationIssue("pose-risk", "high", "Combat poses still clip."),
                new ConversionValidationIssue("physics-bone-missing", "high", "A target physics chain is missing."),
                new ConversionValidationIssue("race-compatibility-warning", "medium", "Custom beast race needs review."),
                new ConversionValidationIssue("heel-offset-review", "medium", "Heel placement needs review."),
            ]);

        var actions = ConversionValidationGuidance.BuildFollowUpActions(summary, "Equine Humanoid", maxActions: 8);
        var artifacts = ConversionValidationGuidance.BuildReviewArtifacts(summary, maxArtifacts: 10);

        Assert.Contains(actions, action => action.Contains("in-game-validation.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("in-game-validation.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("world-physics.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(artifacts, artifact => artifact.Equals("skeleton-compatibility.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InGameValidationGuidance_BuildDesktopGuidanceEntries_IncludeChecklistAndScenarioHighlights()
    {
        var report = new InGameValidationReport(
            "UBE",
            "needs-review",
            "REVIEW REQUIRED",
            ["breasts", "belly", "thighs"],
            ["mouth", "tongue", "throat"],
            ["preview-workbench.html", "in-game-validation.json"],
            "experimental-manual-cleanup",
            new ConversionReadinessAssessment(
                "experimental-manual-cleanup",
                "UBE currently fits the experimental/manual-cleanup tier.",
                CanConvert: true,
                CanPhysicsConvert: true,
                CanSafelyAnimate: false,
                SkeletonReliability: "review",
                SkeletonRemapSafety: "unsafe",
                TargetBodySupportReliability: "review",
                RecommendedReleaseGate: "manual-cleanup"),
            ManualCleanupLikely: true,
            RuntimeVerificationRequired: true,
            Caveats:
            [
                "Radically different topology can still need manual Outfit Studio cleanup before release.",
                "Generated validation guidance improves review, but it is not a substitute for real in-game/runtime checks."
            ],
            TopologyCorrespondence: new TopologyCorrespondenceReport(
                "heuristic-heavy",
                0.42,
                true,
                "heuristic-island-regional",
                false,
                "partial-anchor-backed",
                "partial-anchor-guided",
                "UBE",
                1,
                1,
                2,
                1,
                6,
                ["mouth:HDT Jaw"],
                ["thighs"],
                true,
                "oral-genital-subpieces",
                false,
                ["Topology matching remains heuristic."],
                ["topology-mismatch-risk"],
                ["belly", "thighs"],
                ["Open preview-workbench.html before trusting correspondence."]),
            ScenarioMatrix:
            [
                new InGameValidationScenario(
                    "Oral articulation sweep",
                    "Action",
                    "Extended oral/throat topology requires articulation checks for mouth, tongue, throat",
                    ["talk / phoneme", "open mouth"],
                    ["mouth", "tongue", "throat"],
                    ["preview-workbench.html"]),
                new InGameValidationScenario(
                    "Lower-body compression sweep",
                    "High",
                    "Lower-body morphing or hotspot evidence was detected for belly, thighs",
                    ["crouch", "sit"],
                    ["belly", "thighs"],
                    ["pose-simulation-report.json"])
            ],
            Checklist:
            [
                new InGameValidationCheckpoint(
                    "Body fit smoke test",
                    "Action",
                    "Equip the converted outfit and validate core fit.",
                    ["breasts", "belly", "thighs"],
                    ["conversion-quality.json"]),
                new InGameValidationCheckpoint(
                    "Sensitive topology pass",
                    "Action",
                    "Inspect oral motion and collision.",
                    ["mouth", "tongue"],
                    ["skeleton-compatibility.json"])
            ]);

        var entries = InGameValidationGuidance.BuildDesktopGuidanceEntries(report, maxChecklistItems: 2, maxScenarioItems: 2);

        Assert.Contains(entries, entry => entry.Details.Contains("Runtime smoke-test gate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => entry.Details.Contains("Body fit smoke test", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => entry.Details.Contains("Oral articulation sweep", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => entry.Details.Contains("Lower-body compression sweep", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => entry.Details.Contains("not a substitute for real in-game/runtime checks", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, entry => entry.Priority.Equals("High", StringComparison.OrdinalIgnoreCase));
    }
}
