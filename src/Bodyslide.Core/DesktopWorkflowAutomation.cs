using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record DesktopWorkflowSummaryRow(string Property, string Value);
internal sealed record DesktopWorkflowReportMetric(string ReportName, string Property, string Value, string FilePath);
internal sealed record DesktopWorkflowArtifact(string Name, string DisplayPath, string FullPath);
internal sealed record DesktopWorkflowAutomationStep(string Area, string Action, string ExpectedSignal, string? ArtifactPath, bool Blocking);
internal sealed record DesktopAutomationContract(
    string Coverage,
    bool RequiresManualWinFormsInteraction,
    bool RequiresWindowsHost,
    bool RequiresWebViewRuntimeForEmbeddedPreview,
    bool SupportsAutomatedWebViewInteraction,
    bool SupportsTrueUiEndToEndAutomation,
    IReadOnlyList<string> LimitationNotes);
internal sealed record DesktopWorkflowValidationState(
    string PreviewTabTitle,
    string GuidanceTabTitle,
    string StatusLabel,
    string OutcomeSummary,
    string? EffectiveStatus,
    bool PreviewAvailable);
internal sealed record DesktopWorkflowAutomationSnapshot(
    IReadOnlyList<DesktopWorkflowSummaryRow> SummaryRows,
    IReadOnlyList<DesktopWorkflowReportMetric> ReportMetrics,
    IReadOnlyList<DesktopWorkflowArtifact> Artifacts,
    IReadOnlyList<DesktopWorkflowAutomationStep> SuggestedGuiFlow,
    DesktopWorkflowValidationState ValidationState,
    DesktopAutomationContract AutomationContract);

internal static class DesktopWorkflowAutomation
{
    public static DesktopWorkflowAutomationSnapshot BuildFromResults(
        IReadOnlyList<ConversionResult> results,
        string? previewPath)
    {
        var outputDirectories = results
            .Select(result => result.OutputDirectory)
            .Where(static directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var summaryRows = BuildSummaryRows(results);
        var reportMetrics = BuildReportMetrics(outputDirectories, FindCommonDirectory(outputDirectories));
        var artifacts = BuildArtifacts(
            results.SelectMany(result => result.OutputFiles).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            FindCommonDirectory(outputDirectories));
        var validationState = BuildValidationState(outputDirectories, previewPath, reportMetrics);
        var automationContract = BuildAutomationContract(validationState);
        return new DesktopWorkflowAutomationSnapshot(summaryRows, reportMetrics, artifacts, BuildSuggestedGuiFlow(reportMetrics, artifacts, validationState, automationContract), validationState, automationContract);
    }

    public static DesktopWorkflowAutomationSnapshot BuildFromOutputDirectory(
        string? outputDirectory,
        string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            var emptyValidationState = BuildValidationState([], previewPath, []);
            var emptyContract = BuildAutomationContract(emptyValidationState);
            return new DesktopWorkflowAutomationSnapshot([], [], [], [], emptyValidationState, emptyContract);
        }

        var files = Directory
            .EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var reportMetrics = BuildReportMetrics([outputDirectory], outputDirectory);
        var validationState = BuildValidationState([outputDirectory], previewPath, reportMetrics);
        var automationContract = BuildAutomationContract(validationState);
        return new DesktopWorkflowAutomationSnapshot(
            [],
            reportMetrics,
            BuildArtifacts(files, outputDirectory),
            BuildSuggestedGuiFlow(reportMetrics, BuildArtifacts(files, outputDirectory), validationState, automationContract),
            validationState,
            automationContract);
    }

    public static DesktopWorkflowValidationState BuildValidationState(
        IReadOnlyList<string> outputDirectories,
        string? previewPath) =>
        BuildValidationState(outputDirectories, previewPath, BuildReportMetrics(outputDirectories, FindCommonDirectory(outputDirectories)));

    private static IReadOnlyList<DesktopWorkflowSummaryRow> BuildSummaryRows(IReadOnlyList<ConversionResult> results)
    {
        var rows = new List<DesktopWorkflowSummaryRow>
        {
            new("Items converted", results.Count.ToString())
        };

        foreach (var result in results)
        {
            if (results.Count > 1)
            {
                rows.Add(new(string.Empty, string.Empty));
                rows.Add(new("Output", result.OutputDirectory));
            }

            foreach (var step in result.Steps)
            {
                if (TryMapSummaryStep(step, out var row))
                {
                    rows.Add(row);
                }
            }

            rows.Add(new("Output files", result.OutputFiles.Count.ToString()));
        }

        return rows;
    }

