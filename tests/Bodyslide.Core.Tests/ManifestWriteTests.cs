using System.Reflection;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class ManifestWriteTests
{
    [Fact]
    public async Task WriteConversionManifestAsync_RetriesAndWritesPreferredPathAfterTransientLock()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        var preferredPath = Path.Combine(outputDirectory, "conversion-manifest.json");
        await File.WriteAllTextAsync(preferredPath, "{}");

        try
        {
            await using var lockHandle = new FileStream(preferredPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var writeTask = InvokeWriteConversionManifestAsync(outputDirectory, "{\"ok\":true}");
            await Task.Delay(250);
            await lockHandle.DisposeAsync();

            var resultPath = await writeTask;
            Assert.Equal(preferredPath, resultPath);
            var written = await File.ReadAllTextAsync(preferredPath);
            Assert.Contains("\"ok\":true", written, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteConversionManifestAsync_UsesFallbackPathWhenPreferredPathStaysLocked()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        var preferredPath = Path.Combine(outputDirectory, "conversion-manifest.json");
        await File.WriteAllTextAsync(preferredPath, "{}");

        try
        {
            await using var lockHandle = new FileStream(preferredPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var resultPath = await InvokeWriteConversionManifestAsync(outputDirectory, "{\"fallback\":true}");

            Assert.NotEqual(preferredPath, resultPath);
            Assert.StartsWith(
                Path.Combine(outputDirectory, "conversion-manifest-"),
                resultPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(resultPath));
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static Task<string> InvokeWriteConversionManifestAsync(string outputDirectory, string manifestJson)
    {
        var method = typeof(LocalExportService).GetMethod(
            "WriteConversionManifestAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var task = (Task<string>)method!.Invoke(null, [outputDirectory, manifestJson, CancellationToken.None])!;
        return task;
    }
}
