using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace Bodyslide.Core;

public sealed record DesktopUiSettings(
    [property: JsonPropertyName("theme")] string? Theme,
    [property: JsonPropertyName("customProfilePaths")] IReadOnlyList<string>? CustomProfilePaths);

public static class DesktopUiSettingsStore
{
    private sealed record SettingsLockMetadata(
        [property: JsonPropertyName("processId")] int ProcessId,
        [property: JsonPropertyName("processStartTimeUtcTicks")] long ProcessStartTimeUtcTicks,
        [property: JsonPropertyName("machineName")] string MachineName,
        [property: JsonPropertyName("createdUtcTicks")] long CreatedUtcTicks);

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };


    public static string GetDefaultSettingsPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SlideSmith",
            "ui-settings.json");

    public static DesktopUiSettings Load(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);

        if (!File.Exists(settingsPath))
        {
            return new DesktopUiSettings(null, []);
        }

        var json = File.ReadAllText(settingsPath);
        var settings = JsonSerializer.Deserialize<DesktopUiSettings>(json, ReadOptions);
        return Normalize(settings, requireExistingCustomProfiles: true);
    }

    public static void Save(string settingsPath, DesktopUiSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = Normalize(settings, requireExistingCustomProfiles: true);
        var settingsDirectory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(settingsDirectory))
        {
            Directory.CreateDirectory(settingsDirectory);
        }

        string? tempPath = null;
        var settingsLock = AcquireExclusiveSettingsLock(settingsPath);
        try
        {
            using (settingsLock)
            {
                tempPath = $"{settingsPath}.{Guid.NewGuid():N}.tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(normalized, WriteOptions));
                File.Move(tempPath, settingsPath, overwrite: true);
                tempPath = null;
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }

        }
    }

    public static IReadOnlyList<string> NormalizeCustomProfilePaths(
        IEnumerable<string>? paths,
        bool requireExistingFiles = false)
    {
        var normalized = new List<string>();
        if (paths is null)
        {
            return normalized;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var trimmed = path.Trim();
            if (requireExistingFiles && !File.Exists(trimmed))
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized;
    }

    private static DesktopUiSettings Normalize(
        DesktopUiSettings? settings,
        bool requireExistingCustomProfiles)
    {
        return new DesktopUiSettings(
            string.IsNullOrWhiteSpace(settings?.Theme)
                ? null
                : settings.Theme.Trim(),
            NormalizeCustomProfilePaths(settings?.CustomProfilePaths, requireExistingCustomProfiles));
    }

    private static FileStream AcquireExclusiveSettingsLock(string settingsPath)
    {
        var lockPath = $"{settingsPath}.lock";
        const int maxAttempts = 20;
        const int retryDelayMilliseconds = 50;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                WriteLockMetadata(lockStream);
                return lockStream;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(retryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(retryDelayMilliseconds);
            }
        }
    }

    private static void WriteLockMetadata(FileStream lockStream)
    {
        try
        {
            var metadata = new SettingsLockMetadata(
                Environment.ProcessId,
                GetCurrentProcessStartTimeUtcTicks(),
                Environment.MachineName,
                DateTime.UtcNow.Ticks);
            var json = JsonSerializer.Serialize(metadata);
            lockStream.SetLength(0);
            lockStream.Position = 0;
            using var writer = new StreamWriter(lockStream, System.Text.Encoding.UTF8, bufferSize: 1024, leaveOpen: true);
            writer.Write(json);
            writer.Flush();
            lockStream.Flush(flushToDisk: true);
            lockStream.Position = 0;
        }
        catch
        {
        }
    }

    private static long GetCurrentProcessStartTimeUtcTicks()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.StartTime.ToUniversalTime().Ticks;
        }
        catch
        {
            return 0;
        }
    }
}
