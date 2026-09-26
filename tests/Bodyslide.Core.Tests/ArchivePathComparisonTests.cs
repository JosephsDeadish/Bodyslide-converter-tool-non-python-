using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Bodyslide.Core.Tests
{
    public class ArchivePathComparisonTests
    {
        [Fact]
        public void PathComparison_IsCaseInsensitiveOnWindows()
        {
            var root = @"C:\Temp\Extract\";
            var path = @"c:\temp\extract\file.txt";

            if (OperatingSystem.IsWindows())
            {
                Assert.StartsWith(root, path, StringComparison.OrdinalIgnoreCase);
                Assert.False(path.StartsWith(root, StringComparison.Ordinal));
            }
            else
            {
                Assert.False(path.StartsWith(root, StringComparison.Ordinal));
            }
        }

        [Fact]
        public void ExtractToTemporaryWorkspace_RejectsZipArchiveTraversalEntries()
        {
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            try
            {
                var zipPath = Path.Combine(workingDirectory, "traversal.zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("..\\evil.txt");
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream);
                    writer.Write("malicious");
                }

                var exception = Assert.Throws<InvalidDataException>(() => ArchiveExtractionHelper.ExtractToTemporaryWorkspace(zipPath, "test-traversal"));
                Assert.Contains("Archive entry escapes extraction root", exception.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public void ExtractToTemporaryWorkspace_RejectsTarArchiveTraversalEntries()
        {
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            try
            {
                var tarPath = Path.Combine(workingDirectory, "traversal.tar");
                CreateTarWithTraversalEntry(tarPath);

                var exception = Assert.Throws<InvalidDataException>(() => ArchiveExtractionHelper.ExtractToTemporaryWorkspace(tarPath, "test-tar-traversal"));
                Assert.Contains("Archive entry escapes extraction root", exception.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public void ExtractToTemporaryWorkspace_RejectsTarGzArchiveTraversalEntries()
        {
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            try
            {
                var tarPath = Path.Combine(workingDirectory, "traversal.tar");
                var tarGzPath = Path.Combine(workingDirectory, "traversal.tar.gz");
                CreateTarWithTraversalEntry(tarPath);
                using (var tarStream = File.OpenRead(tarPath))
                using (var gzipFileStream = File.Create(tarGzPath))
                using (var gzipStream = new GZipStream(gzipFileStream, CompressionLevel.Optimal))
                {
                    tarStream.CopyTo(gzipStream);
                }

                var exception = Assert.Throws<InvalidDataException>(() => ArchiveExtractionHelper.ExtractToTemporaryWorkspace(tarGzPath, "test-targz-traversal"));
                Assert.Contains("Archive entry escapes extraction root", exception.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public void ExtractToTemporaryWorkspace_HonorsCancellation()
        {
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            try
            {
                var zipPath = Path.Combine(workingDirectory, "cancel.zip");
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entry = archive.CreateEntry("mesh.nif");
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream);
                    writer.Write("dummy");
                }

                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                Assert.Throws<OperationCanceledException>(() =>
                    ArchiveExtractionHelper.ExtractToTemporaryWorkspace(zipPath, "test-cancel", cancellation.Token));
            }
            finally
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public void EnumerateFiles_HonorsCancellation()
        {
            var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            try
            {
                File.WriteAllText(Path.Combine(workingDirectory, "armor_0.nif"), "dummy");

                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                Assert.Throws<OperationCanceledException>(() =>
                    BatchConversionRunner.SourceScanEnumerator.EnumerateFiles(
                        workingDirectory,
                        [".nif"],
                        cancellationToken: cancellation.Token));
            }
            finally
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, recursive: true);
                }
            }
        }

        private static void CreateTarWithTraversalEntry(string tarPath)
        {
            using var tarStream = File.Create(tarPath);
            using var tarWriter = new TarWriter(tarStream, leaveOpen: false);
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "../evil.txt")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes("malicious")),
            };

            tarWriter.WriteEntry(entry);
        }
    }
}
