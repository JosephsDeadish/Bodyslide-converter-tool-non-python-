using System.Runtime.InteropServices;
using System.Text.Json;

namespace Bodyslide.Core;

public sealed record RuntimeReadinessCheck(string Area, string Status, string Details);

public static class RuntimeReadinessReporter
{
    private sealed record ProofCoverageSnapshot(
        string? ReportPath,
        bool HasPackProofReport,
        bool StrictProofReady,
        int RemainingGapCount,
        int MissingMatrixDimensionsCount,
        int MissingMatrixCombinationsCount,
        IReadOnlySet<string> MissingProofAxes);

    private static readonly string[] DesktopCompanionExeNames =
    [
        "SlideSmith.exe",
        "SlideSmith-Desktop.exe",
    ];

    private static readonly string[] CliCompanionExeNames =
    [
        "SlideSmith-CLI.exe",
        "SlideSmith.CLI.exe",
    ];

    private static readonly string[] SupportedInputs =
    [
        ".nif mesh",
        ".esp/.esm/.esl plugin",
        "armor folder",
        ".zip/.7z/.tar/.tar.gz/.tgz archive",
    ];

    public static IReadOnlyList<RuntimeReadinessCheck> CreateCliReport(string? currentExePath) =>
        CreateReport(currentExePath, includeDesktopProbe: true, includeCliProbe: false);

    public static IReadOnlyList<RuntimeReadinessCheck> CreateDesktopReport(string? currentExePath) =>
        CreateReport(currentExePath, includeDesktopProbe: false, includeCliProbe: true);

    internal static IReadOnlyList<RuntimeReadinessCheck> CreateDesktopReport(string? currentExePath, string? proofOutputRootOverride) =>
        CreateReport(currentExePath, includeDesktopProbe: false, includeCliProbe: true, proofOutputRootOverride: proofOutputRootOverride);

    private static IReadOnlyList<RuntimeReadinessCheck> CreateReport(
        string? currentExePath,
        bool includeDesktopProbe,
        bool includeCliProbe,
        string? proofOutputRootOverride = null)
    {
        var proofCoverage = BuildProofCoverageSnapshot(proofOutputRootOverride);
        var checks = new List<RuntimeReadinessCheck>
        {
            new(
                "Runtime",
                "OK",
                $"{RuntimeInformation.OSDescription}; {RuntimeInformation.OSArchitecture}; process={RuntimeInformation.ProcessArchitecture}; .NET {Environment.Version}"),
            new(
                "Input support",
                "OK",
                string.Join(", ", SupportedInputs)),
            new(
                "Catalog coverage",
                "OK",
                $"{PresetCatalog.All.Count} presets, {BodyTypeCatalog.All.Count} body types, {DeformationProfileModifier.All.Count} deformation profiles, {PhysicsProfileCatalog.All.Count} physics profiles"),
        };

        checks.Add(CreateCatalogDataCheck());
        checks.Add(CreateExecutableCheck(currentExePath));
        checks.Add(CreatePipelineCheck());
        checks.Add(CreateNifParsingCheck());
        checks.Add(CreateCacheCheck());
        checks.Add(CreateScratchWriteCheck());
        checks.Add(CreateStartupCrashLogWriteCheck());
        checks.Add(CreateUniversalCoverageCheck(proofCoverage));
        checks.Add(CreateDesktopAutomationCoverageCheck(proofCoverage));
        checks.Add(CreateLiveGameAutomationCoverageCheck(proofCoverage));
        checks.Add(CreateStrictMatrixCoverageCheck(proofCoverage));

        if (includeDesktopProbe)
        {
            checks.Add(CreateDesktopProbeCheck(currentExePath));
        }

        if (includeCliProbe)
        {
            checks.Add(CreateCliProbeCheck(currentExePath));
        }

        return checks;
    }

