using System.Diagnostics;
using System.IO.Compression;

namespace Bodyslide.Core.Tests;

public sealed class ArchiveExtractionStressTests
{
    [Fact]
    public void ExtractToTemporaryWorkspace_LargeZip_ReportsThroughputAndByteMetrics()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "slidesmith-archive-stress-throughput", Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(tempRoot, "input.zip");
        Directory.CreateDirectory(tempRoot);
        CreateZipWithPayload(archivePath, "meshes/armor_large_0.nif", sizeBytes: 96L * 1024 * 1024);

        string? extractionDirectory = null;
        try
        {
            ArchiveExtractionHelper.ArchiveExtractionProgress? lastProgress = null;
            var maxThroughput = 0d;
            var progressEvents = 0;

            extractionDirectory = ArchiveExtractionHelper.ExtractToTemporaryWorkspace(
                archivePath,
                "slidesmith-archive-stress-throughput-run",
                cancellationToken: CancellationToken.None,
                onProgress: progress =>
                {
                    progressEvents++;
                    lastProgress = progress;
                    if (progress.ThroughputMiBPerSecond is double throughput && throughput > 0d)
                    {
                        maxThroughput = Math.Max(maxThroughput, throughput);
                    }
                });

            Assert.True(progressEvents > 0, "Expected extraction to emit progress events.");
            Assert.NotNull(lastProgress);
            Assert.True(lastProgress!.TotalBytesCopied >= 96L * 1024 * 1024, "Expected copied-byte metrics to include full extracted payload.");
            Assert.True(lastProgress.TotalBytesEstimated is >= 96L * 1024 * 1024, "Expected estimated-byte metrics for zip payload.");
            Assert.True(lastProgress.ElapsedMilliseconds > 0, "Expected elapsed-milliseconds metric to be populated.");
            Assert.True(maxThroughput > 0, "Expected throughput metric to be greater than zero.");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(extractionDirectory) && Directory.Exists(extractionDirectory))
            {
                Directory.Delete(extractionDirectory, recursive: true);
            }

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ExtractToTemporaryWorkspace_LargeZip_CancelLatencyStaysBounded()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "slidesmith-archive-stress-cancel", Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(tempRoot, "input.zip");
        var extractionPrefix = $"slidesmith-archive-stress-cancel-run-{Guid.NewGuid():N}";
        Directory.CreateDirectory(tempRoot);
        CreateZipWithPayload(archivePath, "meshes/armor_cancel_0.nif", sizeBytes: 192L * 1024 * 1024);

        var cts = new CancellationTokenSource();
        long? cancelRequestedAtMs = null;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var exception = Record.Exception(() =>
                ArchiveExtractionHelper.ExtractToTemporaryWorkspace(
                    archivePath,
                    extractionPrefix,
                    cancellationToken: cts.Token,
                    onProgress: progress =>
                    {
                        if (cancelRequestedAtMs is not null)
                        {
                            return;
                        }

                        if (progress.CurrentEntry.StartsWith("copying:", StringComparison.OrdinalIgnoreCase))
                        {
                            cancelRequestedAtMs = stopwatch.ElapsedMilliseconds;
                            cts.Cancel();
                        }
                    }));

            Assert.IsType<OperationCanceledException>(exception);
            Assert.True(cancelRequestedAtMs.HasValue, "Expected cancellation to be requested during active extraction progress.");
            var cancelObservedLatencyMs = stopwatch.ElapsedMilliseconds - cancelRequestedAtMs.Value;
            Assert.True(cancelObservedLatencyMs <= 2000, $"Expected cancel latency <= 2000ms, observed {cancelObservedLatencyMs}ms.");
        }
        finally
        {
            cts.Dispose();
            var extractionRoot = Path.Combine(Path.GetTempPath(), extractionPrefix);
            if (Directory.Exists(extractionRoot))
            {
                Directory.Delete(extractionRoot, recursive: true);
            }

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void CreateZipWithPayload(string archivePath, string entryName, long sizeBytes)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var stream = entry.Open();
        var buffer = new byte[1024 * 1024];
        var remaining = sizeBytes;
        while (remaining > 0)
        {
            var bytesToWrite = (int)Math.Min(buffer.Length, remaining);
            stream.Write(buffer, 0, bytesToWrite);
            remaining -= bytesToWrite;
        }
    }
}
