namespace Bodyslide.Core;

public static class ConversionMatrixProofGuidance
{
    private static readonly string[] DefaultArtifacts = ["conversion-matrix-proof.json"];

    public static string BuildAxisActionText(string? axis) =>
        NormalizeAxis(axis) switch
        {
            "topology-transfer" or "strict-layout" =>
                "Hard topology correspondence is not fully proven yet. Open topology-correspondence.json and preview-workbench.html, then review the highest-risk regions before treating the conversion as universal.",
            "desktop-e2e" =>
                "Windows desktop end-to-end coverage still depends on an external harness. Open desktop-workflow-automation.json or windows-ui-e2e-automation.json and complete that click-path proof on a Windows host.",
            "live-game-execution" =>
                "Live-game validation is still external. Open live-game-execution.json and complete the scenario/save-driven proof on the target Windows mod stack before release.",
            "runtime-automation" =>
                "Runtime automation is still exported as a harness contract. Open runtime-validation-plan.json and runtime-validation-harness.json to finish the missing automation proof outside the desktop app.",
            "plugin-modstack" =>
                "Mixed body × skeleton × plugin-family coverage still needs stricter matrix proof. Open mod-stack-cross-validation.json and plugin-patches.json, then validate the real load-order combination before release.",
            "custom-skeleton" =>
                "Custom skeleton proof still needs stronger direct evidence. Open skeleton-compatibility.json and review any sparse-inference or unsupported-bone warnings before release.",
            "body-support" =>
                "Target-body support is not yet proven as universal. Open conversion-matrix-proof.json and conversion-quality.json, then review the remaining support-tier gaps for this body family.",
            _ =>
                $"Open conversion-matrix-proof.json and resolve the remaining '{axis ?? "unknown"}' proof gap before treating this output as universally covered."
        };

    public static IReadOnlyList<string> GetPreferredArtifactsForAxis(string? axis) =>
        NormalizeAxis(axis) switch
        {
            "topology-transfer" or "strict-layout" => ["topology-correspondence.json", "preview-workbench.html"],
            "desktop-e2e" => ["desktop-workflow-automation.json", "windows-ui-e2e-automation.json"],
            "live-game-execution" => ["live-game-execution.json", "runtime-validation-plan.json"],
            "runtime-automation" => ["runtime-validation-harness.json", "runtime-validation-plan.json"],
            "plugin-modstack" => ["mod-stack-cross-validation.json", "plugin-patches.json"],
            "custom-skeleton" => ["skeleton-compatibility.json"],
            "body-support" => ["conversion-matrix-proof.json", "conversion-quality.json"],
            _ => DefaultArtifacts
        };

    public static IReadOnlyList<string> GetPreferredArtifactsForAxes(IEnumerable<string>? axes)
    {
        var normalized = axes?
            .Select(NormalizeAxis)
            .Where(static axis => !string.IsNullOrWhiteSpace(axis))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalized is null || normalized.Count == 0)
        {
            return DefaultArtifacts;
        }

        foreach (var preferredAxis in new[]
                 {
                     "desktop-e2e",
                     "runtime-automation",
                     "live-game-execution",
                     "plugin-modstack",
                     "topology-transfer",
                     "strict-layout",
                     "custom-skeleton",
                     "body-support"
                 })
        {
            if (normalized.Contains(preferredAxis))
            {
                return GetPreferredArtifactsForAxis(preferredAxis);
            }
        }

        return DefaultArtifacts;
    }

    private static string? NormalizeAxis(string? axis) =>
        axis?.Trim().ToLowerInvariant() switch
        {
            "topology" => "topology-transfer",
            "live-game" => "live-game-execution",
            "skeleton" => "custom-skeleton",
            { Length: > 0 } normalized => normalized,
            _ => null
        };
}