    private static RuntimeReadinessCheck CreateCatalogDataCheck()
    {
        try
        {
            var builtInBodies = BuiltInBodyMetadataCatalog.All;
            var aliasCount = builtInBodies
                .SelectMany(static body => body.Aliases)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var skeletonFrameworkCount = SkeletonFrameworkCatalog.All.Count;
            var skeletonCommonBoneCount = SkeletonMappingCatalog.CommonBones.Count;
            var raceCompatibilityRuleCount = RaceCompatibilityCatalog.BodyRules.Count;
            var physicsRepairGroupCount = PhysicsRepairCatalog.All.Count;
            var explicitSupportMetadataCount = builtInBodies.Count(static body => body.HasExplicitSupportMetadata);
            var consistencyIssues = ValidateCatalogConsistency();
            _ = BodyDetectionTuningCatalog.Current;
            _ = MeshBehaviorCatalog.Get("cloth");

            return new(
                "Catalog data",
                consistencyIssues.Count == 0 ? "OK" : "Error",
                consistencyIssues.Count == 0
                    ? $"{builtInBodies.Count} built-in bodies, {aliasCount} body aliases, {skeletonFrameworkCount} skeleton frameworks, {skeletonCommonBoneCount} common skeleton bones, {raceCompatibilityRuleCount} race rules, {physicsRepairGroupCount} physics repair groups, explicit support expectations for {explicitSupportMetadataCount}/{builtInBodies.Count} bodies, verified body physics bone coverage"
                    : $"{builtInBodies.Count} built-in bodies, {aliasCount} body aliases, {skeletonFrameworkCount} skeleton frameworks, {skeletonCommonBoneCount} common skeleton bones, {raceCompatibilityRuleCount} race rules, {physicsRepairGroupCount} physics repair groups, explicit support expectations for {explicitSupportMetadataCount}/{builtInBodies.Count} bodies; catalog consistency issues: {string.Join(" | ", consistencyIssues.Take(5))}");
        }
        catch (Exception ex)
        {
            return new("Catalog data", "Error", $"Embedded application data catalogs failed to load: {ex.Message}");
        }
    }

    internal static IReadOnlyList<string> ValidateCatalogConsistency()
    {
        var issues = new List<string>();

        foreach (var body in BuiltInBodyMetadataCatalog.All.OrderBy(static body => body.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!BodyTechnicalProfileCatalog.TryGet(body.Name, out var profile))
            {
                issues.Add($"Built-in body '{body.Name}' is missing its technical profile.");
                continue;
            }

            var requiredBones = profile.RequiredPhysicsBones;
            foreach (var bone in body.AvailablePhysicsBones
                         .Concat(requiredBones)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (SkeletonMappingCatalog.TryResolveSupportedBone(bone, body.SkeletonFramework, out _))
                {
                    continue;
                }

                issues.Add($"{body.Name}: physics bone '{bone}' is not covered by skeleton framework '{body.SkeletonFramework}'");
            }

            ValidateSupportMetadataConsistency(body, profile, issues);
        }

        foreach (var rule in RaceCompatibilityCatalog.BodyRules.OrderBy(static rule => rule.Body, StringComparer.OrdinalIgnoreCase))
        {
            if (BuiltInBodyMetadataCatalog.TryResolveCanonicalName(rule.Body, out _))
            {
                continue;
            }

            issues.Add($"Race rule body '{rule.Body}' does not resolve to a known built-in body.");
        }

        return issues;
    }

