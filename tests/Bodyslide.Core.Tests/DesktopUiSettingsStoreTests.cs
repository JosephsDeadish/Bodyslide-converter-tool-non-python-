using System.Text.Json;

namespace Bodyslide.Core.Tests;

public sealed class DesktopUiSettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsThemeAndExistingCustomProfiles()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var existingProfile = Path.Combine(workingDirectory, "custom-profile.json");
            var settingsPath = Path.Combine(workingDirectory, "ui-settings.json");
            File.WriteAllText(existingProfile, "{ }");

            DesktopUiSettingsStore.Save(
                settingsPath,
                new DesktopUiSettings(
                    " Dark ",
                    [existingProfile, existingProfile, "  ", Path.Combine(workingDirectory, "missing-profile.json")]));

            var loaded = DesktopUiSettingsStore.Load(settingsPath);

            Assert.Equal("Dark", loaded.Theme);
            var customProfilePaths = Assert.IsAssignableFrom<IReadOnlyList<string>>(loaded.CustomProfilePaths);
            Assert.Single(customProfilePaths);
            Assert.Equal(existingProfile, customProfilePaths[0]);

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            Assert.Equal("Dark", root.GetProperty("theme").GetString());
            Assert.Single(root.GetProperty("customProfilePaths").EnumerateArray());
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_FiltersMissingCustomProfilePathsFromExistingSettingsFile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var existingProfile = Path.Combine(workingDirectory, "existing-profile.json");
            var missingProfile = Path.Combine(workingDirectory, "missing-profile.json");
            var settingsPath = Path.Combine(workingDirectory, "ui-settings.json");
            File.WriteAllText(existingProfile, "{ }");
            File.WriteAllText(
                settingsPath,
                $$"""
                {
                  "theme": "Light",
                  "customProfilePaths": [
                    "{{existingProfile.Replace("\\", "\\\\")}}",
                    "{{missingProfile.Replace("\\", "\\\\")}}",
                    "{{existingProfile.Replace("\\", "\\\\")}}"
                  ]
                }
                """);

            var loaded = DesktopUiSettingsStore.Load(settingsPath);

            Assert.Equal("Light", loaded.Theme);
            var customProfilePaths = Assert.IsAssignableFrom<IReadOnlyList<string>>(loaded.CustomProfilePaths);
            Assert.Single(customProfilePaths);
            Assert.Equal(existingProfile, customProfilePaths[0]);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}
