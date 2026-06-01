using System.Runtime.InteropServices;

namespace Bodyslide.Core;

public sealed record RuntimeReadinessCheck(string Area, string Status, string Details);

public static class RuntimeReadinessReporter
{
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

    private static IReadOnlyList<RuntimeReadinessCheck> CreateReport(
        string? currentExePath,
        bool includeDesktopProbe,
        bool includeCliProbe)
    {
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

        checks.Add(CreateExecutableCheck(currentExePath));
        checks.Add(CreatePipelineCheck());
        checks.Add(CreateCacheCheck());
        checks.Add(CreateScratchWriteCheck());
        checks.Add(CreateStartupCrashLogWriteCheck());

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
            return new("CLI companion", "Warning", "Could not inspect sibling CLI EXE because the desktop path is unavailable.");
        }

        try
        {
            var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(currentExePath));
            if (string.IsNullOrWhiteSpace(executableDirectory))
            {
                return new("CLI companion", "Warning", "Could not resolve the executable directory.");
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

            return new("CLI companion", "Warning", "No sibling CLI EXE was found beside the desktop binary.");
        }
        catch (Exception ex)
        {
            return new("CLI companion", "Warning", $"Could not inspect sibling CLI EXE: {ex.Message}");
        }
    }
}