    private static bool TryMapSummaryStep(string step, out DesktopWorkflowSummaryRow row)
    {
        static bool TryCreate(string stepValue, string prefix, string label, out DesktopWorkflowSummaryRow created)
        {
            if (stepValue.StartsWith(prefix, StringComparison.Ordinal))
            {
                created = new DesktopWorkflowSummaryRow(label, stepValue[prefix.Length..]);
                return true;
            }

            created = default!;
            return false;
        }

        if (TryCreate(step, "detected-body:", "Detected body", out row) ||
            TryCreate(step, "source-body-override:", "Source body (override)", out row) ||
            TryCreate(step, "cross-gender-conversion:", "Cross-gender conversion", out row) ||
            TryCreate(step, "mesh-type:", "Mesh type", out row) ||
            TryCreate(step, "cage:", "Cage mode", out row) ||
            TryCreate(step, "mesh-converted:", "Conversion strategy", out row) ||
            TryCreate(step, "physics:", "Physics profile", out row) ||
            TryCreate(step, "physics-override:", "Physics (override)", out row) ||
            TryCreate(step, "world-mode-override:", "World drop mode (override)", out row) ||
            TryCreate(step, "skeleton:", "Skeleton mapping", out row) ||
            TryCreate(step, "skeleton-warnings:", "Skeleton warnings", out row) ||
            TryCreate(step, "morphs:", "Morphs", out row) ||
            TryCreate(step, "clipping:", "Clipping", out row) ||
            TryCreate(step, "correction:", "Auto-correction", out row) ||
            TryCreate(step, "correction-applied:", "Correction regions", out row) ||
            TryCreate(step, "voxel-collision:", "Voxel collision", out row) ||
            TryCreate(step, "voxel-push-applied:", "Voxel push-out", out row) ||
            TryCreate(step, "weights:", "Weight profile", out row) ||
            TryCreate(step, "weight-solver:", "Weight solver", out row) ||
            TryCreate(step, "physics-injection:", "Physics bone injection", out row) ||
            TryCreate(step, "bodyslide:", "BodySlide project", out row) ||
            TryCreate(step, "regions:", "Armor regions", out row) ||
            TryCreate(step, "rigid-islands:", "Rigid islands", out row) ||
            TryCreate(step, "normals:", "Normal recalc", out row) ||
            TryCreate(step, "partitions:", "Partitions", out row) ||
            TryCreate(step, "biped-slots-passthrough:", "Biped slots (plugin)", out row) ||
            TryCreate(step, "pose-simulation:", "Pose simulation", out row) ||
            TryCreate(step, "plugins:", "Plugins", out row) ||
            TryCreate(step, "vanilla-armor:", "Vanilla armor", out row) ||
            TryCreate(step, "vanilla-profile:", "Vanilla profile", out row) ||
            TryCreate(step, "weight-variants:", "Weight variants (_0/_1)", out row) ||
            TryCreate(step, "smp-bones:", "SMP bones", out row) ||
            TryCreate(step, "race-compat:", "Race compatibility", out row) ||
            TryCreate(step, "learning-cache:", "Learning cache", out row) ||
            TryCreate(step, "conversion-delta:", "Conversion delta", out row) ||
            TryCreate(step, "textures:", "Texture warnings", out row) ||
            TryCreate(step, "imported:", "Imported assets", out row) ||
            TryCreate(step, "exported:", "Output directory", out row))
        {
            return true;
        }

        row = default!;
        return false;
    }

    private static IReadOnlyList<DesktopWorkflowReportMetric> BuildReportMetrics(
        IReadOnlyList<string> outputDirectories,
        string? baseDirectory)
    {
        var metrics = new List<DesktopWorkflowReportMetric>();
        foreach (var file in outputDirectories
                     .SelectMany(EnumerateKnownReportFiles)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var reportName = !string.IsNullOrWhiteSpace(baseDirectory)
                ? Path.GetRelativePath(baseDirectory, file)
                : Path.GetFileName(file);
            AppendReportMetrics(metrics, reportName, file);
        }

        return metrics;
    }

    private static IReadOnlyList<DesktopWorkflowArtifact> BuildArtifacts(IReadOnlyList<string> files, string? baseDirectory) =>
        files
            .Select(file => new DesktopWorkflowArtifact(
                Path.GetFileName(file),
                !string.IsNullOrWhiteSpace(baseDirectory) ? Path.GetRelativePath(baseDirectory, file) : file,
                file))
            .ToArray();

