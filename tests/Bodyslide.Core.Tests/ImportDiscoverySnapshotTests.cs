using System.Diagnostics;
using Xunit.Abstractions;

namespace Bodyslide.Core.Tests;

public sealed class ImportDiscoverySnapshotTests(ITestOutputHelper output) : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slidesmith-import-snapshot", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(100)]
    [InlineData(5000)]
    public async Task LargeSyntheticPackSnapshotMatchesIndependentScans(int meshCount)
    {
        for (var index = 0; index < meshCount; index++)
        {
            Write($"meshes/armor/set-{index % 20:D2}/armor-{index:D5}.nif", "synthetic-discovery-only");
            Write($"textures/armor/set-{index % 20:D2}/armor-{index:D5}.dds", "synthetic-discovery-only");
        }
        Write("CalienteTools/BodySlide/ShapeData/Project/reference.nif", "synthetic-reference");
        Write("CalienteTools/BodySlide/SliderSets/Project.osp", "<SliderSetInfo/>");
        Write("CalienteTools/BodySlide/SliderGroups/Project.xml", "<SliderGroups/>");
        Write("excluded/ignored.nif", "excluded");
        Write("generated/conversion-manifest.json", "{}");
        Write("generated/ignored.nif", "generated");
        Write("meshes/slidesmith/ignored.nif", "generated");
        var exclusions = new[] { Path.Combine(root, "excluded") };

        var independentTimer = Stopwatch.StartNew();
        var expectedMeshes = BatchConversionRunner.SourceScanEnumerator.EnumerateFiles(root, [".nif"], exclusions);
        var expectedSupport = BatchConversionRunner.SourceScanEnumerator.EnumerateAllFiles(
            root, exclusions, maxTraversalDepth: 16, includeBodySlideSupport: true);
        independentTimer.Stop();

        var snapshotTimer = Stopwatch.StartNew();
        var snapshot = BatchConversionRunner.SourceScanEnumerator.EnumerateImportFiles(root, exclusions, CancellationToken.None);
        snapshotTimer.Stop();
        Assert.Equal(expectedMeshes, snapshot.MeshFiles);
        Assert.Equal(expectedSupport, snapshot.SupportFiles);
        Assert.Equal(meshCount, snapshot.MeshFiles.Count);

        var importTimer = Stopwatch.StartNew();
        var armor = await new LocalArmorImportService().ImportAsync(root, CancellationToken.None, exclusions);
        importTimer.Stop();
        Assert.Equal(expectedMeshes, armor.MeshFiles);
        Assert.Equal(meshCount, armor.TextureFiles.Count);
        Assert.Contains(Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "Project", "reference.nif"), armor.BodyReferenceFiles);
        output.WriteLine(
            $"Synthetic pack: {meshCount} meshes + {meshCount} textures; independent scans={independentTimer.Elapsed.TotalMilliseconds:F2} ms; " +
            $"single snapshot={snapshotTimer.Elapsed.TotalMilliseconds:F2} ms; full import={importTimer.Elapsed.TotalMilliseconds:F2} ms. " +
            "Timings are observational, not a real-user or cold-cache performance guarantee.");
    }

    [Fact]
    public void SnapshotPreservesUnboundedMeshAndBoundedSupportDepths()
    {
        Write("meshes/armor.nif", "mesh");
        var deep = string.Join('/', Enumerable.Repeat("level", 17));
        Write($"{deep}/deep_armor.nif", "mesh");
        Write($"{deep}/deep_texture.dds", "texture");
        Write($"CalienteTools/BodySlide/{deep}/reference.nif", "reference");

        var snapshot = BatchConversionRunner.SourceScanEnumerator.EnumerateImportFiles(root, null, CancellationToken.None);
        Assert.Equal(BatchConversionRunner.SourceScanEnumerator.EnumerateFiles(root, [".nif"]), snapshot.MeshFiles);
        Assert.Equal(BatchConversionRunner.SourceScanEnumerator.EnumerateAllFiles(
            root, maxTraversalDepth: 16, includeBodySlideSupport: true), snapshot.SupportFiles);
        Assert.Contains(Path.Combine(root, deep.Replace('/', Path.DirectorySeparatorChar), "deep_armor.nif"), snapshot.MeshFiles);
        Assert.DoesNotContain(snapshot.SupportFiles, path => Path.GetFileName(path) == "deep_texture.dds");
    }

    [Fact]
    public async Task RepeatedImportsDiscoverAddedAndRemovedAssetsWithoutStaleGlobalCache()
    {
        Write("meshes/first.nif", "mesh");
        var service = new LocalArmorImportService();
        var first = await service.ImportAsync(root, CancellationToken.None);
        Write("meshes/second.nif", "mesh");
        Write("textures/new.dds", "texture");
        File.Delete(Path.Combine(root, "meshes", "first.nif"));
        var second = await service.ImportAsync(root, CancellationToken.None);

        Assert.EndsWith("first.nif", Assert.Single(first.MeshFiles));
        Assert.EndsWith("second.nif", Assert.Single(second.MeshFiles));
        Assert.EndsWith("new.dds", Assert.Single(second.TextureFiles));
    }

    [Fact]
    public void SnapshotHonorsCancellationBeforeTraversal()
    {
        Write("meshes/armor.nif", "mesh");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            BatchConversionRunner.SourceScanEnumerator.EnumerateImportFiles(root, null, cancellation.Token));
    }

    [Fact]
    public void SnapshotSkipsDirectoryLinksAndCycles()
    {
        if (OperatingSystem.IsWindows()) return;
        Write("meshes/armor.nif", "mesh");
        Directory.CreateSymbolicLink(Path.Combine(root, "cycle"), root);
        try
        {
            var snapshot = BatchConversionRunner.SourceScanEnumerator.EnumerateImportFiles(root, null, CancellationToken.None);
            Assert.Single(snapshot.MeshFiles);
            Assert.Single(snapshot.SupportFiles);
        }
        finally
        {
            Directory.Delete(Path.Combine(root, "cycle"));
        }
    }

    [Fact]
    public async Task DirectMeshImportKeepsOnlySelectedWeightPairAndBoundedSupport()
    {
        Write("meshes/armor_0.nif", "mesh");
        Write("meshes/armor_1.nif", "mesh");
        Write("meshes/unrelated.nif", "mesh");
        Write("textures/armor.dds", "texture");
        var deep = string.Join('/', Enumerable.Repeat("level", 17));
        Write($"{deep}/deep_texture.dds", "texture");

        var armor = await new LocalArmorImportService().ImportAsync(
            Path.Combine(root, "meshes", "armor_0.nif"), CancellationToken.None);

        Assert.Equal(2, armor.MeshFiles.Count);
        Assert.DoesNotContain(armor.MeshFiles, path => Path.GetFileName(path) == "unrelated.nif");
        Assert.EndsWith("armor.dds", Assert.Single(armor.TextureFiles));
        Assert.Single(armor.WeightVariantPairs!);
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
