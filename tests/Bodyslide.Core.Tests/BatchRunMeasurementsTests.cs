using System.Text.Json;
using System.IO.Compression;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class BatchRunMeasurementsTests
{
    [Fact]
    public async Task CancelledRunPreservesCancellationAndReportsPartialOutput()
    {
        var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(output, "partial.nif"), new byte[12]);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new BatchConversionRunner(StandaloneConversionModules.CreateDefault())
                    .ConvertAsync(new ConversionRequest("unused.nif", "CBBE", output),
                        cancellationToken: cancelled.Token));
            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "batch-performance.json")));
            Assert.Equal("cancelled", report.RootElement.GetProperty("Status").GetString());
            Assert.Equal(12, report.RootElement.GetProperty("OutputBytesByCategory").GetProperty("meshes").GetInt64());
        }
        finally { Directory.Delete(output, true); }
    }

    [Fact]
    public async Task ArchiveBatchWritesWholeRunMeasurementsAfterPackagingAndCleanup()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "meshes"));
            await File.WriteAllTextAsync(Path.Combine(source, "meshes", "armor_0.nif"), "unsupported fixture mesh");
            var archive = Path.Combine(root, "source.zip");
            ZipFile.CreateFromDirectory(source, archive);
            var output = Path.Combine(root, "output");
            var results = await new BatchConversionRunner(StandaloneConversionModules.CreateDefault())
                .ConvertAsync(new ConversionRequest(archive, "CBBE", output, OutputZip: true));
            Assert.Single(results);
            Assert.True(File.Exists(output + ".zip"));
            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "batch-performance.json")));
            var phases = report.RootElement.GetProperty("PhaseMilliseconds");
            Assert.Equal(5, phases.EnumerateObject().Count());
            Assert.True(report.RootElement.GetProperty("TotalMilliseconds").GetInt64() >=
                phases.EnumerateObject().Sum(property => property.Value.GetInt64()));
            Assert.True(report.RootElement.GetProperty("OutputBytesByCategory").GetProperty("archives").GetInt64() > 0);
            using var zip = ZipFile.OpenRead(output + ".zip");
            Assert.DoesNotContain(zip.Entries, entry => entry.FullName == "batch-performance.json");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InventoryDeduplicatesNestedRootsAndIncludesSiblingArchives()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var child = Path.Combine(root, "armor");
        Directory.CreateDirectory(child);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(child, "mesh.nif"), new byte[10]);
            await File.WriteAllBytesAsync(Path.Combine(child, "texture.dds"), new byte[20]);
            await File.WriteAllBytesAsync(Path.Combine(child, "morph.osd"), new byte[30]);
            await File.WriteAllBytesAsync(Path.Combine(child, "plugin.esp"), new byte[40]);
            await File.WriteAllBytesAsync(child + ".zip", new byte[50]);
            await File.WriteAllBytesAsync(root + ".zip", new byte[60]);
            using var measurements = new BatchRunMeasurements();
            using (measurements.Measure("discovery")) await Task.Delay(15);
            await measurements.WriteAsync(root, [root + Path.DirectorySeparatorChar, child], "completed");
            using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "batch-performance.json")));
            var bytes = report.RootElement.GetProperty("OutputBytesByCategory");
            Assert.Equal(10, bytes.GetProperty("meshes").GetInt64());
            Assert.Equal(20, bytes.GetProperty("textures").GetInt64());
            Assert.Equal(30, bytes.GetProperty("morphs").GetInt64());
            Assert.Equal(40, bytes.GetProperty("plugins").GetInt64());
            Assert.Equal(110, bytes.GetProperty("archives").GetInt64());
            Assert.True(report.RootElement.GetProperty("OutputInventoryComplete").GetBoolean());
            Assert.True(report.RootElement.GetProperty("PhaseMilliseconds").GetProperty("discovery").GetInt64() > 0);
            Assert.True(report.RootElement.GetProperty("SampledPeakWorkingSetBytes").GetInt64() > 0);
        }
        finally
        {
            Directory.Delete(root, true);
            File.Delete(root + ".zip");
        }
    }
}
