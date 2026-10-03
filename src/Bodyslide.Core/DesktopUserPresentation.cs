namespace Bodyslide.Core;

internal static class DesktopUserPresentation
{
    internal static IReadOnlyList<DesktopWorkflowSummaryRow> BuildOverview(
        IReadOnlyList<DesktopWorkflowSummaryRow> rows) =>
        rows.Where(row => row.Property is
            "Items converted" or "Output" or "Output directory" or "Output files" or
            "Desktop status" or "Validation gate" or "Pack status" or "Needs review" or
            "High risk" or "Top packaging issues" or "Detected body" or "Detected source body" or
            "Source body (override)" or "Target body" or "Mesh type" or "Physics profile" or
            "Physics (override)" or "Skeleton warnings" or "Texture warnings" or
            "Can safely animate" or "Manual cleanup likely" or "Runtime verification required")
            .Select(row => row with
            {
                Property = row.Property switch
                {
                    "Desktop status" => "Conversion result",
                    "Validation gate" => "Before installing",
                    "Output" or "Output directory" => "Output folder",
                    "Pack status" => "Installation package",
                    "High risk" => "High-risk items",
                    _ => row.Property
                }
            }).ToArray();

    internal static IReadOnlyList<DesktopWorkflowSummaryRow> BuildInspection(
        ConversionInspectionResult inspection, double confidenceFloor)
    {
        var rows = new List<DesktopWorkflowSummaryRow>
        {
            new("Summary", "Inspection complete. No conversion has been run by this inspection."),
            new("Input", inspection.InputPath),
            new("Detected body", $"{inspection.Detection.Body} ({inspection.Detection.Confidence:P0} confidence)"),
            new("Armor type", inspection.Analysis.MeshType),
            new("Files found", $"{inspection.Armor.MeshFiles.Count} meshes, {inspection.Armor.TextureFiles.Count} textures, {inspection.Armor.PhysicsFiles.Count} physics files"),
            new("Source physics", inspection.Analysis.PhysicsEnabled ? "Detected" : "Not detected")
        };
        var actions = new List<string>();
        if (inspection.Armor.MeshFiles.Count == 0)
            actions.Add("No meshes found. Select the armor's mesh folder or a complete mod archive.");
        if (inspection.Detection.Confidence < confidenceFloor)
            actions.Add("Body detection is uncertain. Set FROM body manually before converting.");
        if (string.IsNullOrWhiteSpace(inspection.RequestedTargetBody))
            actions.Add("Choose a TO body or preset to check compatibility and convert.");
        else
            rows.Add(new("Target body", inspection.RequestedTargetBody));

        if (inspection.SkeletonMapping is { } mapping)
        {
            var needsReview = mapping.UnsupportedBones.Count > 0 ||
                !string.Equals(mapping.AutomaticRemapSafety, "safe", StringComparison.OrdinalIgnoreCase);
            rows.Add(new("Skeleton compatibility", needsReview
                ? "Needs review. Some bone mappings are unsupported or uncertain."
                : "No bone-mapping concerns reported. In-game behavior is not yet verified."));
            if (needsReview)
                actions.Add("Select the correct skeleton support file, then inspect again. Review skeleton details in Log before converting.");
        }
        else if (!string.IsNullOrWhiteSpace(inspection.RequestedTargetBody))
            actions.Add("Skeleton compatibility was not checked. Confirm your target and skeleton support, then inspect again.");

        var unsupported = inspection.NifSupport?.Count(report =>
            string.Equals(report.Status, "unsupported", StringComparison.OrdinalIgnoreCase)) ?? 0;
        if (unsupported > 0)
            actions.Add($"{unsupported} mesh file(s) could not be read or are unsupported. Select supported Skyrim meshes; see Log for file details.");
        if (inspection.Analysis.IsFootwear || inspection.NifSupport?.Any(report => report.HeelAnalysis is not null) == true)
            rows.Add(new("Footwear", "Detected. Check heel height and ground contact in Preview after conversion."));
        rows.Add(new("What to do next", actions.Count > 0
            ? string.Join(" ", actions)
            : "Confirm FROM / TO body and output folder, then start conversion. Review Preview and Next actions before installing."));
        return rows;
    }

