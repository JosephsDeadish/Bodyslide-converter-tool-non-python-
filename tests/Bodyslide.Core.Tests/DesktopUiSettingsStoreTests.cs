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

    [Fact]
    public void GetDefaultSettingsPath_UsesSlideSmithLocalApplicationDataLocation()
    {
        var path = DesktopUiSettingsStore.GetDefaultSettingsPath();
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.Equal("ui-settings.json", Path.GetFileName(path));
        Assert.Equal("SlideSmith", new DirectoryInfo(Path.GetDirectoryName(path)!).Name);
        Assert.StartsWith(expectedRoot, path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Save_AllowsConcurrentWritersAndLeavesValidJson()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var settingsPath = Path.Combine(workingDirectory, "ui-settings.json");
            var firstProfile = Path.Combine(workingDirectory, "first-profile.json");
            var secondProfile = Path.Combine(workingDirectory, "second-profile.json");
            File.WriteAllText(firstProfile, "{ }");
            File.WriteAllText(secondProfile, "{ }");

            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var saveTasks = new[]
            {
                Task.Run(async () =>
                {
                    await startGate.Task;
                    for (var attempt = 0; attempt < 10; attempt++)
                    {
                        DesktopUiSettingsStore.Save(settingsPath, new DesktopUiSettings("Dark", [firstProfile]));
                    }
                }),
                Task.Run(async () =>
                {
                    await startGate.Task;
                    for (var attempt = 0; attempt < 10; attempt++)
                    {
                        DesktopUiSettingsStore.Save(settingsPath, new DesktopUiSettings("Light", [secondProfile]));
                    }
                })
            };

            startGate.SetResult();
            await Task.WhenAll(saveTasks);

            var loaded = DesktopUiSettingsStore.Load(settingsPath);
            Assert.True(
                loaded.Theme is "Dark" or "Light",
                $"Expected theme to be either Dark or Light but was '{loaded.Theme ?? "<null>"}'.");

            var customProfilePaths = Assert.IsAssignableFrom<IReadOnlyList<string>>(loaded.CustomProfilePaths);
            Assert.Single(customProfilePaths);
            Assert.True(
                customProfilePaths[0] is var selectedProfile &&
                (selectedProfile == firstProfile || selectedProfile == secondProfile),
                $"Expected saved profile path to be either '{firstProfile}' or '{secondProfile}' but was '{customProfilePaths[0]}'.");

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = document.RootElement;
            Assert.True(
                root.GetProperty("theme").GetString() is "Dark" or "Light",
                "Expected persisted theme to be either Dark or Light.");
            Assert.Single(root.GetProperty("customProfilePaths").EnumerateArray());
            Assert.False(File.Exists($"{settingsPath}.lock"));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void Save_RecoversFromStaleLockFile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var settingsPath = Path.Combine(workingDirectory, "ui-settings.json");
            var existingProfile = Path.Combine(workingDirectory, "custom-profile.json");
            File.WriteAllText(existingProfile, "{ }");
            File.WriteAllText($"{settingsPath}.lock", string.Empty);

            DesktopUiSettingsStore.Save(settingsPath, new DesktopUiSettings("Dark", [existingProfile]));

            Assert.True(File.Exists(settingsPath));
            Assert.False(File.Exists($"{settingsPath}.lock"));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}
