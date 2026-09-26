using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bodyslide.Core;

public sealed record DesktopUiSettings(
    [property: JsonPropertyName("theme")] string? Theme,
    [property: JsonPropertyName("customProfilePaths")] IReadOnlyList<string>? CustomProfilePaths);

public static class DesktopUiSettingsStore
{
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
        var lockPath = settingsLock.Name;
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

            if (!string.IsNullOrWhiteSpace(lockPath))
            {
                try
                {
                    File.Delete(lockPath);
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
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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
}
