using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using SharpCompress.Common;
using SharpCompress.Writers.SevenZip;

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

        [Fact]
        public void ExtractToTemporaryWorkspace_SevenZip_ReportsTotalEntriesAndBytes()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "slidesmith-archive-stress-7z-progress", Guid.NewGuid().ToString("N"));
            var archivePath = Path.Combine(tempRoot, "input.7z");
            Directory.CreateDirectory(tempRoot);
            CreateSevenZipWithMultiplePayloads(archivePath);

            string? extractionDirectory = null;
            try
            {
                ArchiveExtractionHelper.ArchiveExtractionProgress? lastProgress = null;
                extractionDirectory = ArchiveExtractionHelper.ExtractToTemporaryWorkspace(
                    archivePath,
                    "slidesmith-archive-stress-7z-progress-run",
                    cancellationToken: CancellationToken.None,
                    onProgress: progress => lastProgress = progress);

                Assert.NotNull(lastProgress);
                Assert.Equal(2, lastProgress!.TotalEntries);
                Assert.True(lastProgress.TotalBytesEstimated is >= (2 * 8L));
                Assert.True(lastProgress.TotalBytesCopied >= 2 * 8L);
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
    }

    [Theory]
    [InlineData("zip")]
    [InlineData("7z")]
    [InlineData("tar")]
    [InlineData("tar.gz")]
    public void ExtractToTemporaryWorkspace_LargeArchives_CancelLatencyStaysBounded(string archiveFormat)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "slidesmith-archive-stress-cancel", Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(tempRoot, archiveFormat switch
        {
            "zip" => "input.zip",
            "7z" => "input.7z",
            "tar" => "input.tar",
            "tar.gz" => "input.tar.gz",
            _ => throw new NotSupportedException($"Unsupported archive format '{archiveFormat}'.")
        });
        var extractionPrefix = $"slidesmith-archive-stress-cancel-run-{Guid.NewGuid():N}";
        Directory.CreateDirectory(tempRoot);
        CreateArchiveWithPayload(archivePath, archiveFormat, "meshes/armor_cancel_0.nif", sizeBytes: 96L * 1024 * 1024);

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
            Assert.True(cancelObservedLatencyMs <= 2000, $"Expected cancel latency <= 2000ms for {archiveFormat}, observed {cancelObservedLatencyMs}ms.");
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

    private static void CreateArchiveWithPayload(string archivePath, string archiveFormat, string entryName, long sizeBytes)
    {
        switch (archiveFormat)
        {
            case "zip":
                CreateZipWithPayload(archivePath, entryName, sizeBytes);
                break;
            case "7z":
                CreateSevenZipWithPayload(archivePath, entryName, sizeBytes);
                break;
            case "tar":
                CreateTarWithPayload(archivePath, entryName, sizeBytes);
                break;
            case "tar.gz":
                CreateTarGzWithPayload(archivePath, entryName, sizeBytes);
                break;
            default:
                throw new NotSupportedException($"Unsupported archive format '{archiveFormat}'.");
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

    private static void CreateSevenZipWithPayload(string archivePath, string entryName, long sizeBytes)
    {
        var payloadPath = Path.Combine(Path.GetDirectoryName(archivePath)!, $"{Guid.NewGuid():N}.payload.bin");
        CreatePayloadFile(payloadPath, sizeBytes);
        try
        {
            using var archiveStream = File.Create(archivePath);
            using var writer = SevenZipWriter.OpenWriter(archiveStream, CompressionType.LZMA2);
            using var payloadStream = File.OpenRead(payloadPath);
            writer.Write(entryName, payloadStream, DateTime.UtcNow);
        }
        finally
        {
            if (File.Exists(payloadPath))
            {
                File.Delete(payloadPath);
            }
        }

        private static void CreateSevenZipWithMultiplePayloads(string archivePath)
        {
            var payloadRoot = Path.Combine(Path.GetDirectoryName(archivePath)!, $"{Guid.NewGuid():N}");
            Directory.CreateDirectory(payloadRoot);
            var payloadFiles = new[]
            {
                Path.Combine(payloadRoot, "a.bin"),
                Path.Combine(payloadRoot, "b.bin")
            };

            try
            {
                File.WriteAllBytes(payloadFiles[0], new byte[8]);
                File.WriteAllBytes(payloadFiles[1], new byte[12]);
                using var archiveStream = File.Create(archivePath);
                using var writer = SevenZipWriter.OpenWriter(archiveStream, CompressionType.LZMA2);
                foreach (var payloadFile in payloadFiles)
                {
                    using var payloadStream = File.OpenRead(payloadFile);
                    writer.Write(Path.GetFileName(payloadFile), payloadStream, DateTime.UtcNow);
                }
            }
            finally
            {
                if (Directory.Exists(payloadRoot))
                {
                    Directory.Delete(payloadRoot, recursive: true);
                }
            }
        }
    }

    private static void CreateTarWithPayload(string archivePath, string entryName, long sizeBytes)
    {
        var payloadPath = Path.Combine(Path.GetDirectoryName(archivePath)!, $"{Guid.NewGuid():N}.payload.bin");
        CreatePayloadFile(payloadPath, sizeBytes);
        try
        {
            using var tarStream = File.Create(archivePath);
            using var tarWriter = new TarWriter(tarStream, leaveOpen: false);
            using var payloadStream = File.OpenRead(payloadPath);
            var tarEntry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
            {
                DataStream = payloadStream
            };
            tarWriter.WriteEntry(tarEntry);
        }
        finally
        {
            if (File.Exists(payloadPath))
            {
                File.Delete(payloadPath);
            }
        }
    }

    private static void CreateTarGzWithPayload(string archivePath, string entryName, long sizeBytes)
    {
        var tarPath = Path.Combine(Path.GetDirectoryName(archivePath)!, $"{Guid.NewGuid():N}.tar");
        try
        {
            CreateTarWithPayload(tarPath, entryName, sizeBytes);
            using var tarStream = File.OpenRead(tarPath);
            using var outputStream = File.Create(archivePath);
            using var gzipStream = new GZipStream(outputStream, CompressionLevel.Fastest, leaveOpen: false);
            tarStream.CopyTo(gzipStream);
        }
        finally
        {
            if (File.Exists(tarPath))
            {
                File.Delete(tarPath);
            }
        }
    }

    private static void CreatePayloadFile(string payloadPath, long sizeBytes)
    {
        using var stream = File.Create(payloadPath);
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