    private static DesktopWorkflowValidationState BuildValidationState(
        IReadOnlyList<string> outputDirectories,
        string? previewPath,
        IReadOnlyList<DesktopWorkflowReportMetric> reportMetrics)
    {
        var previewAvailable = !string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath);
        var summary = TryReadWorstValidationSummary(outputDirectories);
        var requiresReview = reportMetrics.Any(metric =>
            (metric.Property.Equals("Validation gate", StringComparison.OrdinalIgnoreCase) &&
             !metric.Value.Equals("PASS", StringComparison.OrdinalIgnoreCase)) ||
            (metric.Property.Equals("Support tier", StringComparison.OrdinalIgnoreCase) &&
             !metric.Value.Equals("mainstream-automatic", StringComparison.OrdinalIgnoreCase)) ||
            (metric.Property.Equals("Sparse source inference", StringComparison.OrdinalIgnoreCase) &&
             metric.Value.Equals("Yes", StringComparison.OrdinalIgnoreCase)) ||
            (metric.Property.Equals("Source skeleton reliability", StringComparison.OrdinalIgnoreCase) &&
             metric.Value.Equals("provisional", StringComparison.OrdinalIgnoreCase)) ||
            (metric.Property.Equals("Can safely animate", StringComparison.OrdinalIgnoreCase) &&
             metric.Value.Equals("No", StringComparison.OrdinalIgnoreCase)) ||
            (metric.Property.Equals("Manual cleanup likely", StringComparison.OrdinalIgnoreCase) &&
             metric.Value.Equals("Yes", StringComparison.OrdinalIgnoreCase)) ||
            (metric.Property.Equals("Heuristic-heavy topology", StringComparison.OrdinalIgnoreCase) &&
             metric.Value.Equals("Yes", StringComparison.OrdinalIgnoreCase)) ||
            (metric.Property.Equals("Runtime verification required", StringComparison.OrdinalIgnoreCase) &&
             metric.Value.Equals("Yes", StringComparison.OrdinalIgnoreCase)) ||
            (metric.Property.Equals("Unsupported bones", StringComparison.OrdinalIgnoreCase) &&
             !string.IsNullOrWhiteSpace(metric.Value)));
        var effectiveStatus = summary?.Status
            ?? (requiresReview ? "needs-review" : previewAvailable ? "ready" : null);
        return new DesktopWorkflowValidationState(
            ConversionValidationPresentation.BuildDesktopResultTabTitle("Preview", effectiveStatus),
            ConversionValidationPresentation.BuildDesktopResultTabTitle("Next actions", effectiveStatus),
            ConversionValidationPresentation.BuildDesktopStatusLabel(effectiveStatus, previewAvailable),
            summary is not null
                ? ConversionValidationPresentation.BuildOutcomeSummary(summary, previewAvailable)
                : ConversionValidationPresentation.BuildOutcomeSummary(requiresReview ? "needs-review" : previewAvailable ? "ready" : null, 0, 0, 0, previewAvailable),
            effectiveStatus,
            previewAvailable);
    }

    private static void AppendReportMetrics(
        ICollection<DesktopWorkflowReportMetric> metrics,
        string reportName,
        string filePath)
    {
        try
        {
            using var document = OpenJsonDocument(filePath);
            var root = document.RootElement;
            switch (Path.GetFileName(filePath))
            {
                case "batch-report.json":
                    Add(metrics, reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    Add(metrics, reportName, "Conversion label", TryReadString(root, "ConversionLabel"), filePath);
                    Add(metrics, reportName, "Total items", TryReadIntValue(root, "TotalCount"), filePath);
                    Add(metrics, reportName, "Succeeded", TryReadIntValue(root, "SuccessCount"), filePath);
                    Add(metrics, reportName, "Failed", TryReadIntValue(root, "FailedCount"), filePath);
                    Add(metrics, reportName, "Pack status", TryReadString(root, "PackReadinessStatus"), filePath);
                    Add(metrics, reportName, "Avg validation score", TryReadString(root, "AverageValidationScore"), filePath);
                    break;
                case "armor-pack-validation.json":
                    Add(metrics, reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    Add(metrics, reportName, "Conversion label", TryReadString(root, "ConversionLabel"), filePath);
                    Add(metrics, reportName, "Pack status", TryReadString(root, "PackReadinessStatus"), filePath);
                    Add(metrics, reportName, "Items", TryReadIntValue(root, "TotalCount"), filePath);
                    Add(metrics, reportName, "Quality reports", TryReadIntValue(root, "QualityReportCount"), filePath);
                    Add(metrics, reportName, "Avg validation score", TryReadString(root, "AverageValidationScore"), filePath);
                    break;
                case "conversion-quality.json":
                    Add(metrics, reportName, "Source body", TryReadString(root, "DetectedSourceBody"), filePath);
                    Add(metrics, reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    Add(metrics, reportName, "Mesh type", TryReadString(root, "MeshType"), filePath);
                    Add(metrics, reportName, "Strategy", TryReadString(root, "Strategy"), filePath);
                    Add(metrics, reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    Add(metrics, reportName, "Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert")), filePath);
                    Add(metrics, reportName, "Can physics-convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanPhysicsConvert")), filePath);
                    Add(metrics, reportName, "Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate")), filePath);
                    Add(metrics, reportName, "Support tier summary", TryReadNestedString(root, "ConversionReadiness", "Summary"), filePath);
                    Add(metrics, reportName, "Clipping detected", FormatBool(TryReadBoolValue(root, "ClippingDetected")), filePath);
                    Add(metrics, reportName, "Topology risk", FormatBool(TryReadBoolValue(root, "TopologyMismatchRisk")), filePath);
                    Add(metrics, reportName, "Topology correspondence", TryReadNestedString(root, "TopologyCorrespondence", "Classification"), filePath);
                    Add(metrics, reportName, "Topology correspondence confidence", TryReadNestedString(root, "TopologyCorrespondence", "Confidence"), filePath);
                    Add(metrics, reportName, "Topology matching mode", TryReadNestedString(root, "TopologyCorrespondence", "MatchingMode"), filePath);
                    Add(metrics, reportName, "True semantic correspondence", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "UsesTrueSemanticCorrespondence")), filePath);
                    Add(metrics, reportName, "Manual semantic review", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "RequiresManualSemanticReview")), filePath);
                    Add(metrics, reportName, "Heuristic-heavy topology", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "HeuristicHeavy")), filePath);
                    Add(metrics, reportName, "Topology focus regions", TryReadNestedArray(root, "TopologyCorrespondence", "FocusRegions"), filePath);
                    Add(metrics, reportName, "Topology signals", TryReadNestedArray(root, "TopologyCorrespondence", "Signals"), filePath);
                    Add(metrics, reportName, "Topology recommendations", TryReadNestedArray(root, "TopologyCorrespondence", "Recommendations"), filePath);
                    Add(metrics, reportName, "Validation status", TryReadNestedString(root, "ValidationSummary", "Status"), filePath);
                    Add(metrics, reportName, "Validation score", TryReadNestedString(root, "ValidationSummary", "Score"), filePath);
                    break;
                case "skeleton-compatibility.json":
                    Add(metrics, reportName, "Source skeleton", TryReadString(root, "SourceSkeleton"), filePath);
                    Add(metrics, reportName, "Source skeleton confidence", TryReadString(root, "SourceSkeletonConfidence"), filePath);
                    Add(metrics, reportName, "Source skeleton evidence", TryReadArray(root, "SourceSkeletonEvidence"), filePath);
                    Add(metrics, reportName, "Sparse source inference", FormatBool(TryReadBoolValue(root, "SourceSkeletonUsedSparseInference")), filePath);
                    Add(metrics, reportName, "Source skeleton reliability", TryReadString(root, "SourceSkeletonInferenceReliability"), filePath);
                    Add(metrics, reportName, "Source skeleton summary", TryReadString(root, "SourceSkeletonInferenceSummary"), filePath);
                    Add(metrics, reportName, "Source skeleton candidates", CountNestedArray(root, "SourceSkeletonCandidates"), filePath);
                    Add(metrics, reportName, "Source skeleton alternatives", TryReadInferenceCandidateHighlights(root), filePath);
                    Add(metrics, reportName, "Target skeleton", TryReadString(root, "TargetSkeleton"), filePath);
                    Add(metrics, reportName, "Mapped bones", CountNestedArray(root, "BoneMappings"), filePath);
                    Add(metrics, reportName, "Unsupported bones", TryReadArray(root, "UnsupportedBones"), filePath);
                    Add(metrics, reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    Add(metrics, reportName, "Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert")), filePath);
                    Add(metrics, reportName, "Can physics-convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanPhysicsConvert")), filePath);
                    Add(metrics, reportName, "Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate")), filePath);
                    break;
                case "in-game-validation.json":
                    Add(metrics, reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    Add(metrics, reportName, "Validation gate", TryReadString(root, "ValidationGate"), filePath);
                    Add(metrics, reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    Add(metrics, reportName, "Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert")), filePath);
                    Add(metrics, reportName, "Can physics-convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanPhysicsConvert")), filePath);
                    Add(metrics, reportName, "Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate")), filePath);
                    Add(metrics, reportName, "Support tier summary", TryReadNestedString(root, "ConversionReadiness", "Summary"), filePath);
                    Add(metrics, reportName, "Manual cleanup likely", FormatBool(TryReadBoolValue(root, "ManualCleanupLikely")), filePath);
                    Add(metrics, reportName, "Runtime verification required", FormatBool(TryReadBoolValue(root, "RuntimeVerificationRequired")), filePath);
                    Add(metrics, reportName, "Core body regions", TryReadArray(root, "CoreBodyRegions"), filePath);
                    Add(metrics, reportName, "Sensitive regions", TryReadArray(root, "SensitiveRegions"), filePath);
                    Add(metrics, reportName, "Topology correspondence", TryReadNestedString(root, "TopologyCorrespondence", "Classification"), filePath);
                    Add(metrics, reportName, "Heuristic-heavy topology", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "HeuristicHeavy")), filePath);
                    Add(metrics, reportName, "True semantic correspondence", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "UsesTrueSemanticCorrespondence")), filePath);
                    Add(metrics, reportName, "Semantic anchor profile", TryReadNestedString(root, "TopologyCorrespondence", "SemanticAnchorProfile"), filePath);
                    Add(metrics, reportName, "Semantic anchor coverage", TryReadNestedString(root, "TopologyCorrespondence", "SemanticAnchorCoverage"), filePath);
                    Add(metrics, reportName, "Semantic anchor evidence", TryReadNestedArray(root, "TopologyCorrespondence", "SemanticAnchorEvidence"), filePath);
                    Add(metrics, reportName, "Topology focus regions", TryReadNestedArray(root, "TopologyCorrespondence", "FocusRegions"), filePath);
                    Add(metrics, reportName, "Caveats", TryReadArray(root, "Caveats"), filePath);
                    Add(metrics, reportName, "Scenario matrix", CountNestedArray(root, "ScenarioMatrix"), filePath);
                    Add(metrics, reportName, "Scenario highlights", TryReadScenarioHighlights(root), filePath);
                    Add(metrics, reportName, "High-priority scenarios", CountScenarioPriorities(root, "High", "Action"), filePath);
                    Add(metrics, reportName, "Checklist items", CountNestedArray(root, "Checklist"), filePath);
                    break;
                case "runtime-validation-plan.json":
                    Add(metrics, reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    Add(metrics, reportName, "Validation gate", TryReadString(root, "ValidationGate"), filePath);
                    Add(metrics, reportName, "Support tier", TryReadString(root, "SupportTier"), filePath);
                    Add(metrics, reportName, "Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert")), filePath);
                    Add(metrics, reportName, "Can physics-convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanPhysicsConvert")), filePath);
                    Add(metrics, reportName, "Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate")), filePath);
                    Add(metrics, reportName, "Execution coverage", TryReadString(root, "ExecutionCoverage"), filePath);
                    Add(metrics, reportName, "Live game required", FormatBool(TryReadBoolValue(root, "RequiresLiveGameExecution")), filePath);
                    Add(metrics, reportName, "Automated game execution", FormatBool(TryReadBoolValue(root, "SupportsAutomatedGameExecution")), filePath);
                    Add(metrics, reportName, "External game harness", FormatBool(TryReadBoolValue(root, "RequiresExternalGameHarness")), filePath);
                    Add(metrics, reportName, "Modded test environment", FormatBool(TryReadBoolValue(root, "RequiresModdedTestEnvironment")), filePath);
                    Add(metrics, reportName, "Runtime limitation notes", TryReadArray(root, "LimitationNotes"), filePath);
                    Add(metrics, reportName, "Harness probes", CountNestedArray(root, "AutomationHarness", "Probes"), filePath);
                    Add(metrics, reportName, "Harness automation coverage", TryReadNestedString(root, "AutomationHarness", "AutomationCoverage"), filePath);
                    Add(metrics, reportName, "Execution phases", TryReadExecutionPhases(root), filePath);
                    Add(metrics, reportName, "Blocking runtime steps", CountBlockingExecutionSteps(root), filePath);
                    Add(metrics, reportName, "Runtime execution highlights", TryReadExecutionHighlights(root), filePath);
                    break;
                case "runtime-validation-harness.json":
                    Add(metrics, reportName, "Target body", TryReadString(root, "TargetBody"), filePath);
                    Add(metrics, reportName, "Harness automation coverage", TryReadString(root, "AutomationCoverage"), filePath);
                    Add(metrics, reportName, "Artifact preflight automation", FormatBool(TryReadBoolValue(root, "SupportsArtifactPreflightAutomation")), filePath);
                    Add(metrics, reportName, "Scenario dispatch automation", FormatBool(TryReadBoolValue(root, "SupportsScenarioDispatchAutomation")), filePath);
                    Add(metrics, reportName, "Manual assertion required", FormatBool(TryReadBoolValue(root, "RequiresManualAssertion")), filePath);
                    Add(metrics, reportName, "Harness probes", CountNestedArray(root, "Probes"), filePath);
                    Add(metrics, reportName, "Harness phases", TryReadHarnessPhases(root), filePath);
                    break;
                case "desktop-workflow-automation.json":
                    Add(metrics, reportName, "Preview tab", TryReadNestedString(root, "ValidationState", "PreviewTabTitle"), filePath);
                    Add(metrics, reportName, "Guidance tab", TryReadNestedString(root, "ValidationState", "GuidanceTabTitle"), filePath);
                    Add(metrics, reportName, "Desktop automation coverage", TryReadNestedString(root, "AutomationContract", "Coverage"), filePath);
                    Add(metrics, reportName, "Manual WinForms interaction", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "RequiresManualWinFormsInteraction")), filePath);
                    Add(metrics, reportName, "Windows host required", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "RequiresWindowsHost")), filePath);
                    Add(metrics, reportName, "Embedded preview runtime dependency", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "RequiresWebViewRuntimeForEmbeddedPreview")), filePath);
                    Add(metrics, reportName, "Automated WebView interaction", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "SupportsAutomatedWebViewInteraction")), filePath);
                    Add(metrics, reportName, "True UI E2E automation", FormatBool(TryReadNestedBoolValue(root, "AutomationContract", "SupportsTrueUiEndToEndAutomation")), filePath);
                    Add(metrics, reportName, "Desktop automation limitation notes", TryReadNestedArray(root, "AutomationContract", "LimitationNotes"), filePath);
                    Add(metrics, reportName, "GUI flow steps", CountNestedArray(root, "SuggestedGuiFlow"), filePath);
                    Add(metrics, reportName, "Blocking GUI steps", CountBlockingGuiSteps(root), filePath);
                    Add(metrics, reportName, "GUI flow highlights", TryReadGuiFlowHighlights(root), filePath);
                    break;
                case "pose-simulation-report.json":
                    Add(metrics, reportName, "Tested poses", CountNestedArray(root, "TestedPoses"), filePath);
                    Add(metrics, reportName, "At-risk poses", TryReadIntValue(root, "TotalPosesAtRisk"), filePath);
                    Add(metrics, reportName, "High-risk regions", TryReadArray(root, "HighRiskRegions"), filePath);
                    break;
                case "world-physics.json":
                    Add(metrics, reportName, "Mode", TryReadString(root, "Mode"), filePath);
                    Add(metrics, reportName, "Ground mesh", FormatBool(TryReadBoolValue(root, "GroundMeshAvailable")), filePath);
                    Add(metrics, reportName, "Recommendations", TryReadArray(root, "Recommendations"), filePath);
                    break;
                default:
                    Add(metrics, reportName, "Status", "Open this report for full details.", filePath);
                    break;
            }
        }
        catch (Exception ex)
        {
            Add(metrics, reportName, "Status", $"Failed to read report: {ex.Message}", filePath);
        }
    }

    private static void Add(ICollection<DesktopWorkflowReportMetric> metrics, string reportName, string property, object? value, string filePath)
    {
        var text = value switch
        {
            null => null,
            string stringValue when string.IsNullOrWhiteSpace(stringValue) => null,
            string stringValue => stringValue,
            _ => value.ToString()
        };
        if (!string.IsNullOrWhiteSpace(text))
        {
            metrics.Add(new DesktopWorkflowReportMetric(reportName, property, text, filePath));
        }
    }

    private static IEnumerable<string> EnumerateKnownReportFiles(string outputDirectory) =>
        Directory
            .EnumerateFiles(outputDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    private static JsonDocument OpenJsonDocument(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream);
    }

    private static string? TryReadString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            }
            : null;

    private static string? TryReadNestedString(JsonElement element, string objectPropertyName, string nestedPropertyName) =>
        TryGetProperty(element, objectPropertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? TryReadString(nested, nestedPropertyName)
            : null;

    private static bool? TryReadNestedBoolValue(JsonElement element, string objectPropertyName, string nestedPropertyName) =>
        TryGetProperty(element, objectPropertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? TryReadBoolValue(nested, nestedPropertyName)
            : null;

    private static string? TryReadNestedArray(JsonElement element, string objectPropertyName, string nestedPropertyName) =>
        TryGetProperty(element, objectPropertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? TryReadArray(nested, nestedPropertyName)
            : null;

    private static string? TryReadArray(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? string.Join(", ", value.EnumerateArray().Select(static item => item.ToString()))
            : null;

    private static int? TryReadIntValue(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

    private static int CountNestedArray(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;

    private static int CountNestedArray(JsonElement element, string objectPropertyName, string nestedArrayPropertyName) =>
        TryGetProperty(element, objectPropertyName, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? CountNestedArray(nested, nestedArrayPropertyName)
            : 0;

    private static string? TryReadScenarioHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "ScenarioMatrix", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Join(
            "; ",
            value.EnumerateArray()
                .Select(static scenario => new
                {
                    Name = TryReadString(scenario, "Name"),
                    Priority = TryReadString(scenario, "Priority")
                })
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.Name))
                .OrderByDescending(static entry => GetPriorityRank(entry.Priority))
                .ThenBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static entry =>
                    string.IsNullOrWhiteSpace(entry.Priority) ? entry.Name : $"{entry.Name} [{entry.Priority}]")
                .Take(4)!);
    }

    private static int CountScenarioPriorities(JsonElement element, params string[] priorities)
    {
        if (!TryGetProperty(element, "ScenarioMatrix", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return value.EnumerateArray().Count(scenario =>
        {
            var priority = TryReadString(scenario, "Priority");
            return !string.IsNullOrWhiteSpace(priority) &&
                   priorities.Contains(priority, StringComparer.OrdinalIgnoreCase);
        });
    }

    private static string? TryReadExecutionPhases(JsonElement element)
    {
        if (!TryGetProperty(element, "Steps", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Join(
            ", ",
            value.EnumerateArray()
                .Select(static step => TryReadString(step, "Phase"))
                .Where(static phase => !string.IsNullOrWhiteSpace(phase))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string? TryReadHarnessPhases(JsonElement element)
    {
        if (!TryGetProperty(element, "Probes", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Join(
            ", ",
            value.EnumerateArray()
                .Select(static probe => TryReadString(probe, "Phase"))
                .Where(static phase => !string.IsNullOrWhiteSpace(phase))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static int CountBlockingExecutionSteps(JsonElement element)
    {
        if (!TryGetProperty(element, "Steps", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return value.EnumerateArray().Count(static step =>
            TryReadBoolValue(step, "BlocksRelease") is true);
    }

    private static string? TryReadExecutionHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "Steps", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Join(
            "; ",
            value.EnumerateArray()
                .Select(static step =>
                {
                    var name = TryReadString(step, "Name");
                    var phase = TryReadString(step, "Phase");
                    return string.IsNullOrWhiteSpace(name)
                        ? null
                        : string.IsNullOrWhiteSpace(phase) ? name : $"{phase}: {name}";
                })
                .Where(static entry => !string.IsNullOrWhiteSpace(entry))
                .Take(4)!);
    }

    private static int CountBlockingGuiSteps(JsonElement element)
    {
        if (!TryGetProperty(element, "SuggestedGuiFlow", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return value.EnumerateArray().Count(static step =>
            TryReadBoolValue(step, "Blocking") is true);
    }

    private static string? TryReadGuiFlowHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "SuggestedGuiFlow", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Join(
            "; ",
            value.EnumerateArray()
                .Select(static step =>
                {
                    var area = TryReadString(step, "Area");
                    var action = TryReadString(step, "Action");
                    return string.IsNullOrWhiteSpace(action)
                        ? null
                        : string.IsNullOrWhiteSpace(area) ? action : $"{area}: {action}";
                })
                .Where(static entry => !string.IsNullOrWhiteSpace(entry))
                .Take(4)!);
    }

    private static bool? TryReadBoolValue(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    private static string? FormatBool(bool? value) =>
        value is null ? null : value.Value ? "Yes" : "No";

    private static int GetPriorityRank(string? priority) =>
        priority?.Trim() switch
        {
            var value when string.Equals(value, "high", StringComparison.OrdinalIgnoreCase) => 3,
            var value when string.Equals(value, "action", StringComparison.OrdinalIgnoreCase) => 2,
            var value when string.Equals(value, "warning", StringComparison.OrdinalIgnoreCase) => 2,
            var value when string.Equals(value, "info", StringComparison.OrdinalIgnoreCase) => 1,
            _ => 0
        };

    private static IReadOnlyList<DesktopWorkflowAutomationStep> BuildSuggestedGuiFlow(
        IReadOnlyList<DesktopWorkflowReportMetric> reportMetrics,
        IReadOnlyList<DesktopWorkflowArtifact> artifacts,
        DesktopWorkflowValidationState validationState,
        DesktopAutomationContract automationContract)
    {
        var steps = new List<DesktopWorkflowAutomationStep>
        {
            new(
                "Preview",
                "Open the Preview tab after conversion or after loading a previous output folder.",
                validationState.PreviewTabTitle,
                FindMetricFile(reportMetrics, "Validation gate") ?? FindMetricFile(reportMetrics, "Preview tab"),
                Blocking: false),
            new(
                "Guidance",
                "Open the Next actions tab and verify the summary matches the generated review state.",
                validationState.GuidanceTabTitle,
                FindMetricFile(reportMetrics, "Validation gate") ?? FindMetricFile(reportMetrics, "Guidance tab"),
                Blocking: string.Equals(validationState.EffectiveStatus, "needs-review", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(validationState.EffectiveStatus, "high-risk", StringComparison.OrdinalIgnoreCase))
        };

        if (artifacts.Count > 0)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Files tab",
                "Open the Files tab or use Load result... to inspect the generated artifacts directly from the Desktop workflow.",
                $"{artifacts.Count} output artifact(s) detected",
                artifacts[0].FullPath,
                Blocking: false));
        }

        if (!automationContract.SupportsTrueUiEndToEndAutomation)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Desktop contract",
                "Treat the desktop automation snapshot as shared-output/contract coverage only; run a manual WinForms/WebView interaction pass before release.",
                $"{automationContract.Coverage}; manual WinForms={automationContract.RequiresManualWinFormsInteraction}",
                FindMetricFile(reportMetrics, "Desktop automation coverage"),
                Blocking: false));
        }

        if (automationContract.RequiresWindowsHost)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Windows host",
                "Run any real Desktop E2E pass on a Windows host with WebView2 available; Linux/macOS runs only validate the shared automation contract.",
                $"Windows host required={automationContract.RequiresWindowsHost}; automated WebView={automationContract.SupportsAutomatedWebViewInteraction}",
                FindMetricFile(reportMetrics, "Windows host required") ?? FindMetricFile(reportMetrics, "Automated WebView interaction"),
                Blocking: false));
        }

        var topologyMetric = FindMetric(reportMetrics, "Heuristic-heavy topology", static value => value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                             ?? FindMetric(reportMetrics, "Topology correspondence", static value => !value.Equals("aligned", StringComparison.OrdinalIgnoreCase));
        if (topologyMetric is not null)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Topology review",
                "Open the topology correspondence artifacts and compare the preview against the converted mesh for manual cleanup risk.",
                $"{topologyMetric.Property}: {topologyMetric.Value}",
                topologyMetric.FilePath,
                Blocking: true));
        }

        var supportTierMetric = FindMetric(reportMetrics, "Support tier", static value => !value.Equals("mainstream-automatic", StringComparison.OrdinalIgnoreCase))
                                ?? FindMetric(reportMetrics, "Can safely animate", static value => value.Equals("No", StringComparison.OrdinalIgnoreCase));
        if (supportTierMetric is not null)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Support tier",
                "Check the graded support tier before treating the output as fully automatic; experimental tiers still require manual cleanup or runtime verification.",
                $"{supportTierMetric.Property}: {supportTierMetric.Value}",
                supportTierMetric.FilePath,
                Blocking: true));
        }

        var semanticMetric = FindMetric(reportMetrics, "True semantic correspondence", static value => value.Equals("No", StringComparison.OrdinalIgnoreCase));
        if (semanticMetric is not null)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Semantic anchor review",
                "Open the topology correspondence details and verify whether authored semantic anchors truly cover the flagged body regions before trusting the transfer.",
                $"{semanticMetric.Property}: {semanticMetric.Value}",
                semanticMetric.FilePath,
                Blocking: true));
        }

        var sparseMetric = FindMetric(reportMetrics, "Sparse source inference", static value => value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                           ?? FindMetric(reportMetrics, "Source skeleton reliability", static value => value.Equals("provisional", StringComparison.OrdinalIgnoreCase) || value.Equals("review", StringComparison.OrdinalIgnoreCase));
        if (sparseMetric is not null)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Skeleton review",
                "Open the skeleton compatibility report and confirm the inferred custom rig before trusting automatic remaps.",
                $"{sparseMetric.Property}: {sparseMetric.Value}",
                sparseMetric.FilePath,
                Blocking: true));
        }

        var runtimeMetric = FindMetric(reportMetrics, "Runtime verification required", static value => value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                            ?? FindMetric(reportMetrics, "Scenario highlights");
        if (runtimeMetric is not null)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Runtime scenarios",
                "Review the exported runtime-validation plan and in-game scenario highlights before release.",
                $"{runtimeMetric.Property}: {runtimeMetric.Value}",
                FindMetricFile(reportMetrics, "Runtime execution highlights") ?? runtimeMetric.FilePath,
                Blocking: true));
        }

        var harnessMetric = FindMetric(reportMetrics, "External game harness", static value => value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                            ?? FindMetric(reportMetrics, "Modded test environment", static value => value.Equals("Yes", StringComparison.OrdinalIgnoreCase));
        if (harnessMetric is not null)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Runtime harness",
                "Prepare an external modded test environment before treating the runtime validation plan as executable.",
                $"{harnessMetric.Property}: {harnessMetric.Value}",
                harnessMetric.FilePath,
                Blocking: false));
        }

        var harnessCoverageMetric = FindMetric(reportMetrics, "Harness automation coverage", static value => value.Contains("external-harness-ready", StringComparison.OrdinalIgnoreCase))
                                   ?? FindMetric(reportMetrics, "Scenario dispatch automation", static value => value.Equals("Yes", StringComparison.OrdinalIgnoreCase));
        if (harnessCoverageMetric is not null)
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Runtime harness automation",
                "Use the exported runtime harness contract to automate artifact preflight and scenario dispatch before the final live verification pass.",
                $"{harnessCoverageMetric.Property}: {harnessCoverageMetric.Value}",
                harnessCoverageMetric.FilePath,
                Blocking: false));
        }

        var packagingMetric = FindMetric(reportMetrics, "Pack status")
                              ?? FindMetric(reportMetrics, "Avg validation score");
        if (packagingMetric is not null)
        {
            var packagingReady = packagingMetric.Value.Equals("READY", StringComparison.OrdinalIgnoreCase) ||
                                 packagingMetric.Value.Equals("PASS", StringComparison.OrdinalIgnoreCase);
            steps.Add(new DesktopWorkflowAutomationStep(
                "Packaging",
                "Open armor-pack-validation.json before packaging or sharing the output so the Desktop workflow matches the validated release state.",
                $"{packagingMetric.Property}: {packagingMetric.Value}",
                packagingMetric.FilePath,
                Blocking: !packagingReady));
        }

        if (HasArtifact(artifacts, artifact =>
                artifact.DisplayPath.Contains("CalienteTools", StringComparison.OrdinalIgnoreCase) &&
                artifact.DisplayPath.Contains("BodySlide", StringComparison.OrdinalIgnoreCase)))
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "BodySlide",
                "Open the generated BodySlide OSP/ShapeData artifacts and confirm the Desktop output still builds correctly in BodySlide or Outfit Studio.",
                "BodySlide slider assets detected",
                FindArtifactPath(artifacts, artifact =>
                    artifact.DisplayPath.EndsWith(".osp", StringComparison.OrdinalIgnoreCase) ||
                    artifact.DisplayPath.Contains("ShapeData", StringComparison.OrdinalIgnoreCase)),
                Blocking: false));
        }

        if (HasArtifact(artifacts, artifact =>
                artifact.Name.Equals("plugin-patches.json", StringComparison.OrdinalIgnoreCase) ||
                artifact.Name.Equals("patch-armor.pas", StringComparison.OrdinalIgnoreCase) ||
                artifact.Name.EndsWith("_SlidesmithPatch.esp", StringComparison.OrdinalIgnoreCase)))
        {
            steps.Add(new DesktopWorkflowAutomationStep(
                "Plugin patch",
                "Open the generated plugin-patch artifacts and verify the rewritten mesh paths or manual xEdit follow-up before release.",
                "Plugin patch artifacts detected",
                FindArtifactPath(artifacts, artifact =>
                    artifact.Name.Equals("plugin-patches.json", StringComparison.OrdinalIgnoreCase) ||
                    artifact.Name.Equals("patch-armor.pas", StringComparison.OrdinalIgnoreCase) ||
                    artifact.Name.EndsWith("_SlidesmithPatch.esp", StringComparison.OrdinalIgnoreCase)),
                Blocking: true));
        }

        return steps;
    }

    private static DesktopAutomationContract BuildAutomationContract(DesktopWorkflowValidationState validationState) =>
        new(
            "shared-output-contract",
            RequiresManualWinFormsInteraction: true,
            RequiresWindowsHost: true,
            RequiresWebViewRuntimeForEmbeddedPreview: true,
            SupportsAutomatedWebViewInteraction: false,
            SupportsTrueUiEndToEndAutomation: false,
            [
                "Desktop workflow coverage is derived from shared output artifacts and validation state, not from real WinForms click-path automation.",
                "A true Desktop end-to-end run still requires a Windows host with WebView2 and an external UI automation harness.",
                "Embedded preview behavior still depends on a local WebView2 runtime and should be validated manually on the host machine."
            ]);

    private static string? TryReadInferenceCandidateHighlights(JsonElement element)
    {
        if (!TryGetProperty(element, "SourceSkeletonCandidates", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value
            .EnumerateArray()
            .Select(static candidate => new
            {
                Label = TryReadString(candidate, "Label"),
                Confidence = TryReadString(candidate, "Confidence")
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Label))
            .Take(3)
            .Select(static item => string.IsNullOrWhiteSpace(item.Confidence) ? item.Label! : $"{item.Label} ({item.Confidence})")
            .ToArray();

        return items.Length == 0 ? null : string.Join("; ", items);
    }

    private static DesktopWorkflowReportMetric? FindMetric(
        IReadOnlyList<DesktopWorkflowReportMetric> reportMetrics,
        string property,
        Func<string, bool>? predicate = null) =>
        reportMetrics.FirstOrDefault(metric =>
            metric.Property.Equals(property, StringComparison.OrdinalIgnoreCase) &&
            (predicate is null || predicate(metric.Value)));

    private static string? FindMetricFile(IReadOnlyList<DesktopWorkflowReportMetric> reportMetrics, string property) =>
        FindMetric(reportMetrics, property)?.FilePath;

    private static bool HasArtifact(
        IReadOnlyList<DesktopWorkflowArtifact> artifacts,
        Func<DesktopWorkflowArtifact, bool> predicate) =>
        artifacts.Any(predicate);

    private static string? FindArtifactPath(
        IReadOnlyList<DesktopWorkflowArtifact> artifacts,
        Func<DesktopWorkflowArtifact, bool> predicate) =>
        artifacts.FirstOrDefault(predicate)?.FullPath;

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static ConversionValidationSummary? TryReadWorstValidationSummary(IReadOnlyList<string> outputDirectories)
    {
        return outputDirectories
            .Where(static directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(EnumerateValidationSummaryCandidates)
            .Select(TryReadValidationSummaryFromReport)
            .Where(static summary => summary is not null)
            .Cast<ConversionValidationSummary>()
            .OrderByDescending(static summary => ConversionValidationPresentation.GetGateRank(summary.Status))
            .ThenByDescending(static summary => summary.HighSeverityCount)
            .ThenByDescending(static summary => summary.MediumSeverityCount)
            .ThenByDescending(static summary => summary.LowSeverityCount)
            .ThenBy(static summary => summary.Score)
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateValidationSummaryCandidates(string outputDirectory)
    {
        var conversionQualityPath = Path.Combine(outputDirectory, "conversion-quality.json");
        if (File.Exists(conversionQualityPath))
        {
            yield return conversionQualityPath;
        }

        var batchReportPath = Path.Combine(outputDirectory, "batch-report.json");
        if (File.Exists(batchReportPath))
        {
            yield return batchReportPath;
        }
    }

    private static ConversionValidationSummary? TryReadValidationSummaryFromReport(string reportPath)
    {
        try
        {
            using var document = OpenJsonDocument(reportPath);
            return TryReadValidationSummary(document.RootElement);
        }
        catch (Exception) when (File.Exists(reportPath))
        {
            return null;
        }
    }

    private static ConversionValidationSummary? TryReadValidationSummary(JsonElement element)
    {
        if (!TryGetProperty(element, "ValidationSummary", out var summary) || summary.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var status = TryReadString(summary, "Status");
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var issues = new List<ConversionValidationIssue>();
        if (TryGetProperty(summary, "Issues", out var issuesValue) && issuesValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var issue in issuesValue.EnumerateArray())
            {
                var code = TryReadString(issue, "Code");
                var severity = TryReadString(issue, "Severity");
                var message = TryReadString(issue, "Message");
                if (!string.IsNullOrWhiteSpace(code) &&
                    !string.IsNullOrWhiteSpace(severity) &&
                    !string.IsNullOrWhiteSpace(message))
                {
                    issues.Add(new ConversionValidationIssue(code, severity, message));
                }
            }
        }

        return new ConversionValidationSummary(
            status,
            TryReadIntValue(summary, "Score") ?? 0,
            TryReadIntValue(summary, "HighSeverityCount") ?? 0,
            TryReadIntValue(summary, "MediumSeverityCount") ?? 0,
            TryReadIntValue(summary, "LowSeverityCount") ?? 0,
            issues);
    }

    private static string? FindCommonDirectory(IEnumerable<string> directories)
    {
        var normalized = directories
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        if (normalized.Length == 0)
        {
            return null;
        }

        var candidate = normalized[0];
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            var matchPrefix = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var allMatch = normalized.All(path =>
                string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(matchPrefix, StringComparison.OrdinalIgnoreCase));
            if (allMatch)
            {
                return candidate;
            }

            candidate = Path.GetDirectoryName(candidate);
        }

        return null;
    }
}
