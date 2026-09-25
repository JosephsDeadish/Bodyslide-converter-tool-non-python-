using System.Text;
using System.Text.Json;

namespace Bodyslide.Core;

public sealed record ProofHarnessBundleArtifact(
    string Name,
    string RelativePath,
    string Purpose,
    bool Required);

public sealed record ProofHarnessHostRequirement(
    string Scope,
    string Requirement,
    string Details,
    bool Required);

public sealed record ProofHarnessComponentReference(
    string Component,
    string HarnessKind,
    IReadOnlyList<string> PlanArtifacts,
    IReadOnlyList<string> ProofAxes,
    IReadOnlyList<string> MatrixCoordinatesTargeted,
    IReadOnlyList<string> RequiredHostCapabilities,
    IReadOnlyList<string> ExpectedResultArtifacts);

public sealed record ProofHarnessArtifactEntrypoints(
    string MatrixProofReport,
    string RuntimeValidationPlan,
    string RuntimeValidationHarness,
    string LiveGameExecutionPlan,
    string RuntimeObservationTemplate,
    string DesktopWorkflowAutomation,
    string WindowsUiE2EAutomation,
    string ResultBundle);

public sealed record ProofHarnessScenarioReference(
    string Scenario,
    string Component,
    string ValidationSaveProfile,
    string EvidenceDirectory,
    IReadOnlyList<string> ProofAxes,
    IReadOnlyList<string> MatrixCoordinatesTargeted,
    IReadOnlyList<string> ObservationChannels,
    IReadOnlyList<string> RelatedArtifacts,
    IReadOnlyList<string> ProofDeliverables,
    bool BlocksRelease);

public sealed record ProofEvidenceLocation(
    string Name,
    string RelativePath,
    string Purpose,
    bool Required);

public sealed record ProofHarnessReplayableEvidenceContract(
    string EvidenceRoot,
    IReadOnlyList<ProofEvidenceLocation> Locations,
    IReadOnlyList<string> RequiredObservationChannels,
    IReadOnlyList<string> RequiredEvidenceCategories,
    IReadOnlyList<string> Notes);

public sealed record ProofScenarioExpectation(
    string Scenario,
    string ValidationSaveProfile,
    string EvidenceDirectory,
    IReadOnlyList<string> RequiredSignals,
    IReadOnlyList<string> RelatedArtifacts,
    bool BlocksRelease);

public sealed record ProofResultBundleContract(
    string ContractVersion,
    string ResultFile,
    string EvidenceRoot,
    IReadOnlyList<string> ExpectedComponentNames,
    IReadOnlyList<string> RequiredOverallFields,
    IReadOnlyList<ProofEvidenceLocation> EvidenceLocations,
    IReadOnlyList<ProofScenarioExpectation> ScenarioExpectations,
    IReadOnlyList<string> Notes);

public sealed record ProofHarnessImportTarget(
    string Name,
    string RelativePath,
    string Purpose,
    IReadOnlyList<string> RefreshedFields);

public sealed record ProofHarnessBundleManifest(
    string ContractVersion,
    string TargetBody,
    string CanonicalEntryPoint,
    ProofHarnessArtifactEntrypoints ArtifactEntrypoints,
    IReadOnlyList<ProofHarnessBundleArtifact> RequiredArtifacts,
    IReadOnlyList<ProofHarnessComponentReference> Components,
    IReadOnlyList<ProofHarnessScenarioReference> ScenarioCatalog,
    IReadOnlyList<ProofHarnessHostRequirement> HostRequirements,
    ProofHarnessReplayableEvidenceContract ReplayableEvidence,
    ProofResultBundleContract ResultBundleContract,
    IReadOnlyList<ProofHarnessImportTarget> ImportTargets,
    IReadOnlyList<string> ExpectedResultFiles,
    IReadOnlyList<string> Notes);

public sealed record ImportedProofHostDetails(
    string OperatingSystem,
    string HarnessRunner,
    string? Launcher,
    string? ModManager,
    string? SaveProfile,
    string? ObservationMode,
    IReadOnlyList<string> Capabilities);

public sealed record ImportedProofComponentResult(
    string Component,
    string Status,
    IReadOnlyList<string> ExecutedItems,
    IReadOnlyList<string> MissingItems,
    IReadOnlyList<string> EvidenceArtifacts,
    IReadOnlyList<string> Notes);

public sealed record ImportedProofScenarioResult(
    string Scenario,
    string Status,
    string ValidationSaveProfile,
    IReadOnlyList<string> ObservedSignals,
    IReadOnlyList<string> MissingSignals,
    IReadOnlyList<string> EvidenceArtifacts,
    IReadOnlyList<string> Notes);

public sealed record ImportedProofProbeResult(
    string ProbeId,
    string Status,
    IReadOnlyList<string> ObservedSignals,
    IReadOnlyList<string> MissingSignals,
    IReadOnlyList<string> EvidenceArtifacts,
    IReadOnlyList<string> Notes);

public sealed record ProofExecutionState(
    string Axis,
    string PlannedStatus,
    string ExecutedStatus,
    bool ImportedResultAvailable,
    bool StrictProofSatisfied,
    int PlannedItemCount,
    int ExecutedItemCount,
    int MissingItemCount,
    IReadOnlyList<string> MissingItems,
    IReadOnlyList<string> EvidenceArtifacts,
    IReadOnlyList<string> HostDetails,
    IReadOnlyList<string> Notes);

public sealed record ImportedProofResultBundle(
    string ContractVersion,
    string HarnessKind,
    string OverallStatus,
    ImportedProofHostDetails Host,
    IReadOnlyList<ImportedProofComponentResult> ComponentResults,
    IReadOnlyList<ImportedProofScenarioResult> ScenarioResults,
    IReadOnlyList<ImportedProofProbeResult> ProbeResults,
    IReadOnlyList<string> MissingExpectedArtifacts,
    IReadOnlyList<string> MissingExpectedScenarios,
    IReadOnlyList<string> MissingExpectedProbes,
    IReadOnlyList<string> Notes);

internal sealed record ImportedProofBundleSummary(
    ImportedProofResultBundle? Bundle,
    ProofExecutionState RuntimeProof,
    ProofExecutionState LiveGameProof,
    ProofExecutionState DesktopProof);

