using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class OutputSizeInventoryTests
{
    [Fact]
    public void InventoryCountsPhysicalBytesAndSeparatesSiblingArchive()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            foreach (var (file, bytes) in new[] { ("mesh.nif", 10), ("texture.dds", 20),
                         ("morph.osd", 30), ("quality.json", 40), ("output-size-inventory.json", 99) })
                File.WriteAllBytes(Path.Combine(root, file), new byte[bytes]);
            File.WriteAllBytes(root + ".zip", new byte[50]);
            var report = OutputSizeInventory.Create(root, root + ".zip", CancellationToken.None);
            Assert.True(report.Complete);
            Assert.Equal(5, report.FileCount);
            Assert.Equal(150, report.TotalBytes);
            Assert.Equal(30, Assert.Single(report.Categories, category => category.Category == "morphs").Bytes);
            Assert.Equal(50, Assert.Single(report.Categories, category => category.Category == "archives").Bytes);
        }
        finally { Directory.Delete(root, true); File.Delete(root + ".zip"); }
    }

    [Fact]
    public void InventoryIsBoundedAndReportsIncompleteRatherThanFullCoverage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            for (var i = 0; i < 3; i++) File.WriteAllText(Path.Combine(root, $"{i}.nif"), "mesh");
            var report = OutputSizeInventory.Create(root, null, CancellationToken.None, maximumEntries: 1);
            Assert.False(report.Complete);
            Assert.Single(report.Warnings);
            Assert.Equal(1, report.FileCount);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                OutputSizeInventory.Create(root, null, cancellation.Token));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void InventoryReportsUnreadableRootAsIncomplete()
    {
        var report = OutputSizeInventory.Create(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), null, CancellationToken.None);
        Assert.False(report.Complete);
        Assert.NotEmpty(report.Warnings);
        Assert.Equal(0, report.TotalBytes);
    }
}