    private static void ValidateSupportMetadataConsistency(
        BuiltInBodyMetadata body,
        BodyTechnicalProfileInfo profile,
        List<string> issues)
    {
        if (!body.HasExplicitSupportMetadata)
        {
            issues.Add($"{body.Name}: built-in support metadata is inferred instead of explicit.");
        }

        var normalizedSemanticRegions = BodySupportMetadataHeuristics.NormalizeSupportRegionList(profile.ExpectedSemanticRegions);
        var normalizedCollisionRegions = BodySupportMetadataHeuristics.NormalizeSupportRegionList(profile.ExpectedCollisionRegions);
        var normalizedBilateralRegions = BodySupportMetadataHeuristics.NormalizeSupportRegionList(profile.ExpectedBilateralRegions);
        var sliderRegions = BodySupportMetadataHeuristics.NormalizeSupportRegionList(body.SliderNames);
        var physicsRegions = BodySupportMetadataHeuristics.NormalizeSupportRegionList(profile.RequiredPhysicsBones.Concat(profile.PhysicsBoneSignatures));

        if (normalizedSemanticRegions.Count == 0)
        {
            issues.Add($"{body.Name}: expected semantic regions are empty.");
        }
        else if (!sliderRegions.Intersect(normalizedSemanticRegions, StringComparer.OrdinalIgnoreCase).Any() &&
                 !physicsRegions.Intersect(normalizedSemanticRegions, StringComparer.OrdinalIgnoreCase).Any())
        {
            issues.Add($"{body.Name}: expected semantic regions are not backed by sliderNames or physics bones.");
        }

        if (profile.SupportsPhysics)
        {
            if (normalizedCollisionRegions.Count == 0)
            {
                issues.Add($"{body.Name}: expected collision regions are empty despite physics support.");
            }
            else if (!physicsRegions.Intersect(normalizedCollisionRegions, StringComparer.OrdinalIgnoreCase).Any())
            {
                issues.Add($"{body.Name}: expected collision regions are not backed by physics bones.");
            }
        }

        if (normalizedBilateralRegions.Count > 0 &&
            !normalizedBilateralRegions.Intersect(normalizedSemanticRegions.Concat(normalizedCollisionRegions), StringComparer.OrdinalIgnoreCase).Any())
        {
            issues.Add($"{body.Name}: expected bilateral regions are not backed by semantic or collision regions.");
        }

        var normalizedCollisionComplexity = NormalizeCollisionComplexity(profile.CollisionComplexity);
        if (normalizedCollisionComplexity is not ("none" or "minimal" or "standard" or "extended"))
        {
            issues.Add($"{body.Name}: collision complexity '{profile.CollisionComplexity}' is invalid.");
        }

        if (profile.SupportsPhysics && profile.MinimumPhysicsSlotCount <= 0)
        {
            issues.Add($"{body.Name}: minimum physics slot count must be positive when physics bones are present.");
        }
        else if (profile.MinimumPhysicsSlotCount > 0 && profile.PhysicsSlotCount > 0 && profile.MinimumPhysicsSlotCount > profile.PhysicsSlotCount)
        {
            issues.Add($"{body.Name}: minimum physics slot count {profile.MinimumPhysicsSlotCount} exceeds available slot count {profile.PhysicsSlotCount}.");
        }

        if (profile.SupportsPhysics && profile.MinimumPhysicsChainDepth <= 0)
        {
            issues.Add($"{body.Name}: minimum physics chain depth must be positive when physics bones are present.");
        }
        else if (profile.MinimumPhysicsChainDepth > 0 && profile.PhysicsChainDepth > 0 && profile.MinimumPhysicsChainDepth > profile.PhysicsChainDepth)
        {
            issues.Add($"{body.Name}: minimum physics chain depth {profile.MinimumPhysicsChainDepth} exceeds available chain depth {profile.PhysicsChainDepth}.");
        }

        if (profile.SupportsPhysics && profile.MinimumPhysicsFamilyCount <= 0)
        {
            issues.Add($"{body.Name}: minimum physics family count must be positive when physics bones are present.");
        }
        else if (profile.MinimumPhysicsFamilyCount > 0 && profile.PhysicsFamilyCount > 0 && profile.MinimumPhysicsFamilyCount > profile.PhysicsFamilyCount)
        {
            issues.Add($"{body.Name}: minimum physics family count {profile.MinimumPhysicsFamilyCount} exceeds available family count {profile.PhysicsFamilyCount}.");
        }

        if (profile.SupportsPhysics && profile.MinimumRuntimePhysicsNodeCount <= 0)
        {
            issues.Add($"{body.Name}: minimum runtime physics node count must be positive when physics bones are present.");
        }
        else if (profile.MinimumRuntimePhysicsNodeCount > 0 && profile.PhysicsNodeCount > 0 && profile.MinimumRuntimePhysicsNodeCount > profile.PhysicsNodeCount)
        {
            issues.Add($"{body.Name}: minimum runtime physics node count {profile.MinimumRuntimePhysicsNodeCount} exceeds available node count {profile.PhysicsNodeCount}.");
        }

        var supportWarnings = LocalExportService.EvaluateTargetBodySupportQuality(
            body.ReferenceTokens,
            body.SliderNames,
            body.AvailablePhysicsBones,
            body.ExpectedSemanticRegions,
            body.ExpectedCollisionRegions,
            body.ExpectedBilateralRegions,
            body.MinimumPhysicsSlotCount,
            body.MinimumPhysicsChainDepth,
            body.MinimumPhysicsFamilyCount,
            body.MinimumRuntimePhysicsNodeCount,
            body.CollisionComplexity,
            !string.IsNullOrWhiteSpace(body.SkeletonFramework) || !string.IsNullOrWhiteSpace(body.SkeletonFoundation),
            profile.RecommendedPhysicsProfile,
            body.PhysicsTokens,
            profile.RecommendedPhysicsProfile,
            body.HasExplicitSupportMetadata);
        var structuralWarnings = supportWarnings
            .Where(static warning =>
                warning.Equals("referenceTokens-quality", StringComparison.OrdinalIgnoreCase) ||
                warning.Equals("sliderNames-quality", StringComparison.OrdinalIgnoreCase) ||
                warning.Equals("sliderNames-region-coverage", StringComparison.OrdinalIgnoreCase) ||
                warning.Equals("expectedSemanticRegions-quality", StringComparison.OrdinalIgnoreCase) ||
                warning.Equals("expectedCollisionRegions-quality", StringComparison.OrdinalIgnoreCase) ||
                warning.Equals("expectedBilateralRegions-quality", StringComparison.OrdinalIgnoreCase) ||
                warning.Equals("skeletonFoundation/skeletonFramework-quality", StringComparison.OrdinalIgnoreCase) ||
                warning.Equals("runtime-config-expectations-quality", StringComparison.OrdinalIgnoreCase) ||
                warning.Equals("support-metadata-explicitness", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (structuralWarnings.Length > 0)
        {
            issues.Add($"{body.Name}: support metadata quality warnings: {string.Join(", ", structuralWarnings.Take(5))}");
        }
    }

    private static string NormalizeCollisionComplexity(string? collisionComplexity) =>
        string.IsNullOrWhiteSpace(collisionComplexity)
            ? "none"
            : collisionComplexity.Trim().ToLowerInvariant();

    private static RuntimeReadinessCheck CreateExecutableCheck(string? currentExePath)
    {
        if (string.IsNullOrWhiteSpace(currentExePath))
        {
            return new("Executable", "Warning", "Current executable path is unavailable.");
        }

        try
        {
            var fullPath = Path.GetFullPath(currentExePath);
            if (!File.Exists(fullPath))
            {
                return new("Executable", "Warning", $"Executable path does not exist: {fullPath}");
            }

            var fileInfo = new FileInfo(fullPath);
            var sizeInMegabytes = fileInfo.Length / (1024d * 1024d);
            return new("Executable", "OK", $"{fullPath} ({sizeInMegabytes:F1} MB)");
        }
        catch (Exception ex)
        {
            return new("Executable", "Warning", $"Could not inspect executable path: {ex.Message}");
        }
    }

    private static RuntimeReadinessCheck CreatePipelineCheck()
    {
        try
        {
            _ = StandaloneConversionModules.CreateDefault();
            _ = StandaloneConversionModules.CreateInspector();
            return new("Core pipeline", "OK", "Conversion modules and inspector initialized successfully.");
        }
        catch (Exception ex)
        {
            return new("Core pipeline", "Error", $"Failed to initialize conversion modules: {ex.Message}");
        }
    }

    private static RuntimeReadinessCheck CreateNifParsingCheck()
    {
        try
        {
            return new("NIF parsing", "OK", NifGeometrySignatureReader.GetCapabilitySummary());
        }
        catch (Exception ex)
        {
            return new("NIF parsing", "Warning", $"Could not verify NIF parsing capabilities: {ex.Message}");
        }
    }

    private static RuntimeReadinessCheck CreateCacheCheck()
    {
        try
        {
            var cachePath = ConversionLearningCache.GetGlobalCachePath();
            if (string.IsNullOrWhiteSpace(cachePath))
            {
                return new("Learning cache", "Warning", "Global cache path could not be determined on this machine.");
            }

            return new("Learning cache", "OK", cachePath);
        }
        catch (Exception ex)
        {
            return new("Learning cache", "Warning", $"Could not resolve global cache path: {ex.Message}");
        }
    }

    private static RuntimeReadinessCheck CreateScratchWriteCheck()
    {
        var scratchDirectory = Path.Combine(Path.GetTempPath(), "slidesmith-self-check", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(scratchDirectory);
            var probePath = Path.Combine(scratchDirectory, "probe.txt");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            Directory.Delete(scratchDirectory);
            return new("Scratch write", "OK", $"Temporary write access confirmed in {Path.GetTempPath()}");
        }
        catch (Exception ex)
        {
            return new("Scratch write", "Warning", $"Could not write to a temporary directory: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(scratchDirectory))
                {
                    Directory.Delete(scratchDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static RuntimeReadinessCheck CreateStartupCrashLogWriteCheck()
    {
        var probeDirectory = Path.Combine(Path.GetTempPath(), "slidesmith-startup-check", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(probeDirectory);
            var probePath = Path.Combine(probeDirectory, "startup-crash.log");
            File.WriteAllText(probePath, "startup-check");
            File.Delete(probePath);
            Directory.Delete(probeDirectory);
            return new("Startup crash log", "OK", "Crash-log write path is writable.");
        }
        catch (Exception ex)
        {
            return new("Startup crash log", "Warning", $"Could not verify crash-log write path: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(probeDirectory))
                {
                    Directory.Delete(probeDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static RuntimeReadinessCheck CreateUniversalCoverageCheck(ProofCoverageSnapshot proofCoverage)
    {
        if (proofCoverage.HasPackProofReport && proofCoverage.RemainingGapCount == 0)
        {
            return new(
                "Universal coverage",
                "OK",
                $"Latest proof checkpoint reports no remaining strict/universal blockers ({Path.GetFileName(proofCoverage.ReportPath)}).");
        }

        if (proofCoverage.HasPackProofReport)
        {
            return new(
                "Universal coverage",
                "Info",
                $"Progress checkpoint: strict universal release proof still has {proofCoverage.RemainingGapCount} checklist blocker(s). Use remaining-gaps checklist reports to track concrete blockers ({Path.GetFileName(proofCoverage.ReportPath)}).");
        }

        return new(
            "Universal coverage",
            "Info",
            "Progress checkpoint: SlideSmith can already convert many real-world armors, but a strict 'any armor to any body' release claim still needs broader proof on the hardest topology-correspondence cases. Use remaining-gaps checklist reports to track concrete blockers.");
    }

    private static RuntimeReadinessCheck CreateDesktopAutomationCoverageCheck(ProofCoverageSnapshot proofCoverage)
    {
        if (proofCoverage.HasPackProofReport && !proofCoverage.MissingProofAxes.Contains("desktop-e2e"))
        {
            return new(
                "Desktop automation proof",
                "OK",
                $"Latest matrix pack proof includes desktop-e2e evidence ({Path.GetFileName(proofCoverage.ReportPath)}).");
        }

        return new(
            "Desktop automation proof",
            "Info",
            "Progress checkpoint: real Windows click-path proof is still an external validation step. Complete it on a Windows host with WebView2 and an external UI automation harness.");
    }

    private static RuntimeReadinessCheck CreateLiveGameAutomationCoverageCheck(ProofCoverageSnapshot proofCoverage)
    {
        if (proofCoverage.HasPackProofReport && !proofCoverage.MissingProofAxes.Contains("runtime-automation"))
        {
            return new(
                "Live-game automation proof",
                "OK",
                $"Latest matrix pack proof includes runtime-automation evidence ({Path.GetFileName(proofCoverage.ReportPath)}).");
        }

        return new(
            "Live-game automation proof",
            "Info",
            "Progress checkpoint: in-game/runtime verification is exported as an external harness contract and should be finished against the target Windows mod stack outside this desktop app.");
    }

    private static RuntimeReadinessCheck CreateStrictMatrixCoverageCheck(ProofCoverageSnapshot proofCoverage)
    {
        if (proofCoverage.HasPackProofReport && proofCoverage.StrictProofReady)
        {
            return new(
                "Strict matrix proof",
                "OK",
                $"Latest conversion matrix pack proof is strict-ready with no missing matrix dimensions/combinations ({Path.GetFileName(proofCoverage.ReportPath)}).");
        }

        if (proofCoverage.HasPackProofReport)
        {
            return new(
                "Strict matrix proof",
                "Info",
                $"Progress checkpoint: universal-ready claims still need broader proof across body × skeleton × plugin-family × runtime combinations. Missing matrix dimensions: {proofCoverage.MissingMatrixDimensionsCount}; missing matrix combinations: {proofCoverage.MissingMatrixCombinationsCount} ({Path.GetFileName(proofCoverage.ReportPath)}).");
        }

        return new(
            "Strict matrix proof",
            "Info",
            "Progress checkpoint: universal-ready claims still need broader proof across body × skeleton × plugin-family × runtime combinations, even when one conversion looks healthy. Use conversion-matrix-proof + remaining-gaps checklist reports for exact next blockers.");
    }

    private static ProofCoverageSnapshot BuildProofCoverageSnapshot(string? proofOutputRootOverride)
    {
        var outputRoot = string.IsNullOrWhiteSpace(proofOutputRootOverride)
            ? ExecutionEnvironment.GetDefaultOutputRoot()
            : proofOutputRootOverride;
        if (string.IsNullOrWhiteSpace(outputRoot) || !Directory.Exists(outputRoot))
        {
            return new ProofCoverageSnapshot(
                ReportPath: null,
                HasPackProofReport: false,
                StrictProofReady: false,
                RemainingGapCount: 0,
                MissingMatrixDimensionsCount: 0,
                MissingMatrixCombinationsCount: 0,
                MissingProofAxes: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var reportPath = TryFindLatestReportFile(outputRoot, "conversion-matrix-pack-proof.json");
        if (string.IsNullOrWhiteSpace(reportPath) || !File.Exists(reportPath))
        {
            return new ProofCoverageSnapshot(
                ReportPath: null,
                HasPackProofReport: false,
                StrictProofReady: false,
                RemainingGapCount: CountUncheckedChecklistItems(outputRoot),
                MissingMatrixDimensionsCount: 0,
                MissingMatrixCombinationsCount: 0,
                MissingProofAxes: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
            var root = report.RootElement;
            var strictProofReady = root.TryGetProperty("StrictProofReady", out var strictValue) &&
                                   strictValue.ValueKind == JsonValueKind.True;
            var missingProofAxes = TryReadArrayValues(root, "MissingProofAxes");
            var missingMatrixDimensions = TryReadArrayValues(root, "MissingMatrixDimensions");
            var missingMatrixCombinations = TryReadArrayValues(root, "MissingMatrixCombinations");
            var remainingGapCount = CountUncheckedChecklistItems(Path.GetDirectoryName(reportPath) ?? outputRoot);
            if (remainingGapCount == 0 && !strictProofReady)
            {
                remainingGapCount = TryReadArrayValues(root, "BlockingGaps").Count;
            }

            return new ProofCoverageSnapshot(
                ReportPath: reportPath,
                HasPackProofReport: true,
                StrictProofReady: strictProofReady,
                RemainingGapCount: remainingGapCount,
                MissingMatrixDimensionsCount: missingMatrixDimensions.Count,
                MissingMatrixCombinationsCount: missingMatrixCombinations.Count,
                MissingProofAxes: missingProofAxes);
        }
        catch
        {
            return new ProofCoverageSnapshot(
                ReportPath: reportPath,
                HasPackProofReport: true,
                StrictProofReady: false,
                RemainingGapCount: CountUncheckedChecklistItems(Path.GetDirectoryName(reportPath) ?? outputRoot),
                MissingMatrixDimensionsCount: 0,
                MissingMatrixCombinationsCount: 0,
                MissingProofAxes: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static HashSet<string> TryReadArrayValues(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int CountUncheckedChecklistItems(string outputRoot)
    {
        var checklistPath = TryFindLatestReportFile(outputRoot, "remaining-gaps-pack-checklist.md")
            ?? TryFindLatestReportFile(outputRoot, "remaining-gaps-checklist.md");
        if (string.IsNullOrWhiteSpace(checklistPath) || !File.Exists(checklistPath))
        {
            return 0;
        }

        try
        {
            return File.ReadLines(checklistPath)
                .Count(line => line.TrimStart().StartsWith("- [ ]", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return 0;
        }
    }

    private static string? TryFindLatestReportFile(string rootDirectory, string fileName)
    {
        try
        {
            return Directory.EnumerateFiles(rootDirectory, fileName, SearchOption.AllDirectories)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static RuntimeReadinessCheck CreateDesktopProbeCheck(string? currentExePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new("Desktop GUI", "Info", "Desktop EXE auto-launch probe is only relevant on Windows.");
        }

        if (string.IsNullOrWhiteSpace(currentExePath))
        {
            return new("Desktop GUI", "Warning", "Could not inspect sibling desktop EXE because the CLI path is unavailable.");
        }

        try
        {
            var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(currentExePath));
            if (string.IsNullOrWhiteSpace(executableDirectory))
            {
                return new("Desktop GUI", "Warning", "Could not resolve the executable directory.");
            }

            var currentFullPath = Path.GetFullPath(currentExePath);
            foreach (var candidate in DesktopCompanionExeNames.Select(fileName => Path.Combine(executableDirectory, fileName)))
            {
                if (!File.Exists(candidate) ||
                    string.Equals(Path.GetFullPath(candidate), currentFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new("Desktop GUI", "OK", $"Sibling desktop EXE detected: {candidate}");
            }

            return new("Desktop GUI", "Warning", "No sibling desktop EXE was found beside the CLI binary.");
        }
        catch (Exception ex)
        {
            return new("Desktop GUI", "Warning", $"Could not inspect sibling desktop EXE: {ex.Message}");
        }
    }

    private static RuntimeReadinessCheck CreateCliProbeCheck(string? currentExePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new("CLI companion", "Info", "CLI companion probe is only relevant on Windows.");
        }

        if (string.IsNullOrWhiteSpace(currentExePath))
        {
            return new("CLI companion", "Info", "Optional CLI companion check skipped because the desktop path is unavailable in this context.");
        }

        try
        {
            var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(currentExePath));
            if (string.IsNullOrWhiteSpace(executableDirectory))
            {
                return new("CLI companion", "Info", "Optional CLI companion check skipped because the executable directory could not be resolved.");
            }

            var currentFullPath = Path.GetFullPath(currentExePath);
            foreach (var candidate in CliCompanionExeNames.Select(fileName => Path.Combine(executableDirectory, fileName)))
            {
                if (!File.Exists(candidate) ||
                    string.Equals(Path.GetFullPath(candidate), currentFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return new("CLI companion", "OK", $"Sibling CLI EXE detected: {candidate}");
            }

            return new("CLI companion", "Info", "No sibling CLI EXE was found beside the desktop binary. This is optional unless you plan to run CLI workflows from the same install folder.");
        }
        catch (Exception ex)
        {
            return new("CLI companion", "Info", $"Optional CLI companion check failed: {ex.Message}");
        }
    }
}
