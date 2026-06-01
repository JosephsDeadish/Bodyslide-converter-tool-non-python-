using System.Runtime.InteropServices;

namespace Bodyslide.Core;

public sealed record RuntimeReadinessCheck(string Area, string Status, string Details);

public static class RuntimeReadinessReporter
{
    private static readonly string[] SupportedInputs =
    [
        ".nif mesh",
        ".esp/.esm/.esl plugin",
        "armor folder",
        ".zip/.7z/.tar/.tar.gz/.tgz archive",
    ];

    public static IReadOnlyList<RuntimeReadinessCheck> CreateCliReport(string? currentExePath) =>
        CreateReport(currentExePath, includeDesktopProbe: true);

    public static IReadOnlyList<RuntimeReadinessCheck> CreateDesktopReport(string? currentExePath) =>
        CreateReport(currentExePath, includeDesktopProbe: false);

    private static IReadOnlyList<RuntimeReadinessCheck> CreateReport(string? currentExePath, bool includeDesktopProbe)
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

        if (includeDesktopProbe)
        {
            checks.Add(CreateDesktopProbeCheck(currentExePath));
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
            foreach (var candidate in new[]
                     {
                         Path.Combine(executableDirectory, "SlideSmith.exe"),
                         Path.Combine(executableDirectory, "SlideSmith-Desktop.exe"),
                     })
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
}
