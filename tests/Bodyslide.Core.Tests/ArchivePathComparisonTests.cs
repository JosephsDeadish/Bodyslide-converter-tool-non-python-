using System;
using System.IO;
using System.IO.Compression;
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
    }
}