    internal static IReadOnlyList<RuntimeReadinessCheck> BuildReadiness(IReadOnlyList<RuntimeReadinessCheck> checks) =>
        checks.OrderByDescending(check => check.Status.Equals("Error", StringComparison.OrdinalIgnoreCase) ? 3 :
                check.Status.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 2 :
                check.Status.Equals("Info", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .Select(PresentReadiness).ToArray();

    private static RuntimeReadinessCheck PresentReadiness(RuntimeReadinessCheck check)
    {
        var ok = check.Status.Equals("OK", StringComparison.OrdinalIgnoreCase);
        var info = check.Status.Equals("Info", StringComparison.OrdinalIgnoreCase);
        var action = check.Area switch
        {
            "Runtime" => "Application runtime is available.",
            "Input support" => "Select a Skyrim mesh, plugin, armor folder, or mod archive.",
            "Catalog coverage" => "Body types and conversion presets are available in the selectors.",
            "Catalog data" => ok ? "Built-in body and skeleton data checks passed." :
                "Built-in body or skeleton data failed validation. Reinstall or update SlideSmith; see Log for the exact issue.",
            "Executable" => ok ? "Application executable was found." : "Reinstall or launch SlideSmith from its installation folder.",
            "Core pipeline" => ok ? "Conversion components loaded." : "Conversion components could not load. Reinstall SlideSmith and review Log.",
            "NIF parsing" => ok ? "Mesh-reading support is available." : "Mesh-reading support could not be checked. Review Log and try a supported Skyrim mesh.",
            "Learning cache" => ok ? "Learning-cache location is available." : "Check the cache folder setting and its permissions; see Cache and Log.",
            "Scratch write" => ok ? "Working-file write check passed." : "Free disk space and check working-folder write permissions before converting.",
            "Startup crash log" => ok ? "Diagnostic-log write check passed." : "Check installation-folder permissions so startup failures can be recorded.",
            "CLI companion" => ok ? "Optional command-line application found." : "Optional: only needed for command-line workflows. Desktop conversion does not require it.",
            "Universal coverage" or "Strict matrix proof" or "Desktop automation proof" =>
                ok ? "External validation evidence was found. This does not verify your own conversion." :
                "Broader application validation is not complete. This is not a failed conversion; inspect your own output in Preview and follow Next actions. See Log for validation details.",
            "Live-game automation proof" => ok ? "External game-test evidence was found. Your installed mod stack still needs testing." :
                "Game testing has not been verified here. Test your converted armor in your own game before relying on it.",
            "Latest output verification" => "Not checked yet. Run a conversion or load an output folder, then run Self-check.",
            "Skyrim package recognition" => ok ? "Installation package files were found; still review Preview before installing." :
                "Package files are missing. Re-run conversion and review Files and Next actions before installing.",
            "BodySlide recognizability" => ok ? "BodySlide project and shape files were found." :
                "BodySlide output is incomplete. Enable BodySlide output and provide source project / shape data, then convert again.",
            "Proof blockers" => ok ? "The loaded output's verification checklist reports no remaining blockers. Still test it in game." :
                info ? "Output verification is not complete. Re-run conversion to generate the checklist, then follow Next actions." :
                "Output verification needs attention. Follow Next actions and the remaining-gaps checklist in Reports before installing or sharing.",
            "Preview runtime" => ok ? (check.Details.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? "Embedded preview is unavailable. Preview will open in your browser instead."
                : "Embedded preview is available.") : "Use Open preview to view the output in your browser; see Log for the embedded-preview issue.",
            "Mod manager health check" => info ? "Optional: no mod manager was detected. Use Copy Mod Manager setup if you want to launch from MO2 or Vortex." :
                "Mod manager launch context detected. Keep its launcher pointed at the desktop application.",
            "Mod manager executable target" or "Mod manager launcher arguments" => ok ? "Mod manager launcher check passed." :
                "Use Copy Mod Manager setup to correct the desktop executable, working folder, and launcher arguments.",
            _ => $"{check.Details} Review Reports / Log for details and next steps."
        };
        return new(check.Area, info ? check.Area == "CLI companion" || check.Area == "Mod manager health check"
            ? "Optional" : "Not verified" : check.Status, action);
    }
}
