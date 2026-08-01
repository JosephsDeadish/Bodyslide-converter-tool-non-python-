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
                Assert.StartsWith(root, path, StringComparison.Ordinal);
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
    }
}
