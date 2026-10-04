using System.IO.Compression;
using System.Formats.Tar;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class ArchiveDuplicateEntryTests
{
    [Theory]
    [InlineData("meshes/armor.nif")]
    [InlineData("meshes\\armor.nif")]
    [InlineData("meshes/sub/../armor.nif")]
    [InlineData("Meshes/ARMOR.nif")]
    public void DuplicateGamePathsCannotSilentlyOverwriteExtractedAssets(string duplicate)
    {
        var archivePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".zip");
        var prefix = "slidesmith-duplicate-test-" + Guid.NewGuid().ToString("N");
        string? extracted = null;
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach (var path in new[] { "meshes/armor.nif", duplicate })
                {
                    using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                    writer.Write(path);
                }
            }
            var error = Assert.Throws<InvalidDataException>(() =>
                extracted = ArchiveExtractionHelper.ExtractToTemporaryWorkspace(archivePath, prefix));
            Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetDirectories(Path.Combine(Path.GetTempPath(), prefix)));
        }
        finally
        {
            if (extracted is not null && Directory.Exists(extracted)) Directory.Delete(extracted, true);
            var temporaryRoot = Path.Combine(Path.GetTempPath(), prefix);
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
            File.Delete(archivePath);
        }

    }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TarDuplicateEntriesAreRejectedAndPartialWorkspaceIsRemoved(bool gzip)
        {
            var archivePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + (gzip ? ".tgz" : ".tar"));
            var prefix = "slidesmith-tar-duplicate-test-" + Guid.NewGuid().ToString("N");
            string? extracted = null;
            try
            {
                using (var file = File.Create(archivePath))
                using (Stream compressed = gzip ? new GZipStream(file, CompressionLevel.Fastest) : file)
                using (var writer = new TarWriter(compressed))
                {
                    foreach (var name in new[] { "meshes/armor.nif", "meshes/ARMOR.nif" })
                    {
                        using var data = new MemoryStream(new byte[] { 1, 2, 3 });
                        writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = data });
                    }
                }
                Assert.Throws<InvalidDataException>(() =>
                    extracted = ArchiveExtractionHelper.ExtractToTemporaryWorkspace(archivePath, prefix));
                Assert.Empty(Directory.GetDirectories(Path.Combine(Path.GetTempPath(), prefix)));
            }
            finally
            {
                if (extracted is not null && Directory.Exists(extracted)) Directory.Delete(extracted, true);
                var temporaryRoot = Path.Combine(Path.GetTempPath(), prefix);
                if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
                File.Delete(archivePath);
            }
    }
}