internal static class ExternalProofHarnessSupport
{
    public const string BundleManifestFileName = "proof-harness-bundle.json";
    public const string ResultBundleFileName = "proof-result-bundle.json";
    public const string EvidenceRootDirectory = "proof-evidence";
    public const string ContractVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string GetBundleManifestPath(string outputDirectory) =>
        Path.Combine(outputDirectory, BundleManifestFileName);

    public static string GetResultBundlePath(string outputDirectory) =>
        Path.Combine(outputDirectory, ResultBundleFileName);

    public static ProofExecutionState CreatePlannedExecutionState(
        string axis,
        string plannedStatus,
        int plannedItemCount,
        params string[] notes)
    {
        return new ProofExecutionState(
            Axis: axis,
            PlannedStatus: plannedStatus,
            ExecutedStatus: "planned-only",
            ImportedResultAvailable: false,
            StrictProofSatisfied: false,
            PlannedItemCount: plannedItemCount,
            ExecutedItemCount: 0,
            MissingItemCount: plannedItemCount,
            MissingItems: [],
            EvidenceArtifacts: [],
            HostDetails: [],
            Notes: notes.Where(static note => !string.IsNullOrWhiteSpace(note)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public static string BuildEffectiveCoverage(ProofExecutionState proofExecution)
    {
        return proofExecution.ExecutedStatus switch
        {
            "executed-pass" => "externally-executed-and-imported",
            "executed-incomplete" => "externally-executed-incomplete",
            "executed-fail" => "externally-executed-with-failures",
            "import-error" => "external-result-import-error",
            _ => proofExecution.PlannedStatus
        };
    }

    public static IReadOnlyList<string> BuildPlannedOnlyAxes(IEnumerable<ProofExecutionState> proofExecutions)
    {
        return proofExecutions
            .Where(static proof => string.Equals(proof.ExecutedStatus, "planned-only", StringComparison.OrdinalIgnoreCase))
            .Select(static proof => proof.Axis)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> BuildExecutedImportedAxes(IEnumerable<ProofExecutionState> proofExecutions)
    {
        return proofExecutions
            .Where(static proof => proof.ImportedResultAvailable ||
                                   !string.Equals(proof.ExecutedStatus, "planned-only", StringComparison.OrdinalIgnoreCase))
            .Select(static proof => $"{proof.Axis}:{proof.ExecutedStatus}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> CollectImportedHostDetails(IEnumerable<ProofExecutionState> proofExecutions)
    {
        return proofExecutions
            .SelectMany(static proof => proof.HostDetails)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> CollectImportedEvidenceArtifacts(IEnumerable<ProofExecutionState> proofExecutions)
    {
        return proofExecutions
            .SelectMany(static proof => proof.EvidenceArtifacts)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ProofHarnessBundleManifest BuildBundleManifest(
        string targetBody,
        RuntimeValidationExecutionPlan runtimePlan,
        LiveGameExecutionPlan liveGameExecution,
        WindowsUiE2EAutomationPlan windowsUiAutomation,
        ConversionMatrixProofReport conversionMatrixProof)
    {
        const string matrixProofReport = "conversion-matrix-proof.json";
        const string runtimeValidationPlan = "runtime-validation-plan.json";
        const string runtimeValidationHarness = "runtime-validation-harness.json";
        const string liveGameExecutionPlan = "live-game-execution.json";
        const string runtimeObservationTemplate = "runtime-observation-bundle.template.json";
        const string desktopWorkflowAutomation = "desktop-workflow-automation.json";
        const string windowsUiE2EAutomation = "windows-ui-e2e-automation.json";

        var artifactEntrypoints = new ProofHarnessArtifactEntrypoints(
            MatrixProofReport: matrixProofReport,
            RuntimeValidationPlan: runtimeValidationPlan,
            RuntimeValidationHarness: runtimeValidationHarness,
            LiveGameExecutionPlan: liveGameExecutionPlan,
            RuntimeObservationTemplate: runtimeObservationTemplate,
            DesktopWorkflowAutomation: desktopWorkflowAutomation,
            WindowsUiE2EAutomation: windowsUiE2EAutomation,
            ResultBundle: ResultBundleFileName);

        var requiredArtifacts = new[]
        {
            new ProofHarnessBundleArtifact("Matrix proof", matrixProofReport, "Top-level proof state and release-gate gaps.", true),
            new ProofHarnessBundleArtifact("Runtime validation plan", runtimeValidationPlan, "Planned runtime release-gate steps.", true),
            new ProofHarnessBundleArtifact("Runtime automation harness", runtimeValidationHarness, "Runtime probe dispatch/assertion contract.", true),
            new ProofHarnessBundleArtifact("Live-game execution plan", liveGameExecutionPlan, "Live-game save/launcher/scenario proof contract.", true),
            new ProofHarnessBundleArtifact("Runtime observation template", runtimeObservationTemplate, "Expected runtime observation/evidence template.", true),
            new ProofHarnessBundleArtifact("Desktop workflow automation", desktopWorkflowAutomation, "Desktop review and artifact/state snapshot.", true),
            new ProofHarnessBundleArtifact("Windows UI E2E plan", windowsUiE2EAutomation, "Windows UI automation selectors, flows, and steps.", true),
            new ProofHarnessBundleArtifact("Canonical result bundle", ResultBundleFileName, "Imported external proof results written back into the output root.", false),
        };

        var components = new[]
        {
            new ProofHarnessComponentReference(
                Component: "runtime-automation",
                HarnessKind: runtimePlan.AutomationHarness?.BootstrapContract.HarnessKind ?? "runtime-validation-runner",
                PlanArtifacts: [runtimeValidationPlan, runtimeValidationHarness, BundleManifestFileName],
                ProofAxes: runtimePlan.AutomationHarness?.BlockingProofAxes ?? ["runtime-automation"],
                MatrixCoordinatesTargeted: runtimePlan.AutomationHarness?.MatrixCombinationsTargeted ?? ["body-skeleton-plugin-runtime"],
                RequiredHostCapabilities:
                [
                    "artifact-preflight-review",
                    "probe-dispatch",
                    "assertion-capture"
                ],
                ExpectedResultArtifacts:
                [
                    ResultBundleFileName,
                    $"{EvidenceRootDirectory}/runtime-logs/",
                    $"{EvidenceRootDirectory}/step-traces/",
                    $"{EvidenceRootDirectory}/probe-observations/"
                ]),
            new ProofHarnessComponentReference(
                Component: "live-game-execution",
                HarnessKind: liveGameExecution.BootstrapContract.HarnessKind,
                PlanArtifacts: [liveGameExecutionPlan, runtimeObservationTemplate, BundleManifestFileName],
                ProofAxes: liveGameExecution.BlockingProofAxes,
                MatrixCoordinatesTargeted: liveGameExecution.MatrixCombinationsTargeted,
                RequiredHostCapabilities: liveGameExecution.RequiredHostCapabilities,
                ExpectedResultArtifacts:
                [
                    ResultBundleFileName,
                    $"{EvidenceRootDirectory}/screenshots/",
                    $"{EvidenceRootDirectory}/runtime-logs/",
                    $"{EvidenceRootDirectory}/scenario-observations/",
                    $"{EvidenceRootDirectory}/load-order-state/"
                ]),
            new ProofHarnessComponentReference(
                Component: "desktop-e2e",
                HarnessKind: windowsUiAutomation.BootstrapContract.HarnessKind,
                PlanArtifacts: [desktopWorkflowAutomation, windowsUiE2EAutomation, BundleManifestFileName],
                ProofAxes: windowsUiAutomation.BlockingProofAxes,
                MatrixCoordinatesTargeted: windowsUiAutomation.MatrixCombinationsTargeted,
                RequiredHostCapabilities:
                [
                    "windows-host-control",
                    "winforms-uia-selector-resolution",
                    "preview-runtime-availability",
                    "ui-screenshot-capture"
                ],
                ExpectedResultArtifacts:
                [
                    ResultBundleFileName,
                    $"{EvidenceRootDirectory}/screenshots/",
                    $"{EvidenceRootDirectory}/selector-logs/",
                    $"{EvidenceRootDirectory}/step-traces/"
                ])
        };

        var scenarioCatalog = liveGameExecution.ScenarioProfiles
            .Select(profile => new ProofHarnessScenarioReference(
                Scenario: profile.Name,
                Component: "live-game-execution",
                ValidationSaveProfile: profile.ValidationSaveProfile,
                EvidenceDirectory: $"{EvidenceRootDirectory}/scenario-observations/{BuildScenarioEvidenceKey(profile.Name)}/",
                ProofAxes: profile.ProofAxes ?? ["live-game-execution"],
                MatrixCoordinatesTargeted: profile.MatrixCoordinatesTargeted ?? liveGameExecution.MatrixCombinationsTargeted,
                ObservationChannels: profile.ObservationChannels,
                RelatedArtifacts: profile.RelatedArtifacts,
                ProofDeliverables: profile.ProofDeliverables,
                BlocksRelease: profile.BlocksRelease))
            .ToArray();

        var evidenceLocations = new[]
        {
            new ProofEvidenceLocation("Host summary", $"{EvidenceRootDirectory}/host-summary.txt", "Host/version/harness details captured during external execution.", true),
            new ProofEvidenceLocation("Screenshots", $"{EvidenceRootDirectory}/screenshots/", "Desktop and live-game screenshot evidence.", true),
            new ProofEvidenceLocation("Runtime logs", $"{EvidenceRootDirectory}/runtime-logs/", "Runtime/log capture emitted by the external harness.", true),
            new ProofEvidenceLocation("Step traces", $"{EvidenceRootDirectory}/step-traces/", "Replayable UI/runtime step traces.", true),
            new ProofEvidenceLocation("Selector logs", $"{EvidenceRootDirectory}/selector-logs/", "Windows UI selector resolution logs.", false),
            new ProofEvidenceLocation("Probe observations", $"{EvidenceRootDirectory}/probe-observations/", "Per-probe observation payloads and structured assertions.", false),
            new ProofEvidenceLocation("Scenario observations", $"{EvidenceRootDirectory}/scenario-observations/", "Per-scenario observations keyed by ScenarioMatrix names.", true),
            new ProofEvidenceLocation("Load order state", $"{EvidenceRootDirectory}/load-order-state/", "Launcher/mod-stack/save-profile evidence.", false)
        };

        var replayableEvidence = new ProofHarnessReplayableEvidenceContract(
            EvidenceRoot: EvidenceRootDirectory,
            Locations: evidenceLocations,
            RequiredObservationChannels: liveGameExecution.ObservationChannels
                .Concat(["screenshots", "step-traces", "runtime-logs"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            RequiredEvidenceCategories:
            [
                "screenshots",
                "runtime-logs",
                "step-traces",
                "scenario-observations",
                "probe-observations"
            ],
            Notes:
            [
                "Scenario observation directories must remain keyed by the exported ScenarioMatrix names so imported proof can be matched back to the right live-game scenarios.",
                "Step traces, runtime logs, screenshots, and probe/scenario observations should be replayable without re-discovering the originating plan artifacts."
            ]);

        var resultContract = new ProofResultBundleContract(
            ContractVersion: ContractVersion,
            ResultFile: ResultBundleFileName,
            EvidenceRoot: EvidenceRootDirectory,
            ExpectedComponentNames: ["runtime-automation", "live-game-execution", "desktop-e2e"],
            RequiredOverallFields:
            [
                "ContractVersion",
                "HarnessKind",
                "OverallStatus",
                "Host",
                "ComponentResults",
                "ScenarioResults",
                "ProbeResults",
                "MissingExpectedArtifacts",
                "MissingExpectedScenarios",
                "MissingExpectedProbes",
                "Notes"
            ],
            EvidenceLocations: evidenceLocations,
            ScenarioExpectations: liveGameExecution.ScenarioProfiles
                .Select(profile => new ProofScenarioExpectation(
                    profile.Name,
                    profile.ValidationSaveProfile,
                    $"{EvidenceRootDirectory}/scenario-observations/{BuildScenarioEvidenceKey(profile.Name)}/",
                    profile.SuccessSignals,
                    profile.RelatedArtifacts,
                    profile.BlocksRelease))
                .ToArray(),
            Notes:
            [
                "Copy the completed proof-result-bundle.json back into the output root, then reload the result in SlideSmith/Desktop review to refresh proof state.",
                $"Use {BundleManifestFileName} as the canonical entrypoint for external runners instead of discovering individual JSON artifacts ad hoc."
            ]);

        var hostRequirements = BuildHostRequirements(liveGameExecution, windowsUiAutomation);
        var importTargets = new[]
        {
            new ProofHarnessImportTarget(
                "Runtime validation plan",
                runtimeValidationPlan,
                "Refreshes runtime proof coverage after an external result import.",
                ["ExecutionCoverage", "ProofExecution", "ProofResultBundlePath", "HarnessBundleManifestPath"]),
            new ProofHarnessImportTarget(
                "Live-game execution plan",
                liveGameExecutionPlan,
                "Refreshes live-game proof coverage after an external result import.",
                ["IntegrationCoverage", "ProofExecution", "ProofResultBundlePath", "HarnessBundleManifestPath"]),
            new ProofHarnessImportTarget(
                "Windows UI E2E automation",
                windowsUiE2EAutomation,
                "Refreshes Desktop E2E proof coverage after an external result import.",
                ["Coverage", "ProofExecution", "ProofResultBundlePath", "HarnessBundleManifestPath"]),
            new ProofHarnessImportTarget(
                "Matrix proof",
                matrixProofReport,
                "Refreshes top-level planned-vs-executed proof state and blocking gaps after an external result import.",
                ["ProofCoverage", "StrictProofReady", "ProofExecutionStatus", "ProofExecutions", "MissingProofAxes", "ProofHarnessBundleManifestPath", "ProofResultBundlePath"])
        };

        return new ProofHarnessBundleManifest(
            ContractVersion: ContractVersion,
            TargetBody: targetBody,
            CanonicalEntryPoint: BundleManifestFileName,
            ArtifactEntrypoints: artifactEntrypoints,
            RequiredArtifacts: requiredArtifacts,
            Components: components,
            ScenarioCatalog: scenarioCatalog,
            HostRequirements: hostRequirements,
            ReplayableEvidence: replayableEvidence,
            ResultBundleContract: resultContract,
            ImportTargets: importTargets,
            ExpectedResultFiles:
            [
                ResultBundleFileName,
                $"{EvidenceRootDirectory}/host-summary.txt",
                $"{EvidenceRootDirectory}/screenshots/",
                $"{EvidenceRootDirectory}/runtime-logs/",
                $"{EvidenceRootDirectory}/step-traces/",
                $"{EvidenceRootDirectory}/scenario-observations/",
                $"{EvidenceRootDirectory}/probe-observations/"
            ],
            Notes:
            [
                $"Proof coverage starts as planned-only ({conversionMatrixProof.ProofCoverage}) until {ResultBundleFileName} is imported.",
                "External Windows execution is still required for desktop and live-game proof; this manifest standardizes export, execution, and result re-ingestion."
            ]);
    }

    public static void RefreshImportedProofState(string? outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            return;
        }

        var runtimePlanPath = Path.Combine(outputDirectory, "runtime-validation-plan.json");
        var liveGamePath = Path.Combine(outputDirectory, "live-game-execution.json");
        var windowsUiPath = Path.Combine(outputDirectory, "windows-ui-e2e-automation.json");
        var matrixProofPath = Path.Combine(outputDirectory, "conversion-matrix-proof.json");

        var runtimePlan = ReadJson<RuntimeValidationExecutionPlan>(runtimePlanPath);
        var liveGameExecution = ReadJson<LiveGameExecutionPlan>(liveGamePath);
        var windowsUiAutomation = ReadJson<WindowsUiE2EAutomationPlan>(windowsUiPath);
        var conversionMatrixProof = ReadJson<ConversionMatrixProofReport>(matrixProofPath);

        if (runtimePlan is null || liveGameExecution is null || windowsUiAutomation is null || conversionMatrixProof is null)
        {
            return;
        }

        var summary = BuildImportedProofBundleSummary(outputDirectory, runtimePlan, liveGameExecution, windowsUiAutomation);
        var manifestPath = GetBundleManifestPath(outputDirectory);
        var resultBundlePath = GetResultBundlePath(outputDirectory);

        var updatedRuntimePlan = runtimePlan with
        {
            ExecutionCoverage = BuildEffectiveCoverage(summary.RuntimeProof),
            ProofExecution = summary.RuntimeProof,
            ProofResultBundlePath = resultBundlePath,
            HarnessBundleManifestPath = manifestPath
        };

        var updatedLiveGameExecution = liveGameExecution with
        {
            IntegrationCoverage = BuildEffectiveCoverage(summary.LiveGameProof),
            ProofExecution = summary.LiveGameProof,
            ProofResultBundlePath = resultBundlePath,
            HarnessBundleManifestPath = manifestPath
        };

        var updatedWindowsUiAutomation = windowsUiAutomation with
        {
            Coverage = BuildEffectiveCoverage(summary.DesktopProof),
            ProofExecution = summary.DesktopProof,
            ProofResultBundlePath = resultBundlePath,
            HarnessBundleManifestPath = manifestPath
        };

        var updatedAxes = conversionMatrixProof.Axes
            .Select(axis => axis.Axis switch
            {
                "runtime-automation" => UpdateProofAxis(axis, summary.RuntimeProof),
                "live-game-execution" => UpdateProofAxis(axis, summary.LiveGameProof),
                "desktop-e2e" => UpdateProofAxis(axis, summary.DesktopProof),
                _ => axis
            })
            .ToArray();

        var missingProofAxes = updatedAxes
            .Where(static axis => !axis.StrictlyProven)
            .Select(static axis => axis.Axis)
            .ToArray();
        var effectiveProofCoverage = missingProofAxes.Length == 0
            ? "strict-matrix-proof-ready"
            : missingProofAxes.Length <= 2
                ? "artifact-backed-with-targeted-gaps"
                : "artifact-backed-with-major-gaps";
        var proofExecutions = new[] { summary.RuntimeProof, summary.LiveGameProof, summary.DesktopProof };

        var updatedProof = conversionMatrixProof with
        {
            ProofCoverage = effectiveProofCoverage,
            StrictProofReady = missingProofAxes.Length == 0,
            MissingProofAxes = missingProofAxes,
            Axes = updatedAxes,
            ProofExecutionStatus = DeriveOverallProofExecutionStatus(summary),
            ProofExecutions = proofExecutions,
            PlannedOnlyProofAxes = BuildPlannedOnlyAxes(proofExecutions),
            ExecutedImportedProofAxes = BuildExecutedImportedAxes(proofExecutions),
            ImportedHostDetails = CollectImportedHostDetails(proofExecutions),
            ImportedEvidenceArtifacts = CollectImportedEvidenceArtifacts(proofExecutions),
            ProofHarnessBundleManifestPath = manifestPath,
            ProofResultBundlePath = resultBundlePath
        };

        WriteJsonIfChanged(runtimePlanPath, updatedRuntimePlan);
        WriteJsonIfChanged(liveGamePath, updatedLiveGameExecution);
        WriteJsonIfChanged(windowsUiPath, updatedWindowsUiAutomation);
        WriteJsonIfChanged(matrixProofPath, updatedProof);
    }

    private static ConversionMatrixProofAxis UpdateProofAxis(ConversionMatrixProofAxis axis, ProofExecutionState execution)
    {
        var signals = axis.Signals
            .Where(static signal =>
                !signal.StartsWith("planned-status:", StringComparison.OrdinalIgnoreCase) &&
                !signal.StartsWith("executed-status:", StringComparison.OrdinalIgnoreCase) &&
                !signal.StartsWith("missing-items:", StringComparison.OrdinalIgnoreCase))
            .Concat(
            [
                $"planned-status:{execution.PlannedStatus}",
                $"executed-status:{execution.ExecutedStatus}",
                $"missing-items:{execution.MissingItemCount}"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return axis with
        {
            Coverage = BuildEffectiveCoverage(execution),
            StrictlyProven = execution.StrictProofSatisfied,
            Signals = signals,
            ProofExecution = execution
        };
    }

    private static ImportedProofBundleSummary BuildImportedProofBundleSummary(
        string outputDirectory,
        RuntimeValidationExecutionPlan runtimePlan,
        LiveGameExecutionPlan liveGameExecution,
        WindowsUiE2EAutomationPlan windowsUiAutomation)
    {
        var plannedRuntime = runtimePlan.ProofExecution ?? CreatePlannedExecutionState(
            "runtime-automation",
            runtimePlan.PlannedExecutionCoverage,
            runtimePlan.AutomationHarness?.Probes.Count ?? runtimePlan.Steps.Count,
            "External runtime harness execution has not been imported yet.");
        var plannedLiveGame = liveGameExecution.ProofExecution ?? CreatePlannedExecutionState(
            "live-game-execution",
            liveGameExecution.PlannedIntegrationCoverage,
            liveGameExecution.ScenarioProfiles.Count,
            "External live-game scenario execution has not been imported yet.");
        var plannedDesktop = windowsUiAutomation.ProofExecution ?? CreatePlannedExecutionState(
            "desktop-e2e",
            windowsUiAutomation.PlannedCoverage,
            windowsUiAutomation.SupportedFlows.Count,
            "External Windows UI execution has not been imported yet.");

        var resultBundlePath = GetResultBundlePath(outputDirectory);
        if (!File.Exists(resultBundlePath))
        {
            return new ImportedProofBundleSummary(null, plannedRuntime, plannedLiveGame, plannedDesktop);
        }

        try
        {
            var bundle = ReadJson<ImportedProofResultBundle>(resultBundlePath);
            if (bundle is null)
            {
                return new ImportedProofBundleSummary(
                    null,
                    plannedRuntime with { ExecutedStatus = "import-error", Notes = plannedRuntime.Notes.Concat(["proof-result-bundle.json could not be parsed."]).ToArray() },
                    plannedLiveGame with { ExecutedStatus = "import-error", Notes = plannedLiveGame.Notes.Concat(["proof-result-bundle.json could not be parsed."]).ToArray() },
                    plannedDesktop with { ExecutedStatus = "import-error", Notes = plannedDesktop.Notes.Concat(["proof-result-bundle.json could not be parsed."]).ToArray() });
            }

            var hostDetails = BuildHostDetails(bundle.Host);
            var hostLooksWindows = HostLooksWindows(bundle.Host);
            var runtimeProof = BuildRuntimeProofExecution(bundle, runtimePlan, hostDetails);
            var liveGameProof = BuildLiveGameProofExecution(bundle, liveGameExecution, hostDetails, hostLooksWindows);
            var desktopProof = BuildDesktopProofExecution(bundle, windowsUiAutomation, hostDetails, hostLooksWindows);
            return new ImportedProofBundleSummary(bundle, runtimeProof, liveGameProof, desktopProof);
        }
        catch (Exception ex)
        {
            return new ImportedProofBundleSummary(
                null,
                plannedRuntime with { ExecutedStatus = "import-error", Notes = plannedRuntime.Notes.Concat([$"proof-result-bundle.json import failed: {ex.Message}"]).ToArray() },
                plannedLiveGame with { ExecutedStatus = "import-error", Notes = plannedLiveGame.Notes.Concat([$"proof-result-bundle.json import failed: {ex.Message}"]).ToArray() },
                plannedDesktop with { ExecutedStatus = "import-error", Notes = plannedDesktop.Notes.Concat([$"proof-result-bundle.json import failed: {ex.Message}"]).ToArray() });
        }
    }

    private static ProofExecutionState BuildRuntimeProofExecution(
        ImportedProofResultBundle bundle,
        RuntimeValidationExecutionPlan runtimePlan,
        IReadOnlyList<string> hostDetails)
    {
        var expectedProbeIds = (runtimePlan.AutomationHarness?.Probes ?? [])
            .Select(static probe => probe.ProbeId)
            .Where(static probeId => !string.IsNullOrWhiteSpace(probeId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var probeResults = bundle.ProbeResults
            .Where(result => !string.IsNullOrWhiteSpace(result.ProbeId))
            .GroupBy(result => result.ProbeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var component = bundle.ComponentResults.FirstOrDefault(static result =>
            string.Equals(result.Component, "runtime-automation", StringComparison.OrdinalIgnoreCase));
        var missingProbeIds = expectedProbeIds
            .Where(id => !probeResults.ContainsKey(id))
            .Concat(component?.MissingItems ?? [])
            .Concat(bundle.MissingExpectedProbes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var failedProbeIds = probeResults.Values
            .Where(static result => !StatusMeansPass(result.Status))
            .Select(static result => result.ProbeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var evidence = (component?.EvidenceArtifacts ?? [])
            .Concat(probeResults.Values.SelectMany(static result => result.EvidenceArtifacts))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingEvidenceItems = BuildMissingEvidenceItems(
            evidence,
            ("runtime-logs", $"{EvidenceRootDirectory}/runtime-logs/"),
            ("step-traces", $"{EvidenceRootDirectory}/step-traces/"),
            ("probe-observations", $"{EvidenceRootDirectory}/probe-observations/"));
        var probesMissingEvidence = probeResults.Values
            .Where(static result => result.EvidenceArtifacts.Count == 0)
            .Select(static result => $"probe-evidence:{result.ProbeId}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var componentMissingArtifacts = FilterMissingExpectedArtifacts(
            bundle.MissingExpectedArtifacts,
            $"{EvidenceRootDirectory}/runtime-logs/",
            $"{EvidenceRootDirectory}/step-traces/",
            $"{EvidenceRootDirectory}/probe-observations/");
        var notes = (component?.Notes ?? [])
            .Concat(bundle.Notes)
            .Concat(missingEvidenceItems.Length > 0 ? [$"Runtime proof import is missing expected evidence categories: {string.Join(", ", missingEvidenceItems)}"] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var executedItemCount = probeResults.Count;
        var missingItemCount = missingProbeIds.Length + probesMissingEvidence.Length + missingEvidenceItems.Length + componentMissingArtifacts.Length;
        var failureCount = failedProbeIds.Length;
        var strict = executedItemCount > 0 && missingItemCount == 0 && failureCount == 0;
        var executedStatus = DetermineExecutionStatus(component?.Status, strict, missingItemCount, failureCount);

        return new ProofExecutionState(
            Axis: "runtime-automation",
            PlannedStatus: runtimePlan.PlannedExecutionCoverage,
            ExecutedStatus: executedStatus,
            ImportedResultAvailable: true,
            StrictProofSatisfied: strict,
            PlannedItemCount: expectedProbeIds.Length,
            ExecutedItemCount: executedItemCount,
            MissingItemCount: missingItemCount,
            MissingItems: missingProbeIds
                .Concat(failedProbeIds)
                .Concat(probesMissingEvidence)
                .Concat(missingEvidenceItems)
                .Concat(componentMissingArtifacts)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            EvidenceArtifacts: evidence,
            HostDetails: hostDetails,
            Notes: notes);
    }

    private static ProofExecutionState BuildLiveGameProofExecution(
        ImportedProofResultBundle bundle,
        LiveGameExecutionPlan liveGameExecution,
        IReadOnlyList<string> hostDetails,
        bool hostLooksWindows)
    {
        var expectedScenarioProfiles = liveGameExecution.ScenarioProfiles
            .ToDictionary(static profile => profile.Name, StringComparer.OrdinalIgnoreCase);
        var expectedScenarioNames = expectedScenarioProfiles.Keys.ToArray();
        var scenarioResults = bundle.ScenarioResults
            .Where(result => !string.IsNullOrWhiteSpace(result.Scenario))
            .GroupBy(result => result.Scenario, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var component = bundle.ComponentResults.FirstOrDefault(static result =>
            string.Equals(result.Component, "live-game-execution", StringComparison.OrdinalIgnoreCase));
        var missingScenarioNames = expectedScenarioNames
            .Where(name => !scenarioResults.ContainsKey(name))
            .Concat(component?.MissingItems ?? [])
            .Concat(bundle.MissingExpectedScenarios)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var failedScenarioNames = scenarioResults.Values
            .Where(static result => !StatusMeansPass(result.Status) || result.MissingSignals.Count > 0)
            .Select(static result => result.Scenario)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var scenarioEvidenceMismatches = scenarioResults.Values
            .Where(result => expectedScenarioProfiles.TryGetValue(result.Scenario, out var profile) &&
                             !ContainsEvidencePrefix(result.EvidenceArtifacts, $"{EvidenceRootDirectory}/scenario-observations/{BuildScenarioEvidenceKey(profile.Name)}/"))
            .Select(static result => $"scenario-evidence:{result.Scenario}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var scenarioSaveProfileMismatches = scenarioResults.Values
            .Where(result => expectedScenarioProfiles.TryGetValue(result.Scenario, out var profile) &&
                             !string.Equals(result.ValidationSaveProfile, profile.ValidationSaveProfile, StringComparison.OrdinalIgnoreCase))
            .Select(static result => $"scenario-save-profile:{result.Scenario}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var evidence = (component?.EvidenceArtifacts ?? [])
            .Concat(scenarioResults.Values.SelectMany(static result => result.EvidenceArtifacts))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingEvidenceItems = BuildMissingEvidenceItems(
            evidence,
            ("screenshots", $"{EvidenceRootDirectory}/screenshots/"),
            ("runtime-logs", $"{EvidenceRootDirectory}/runtime-logs/"),
            ("scenario-observations", $"{EvidenceRootDirectory}/scenario-observations/"),
            ("load-order-state", $"{EvidenceRootDirectory}/load-order-state/"));
        var componentMissingArtifacts = FilterMissingExpectedArtifacts(
            bundle.MissingExpectedArtifacts,
            $"{EvidenceRootDirectory}/screenshots/",
            $"{EvidenceRootDirectory}/runtime-logs/",
            $"{EvidenceRootDirectory}/scenario-observations/",
            $"{EvidenceRootDirectory}/load-order-state/");
        var hostRequirementItems = hostLooksWindows ? Array.Empty<string>() : ["host:windows"];
        var notes = (component?.Notes ?? [])
            .Concat(bundle.Notes)
            .Concat(hostLooksWindows ? Array.Empty<string>() : ["Live-game proof was imported from a non-Windows host."])
            .Concat(missingEvidenceItems.Length > 0 ? [$"Live-game proof import is missing expected evidence categories: {string.Join(", ", missingEvidenceItems)}"] : Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var executedItemCount = scenarioResults.Count;
        var missingItemCount = missingScenarioNames.Length + scenarioEvidenceMismatches.Length + missingEvidenceItems.Length + componentMissingArtifacts.Length;
        var failureCount = failedScenarioNames.Length + scenarioSaveProfileMismatches.Length + hostRequirementItems.Length;
        var strict = executedItemCount > 0 && missingItemCount == 0 && failureCount == 0;
        var executedStatus = DetermineExecutionStatus(component?.Status, strict, missingItemCount, failureCount);

        return new ProofExecutionState(
            Axis: "live-game-execution",
            PlannedStatus: liveGameExecution.PlannedIntegrationCoverage,
            ExecutedStatus: executedStatus,
            ImportedResultAvailable: true,
            StrictProofSatisfied: strict,
            PlannedItemCount: expectedScenarioNames.Length,
            ExecutedItemCount: executedItemCount,
            MissingItemCount: missingItemCount,
            MissingItems: missingScenarioNames
                .Concat(failedScenarioNames)
                .Concat(scenarioEvidenceMismatches)
                .Concat(scenarioSaveProfileMismatches)
                .Concat(missingEvidenceItems)
                .Concat(componentMissingArtifacts)
                .Concat(hostRequirementItems)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            EvidenceArtifacts: evidence,
            HostDetails: hostDetails,
            Notes: notes);
    }

    private static ProofExecutionState BuildDesktopProofExecution(
        ImportedProofResultBundle bundle,
        WindowsUiE2EAutomationPlan windowsUiAutomation,
        IReadOnlyList<string> hostDetails,
        bool hostLooksWindows)
    {
        var expectedFlows = windowsUiAutomation.SupportedFlows;
        var component = bundle.ComponentResults.FirstOrDefault(static result =>
            string.Equals(result.Component, "desktop-e2e", StringComparison.OrdinalIgnoreCase));
        var executedFlows = component?.ExecutedItems ?? [];
        var missingFlows = expectedFlows
            .Where(flow => !executedFlows.Contains(flow, StringComparer.OrdinalIgnoreCase))
            .Concat(component?.MissingItems ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var evidence = component?.EvidenceArtifacts?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        var missingEvidenceItems = BuildMissingEvidenceItems(
            evidence,
            ("screenshots", $"{EvidenceRootDirectory}/screenshots/"),
            ("step-traces", $"{EvidenceRootDirectory}/step-traces/"),
            ("selector-logs", $"{EvidenceRootDirectory}/selector-logs/"));
        var componentMissingArtifacts = FilterMissingExpectedArtifacts(
            bundle.MissingExpectedArtifacts,
            $"{EvidenceRootDirectory}/screenshots/",
            $"{EvidenceRootDirectory}/step-traces/",
            $"{EvidenceRootDirectory}/selector-logs/");
        var hostRequirementItems = hostLooksWindows ? Array.Empty<string>() : ["host:windows"];
        var notes = (component?.Notes ?? [])
            .Concat(bundle.Notes)
            .Concat(hostLooksWindows ? Array.Empty<string>() : ["Desktop E2E proof was imported from a non-Windows host."])
            .Concat(missingEvidenceItems.Length > 0 ? [$"Desktop proof import is missing expected evidence categories: {string.Join(", ", missingEvidenceItems)}"] : Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingItemCount = missingFlows.Length + missingEvidenceItems.Length + componentMissingArtifacts.Length;
        var failureCount = (StatusMeansFail(component?.Status) ? 1 : 0) + hostRequirementItems.Length;
        var strict = executedFlows.Count > 0 && missingItemCount == 0 && failureCount == 0 && StatusMeansPass(component?.Status);
        var executedStatus = DetermineExecutionStatus(component?.Status, strict, missingItemCount, failureCount);

        return new ProofExecutionState(
            Axis: "desktop-e2e",
            PlannedStatus: windowsUiAutomation.PlannedCoverage,
            ExecutedStatus: executedStatus,
            ImportedResultAvailable: component is not null,
            StrictProofSatisfied: strict,
            PlannedItemCount: expectedFlows.Count,
            ExecutedItemCount: executedFlows.Count,
            MissingItemCount: missingItemCount,
            MissingItems: missingFlows
                .Concat(missingEvidenceItems)
                .Concat(componentMissingArtifacts)
                .Concat(hostRequirementItems)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            EvidenceArtifacts: evidence,
            HostDetails: hostDetails,
            Notes: notes);
    }

    private static IReadOnlyList<ProofHarnessHostRequirement> BuildHostRequirements(
        LiveGameExecutionPlan liveGameExecution,
        WindowsUiE2EAutomationPlan windowsUiAutomation)
    {
        var requirements = new List<ProofHarnessHostRequirement>();

        requirements.Add(new ProofHarnessHostRequirement(
            "desktop-e2e",
            "windows-host",
            "Desktop UI proof requires a Windows-capable host that can drive the WinForms application and capture UI evidence.",
            true));
        requirements.AddRange(windowsUiAutomation.BootstrapContract.LaunchActions.Select(static action =>
            new ProofHarnessHostRequirement("desktop-e2e", action, "Required Windows UI runner launch/action capability.", true)));
        requirements.Add(new ProofHarnessHostRequirement(
            "desktop-e2e",
            "webview-runtime",
            windowsUiAutomation.RequiresEmbeddedPreviewRuntimeForInAppPreview
                ? "Embedded preview/runtime support is required to validate the in-app preview path."
                : "Embedded preview/runtime support is optional for this run.",
            windowsUiAutomation.RequiresEmbeddedPreviewRuntimeForInAppPreview));
        requirements.Add(new ProofHarnessHostRequirement(
            "desktop-e2e",
            "evidence-capture",
            "The harness must emit screenshots plus replayable step traces under proof-evidence/ for every required Desktop flow.",
            true));

        requirements.Add(new ProofHarnessHostRequirement(
            "live-game-execution",
            "windows-host",
            liveGameExecution.RequiresWindowsHost
                ? "Live-game proof requires a Windows host with the target Skyrim installation and external launcher control."
                : "A Windows host is recommended for the supported external live-game workflow.",
            liveGameExecution.RequiresWindowsHost));
        requirements.Add(new ProofHarnessHostRequirement(
            "live-game-execution",
            "game-install",
            "The host must provide the target Skyrim runtime plus the converted output staged into the expected mod stack.",
            true));
        requirements.Add(new ProofHarnessHostRequirement(
            "live-game-execution",
            "mod-stack-prerequisites",
            liveGameExecution.RequiresDeployedModManagerLoadOrder
                ? "A deployed mod-manager load order matching the exported output is required for live-game validation."
                : "A representative mod stack is recommended for live-game validation.",
            liveGameExecution.RequiresDeployedModManagerLoadOrder));
        requirements.AddRange(liveGameExecution.RequiredHostCapabilities.Select(static capability =>
            new ProofHarnessHostRequirement("live-game-execution", capability, "Required capability for the live-game external runner.", true)));
        requirements.Add(new ProofHarnessHostRequirement(
            "live-game-execution",
            "launcher-control",
            liveGameExecution.RequiresSkseOrEquivalentLauncher
                ? "The external harness must be able to launch through SKSE or the equivalent game loader."
                : "Direct game-launch control is sufficient.",
            liveGameExecution.RequiresSkseOrEquivalentLauncher));
        requirements.Add(new ProofHarnessHostRequirement(
            "live-game-execution",
            "validation-save-profiles",
            string.Join(", ", liveGameExecution.ValidationSaveProfiles),
            true));
        requirements.Add(new ProofHarnessHostRequirement(
            "live-game-execution",
            "observation-channels",
            string.Join(", ", liveGameExecution.ObservationChannels),
            liveGameExecution.ObservationChannels.Count > 0));
        requirements.Add(new ProofHarnessHostRequirement(
            "live-game-execution",
            "evidence-capture",
            "The harness must emit scenario observations, screenshots, and runtime logs under proof-evidence/ so imported proof can be replayed.",
            true));

        return requirements
            .GroupBy(static requirement => $"{requirement.Scope}|{requirement.Requirement}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static string DetermineExecutionStatus(string? componentStatus, bool strict, int missingCount, int failureCount)
    {
        if (strict)
        {
            return "executed-pass";
        }

        if (failureCount > 0 || StatusMeansFail(componentStatus))
        {
            return "executed-fail";
        }

        return missingCount > 0 || string.Equals(componentStatus, "incomplete", StringComparison.OrdinalIgnoreCase)
            ? "executed-incomplete"
            : "executed-fail";
    }

    private static string DeriveOverallProofExecutionStatus(ImportedProofBundleSummary summary)
    {
        var statuses = new[] { summary.RuntimeProof.ExecutedStatus, summary.LiveGameProof.ExecutedStatus, summary.DesktopProof.ExecutedStatus };
        if (statuses.All(static status => string.Equals(status, "planned-only", StringComparison.OrdinalIgnoreCase)))
        {
            return "planned-only";
        }

        if (statuses.Any(static status => string.Equals(status, "import-error", StringComparison.OrdinalIgnoreCase)))
        {
            return "import-error";
        }

        if (statuses.Any(static status => string.Equals(status, "executed-fail", StringComparison.OrdinalIgnoreCase)))
        {
            return "executed-fail";
        }

        if (statuses.Any(static status => string.Equals(status, "executed-incomplete", StringComparison.OrdinalIgnoreCase)))
        {
            return "executed-incomplete";
        }

        return statuses.Any(static status => string.Equals(status, "executed-pass", StringComparison.OrdinalIgnoreCase))
            ? "executed-pass"
            : "planned-only";
    }

    private static IReadOnlyList<string> BuildHostDetails(ImportedProofHostDetails host)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(host.OperatingSystem))
        {
            values.Add($"os:{host.OperatingSystem}");
        }

        if (!string.IsNullOrWhiteSpace(host.HarnessRunner))
        {
            values.Add($"runner:{host.HarnessRunner}");
        }

        if (!string.IsNullOrWhiteSpace(host.Launcher))
        {
            values.Add($"launcher:{host.Launcher}");
        }

        if (!string.IsNullOrWhiteSpace(host.ModManager))
        {
            values.Add($"mod-manager:{host.ModManager}");
        }

        if (!string.IsNullOrWhiteSpace(host.SaveProfile))
        {
            values.Add($"save-profile:{host.SaveProfile}");
        }

        if (!string.IsNullOrWhiteSpace(host.ObservationMode))
        {
            values.Add($"observation-mode:{host.ObservationMode}");
        }

        values.AddRange(host.Capabilities.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => $"capability:{value}"));
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool StatusMeansPass(string? status) =>
        string.Equals(status, "pass", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "passed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

    private static bool StatusMeansFail(string? status) =>
        string.Equals(status, "fail", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "error", StringComparison.OrdinalIgnoreCase);

    private static bool HostLooksWindows(ImportedProofHostDetails host) =>
        !string.IsNullOrWhiteSpace(host.OperatingSystem) &&
        host.OperatingSystem.Contains("windows", StringComparison.OrdinalIgnoreCase);

    private static string[] BuildMissingEvidenceItems(
        IEnumerable<string> evidenceArtifacts,
        params (string Label, string Prefix)[] requiredPrefixes)
    {
        return requiredPrefixes
            .Where(requirement => !ContainsEvidencePrefix(evidenceArtifacts, requirement.Prefix))
            .Select(static requirement => $"evidence:{requirement.Label}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] FilterMissingExpectedArtifacts(
        IEnumerable<string> missingExpectedArtifacts,
        params string[] relevantPrefixes)
    {
        return missingExpectedArtifacts
            .Where(artifact => relevantPrefixes.Any(prefix => NormalizeEvidencePath(artifact).StartsWith(NormalizeEvidencePath(prefix), StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ContainsEvidencePrefix(IEnumerable<string> evidenceArtifacts, string expectedPrefix)
    {
        var normalizedPrefix = NormalizeEvidencePath(expectedPrefix);
        return evidenceArtifacts.Any(artifact => NormalizeEvidencePath(artifact).StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeEvidencePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim();

    private static string BuildScenarioEvidenceKey(string scenarioName)
    {
        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            return "scenario";
        }

        var builder = new StringBuilder();
        foreach (var character in scenarioName.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length == 0 || builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    private static T? ReadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream);
    }

    private static void WriteJsonIfChanged<T>(string path, T value)
    {
        var serialized = JsonSerializer.Serialize(value, JsonOptions);
        var existing = File.Exists(path) ? File.ReadAllText(path) : null;
        if (!string.Equals(existing, serialized, StringComparison.Ordinal))
        {
            File.WriteAllText(path, serialized);
        }
    }
}
