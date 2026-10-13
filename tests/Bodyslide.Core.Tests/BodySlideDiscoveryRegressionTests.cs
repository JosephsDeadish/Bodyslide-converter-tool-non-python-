namespace Bodyslide.Core.Tests;

public sealed class BodySlideDiscoveryRegressionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slidesmith-discovery", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ResolveAsync_CanceledBeforeDiscovery_DoesNotScan()
    {
        Directory.CreateDirectory(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var armor = new ImportedArmor(root, [], [], [], []);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", cancellation.Token));
    }

    [Theory]
    [InlineData("project.osp")]
    [InlineData("PROJECT.OSP")]
    public async Task ResolveAsync_OverlappingRoots_DiscoverProjectOnceAndReportTiming(string fileName)
    {
        var bodySlideRoot = Path.Combine(root, "CalienteTools", "BodySlide");
        var projectDirectory = Path.Combine(bodySlideRoot, "SliderSets", "nested");
        Directory.CreateDirectory(projectDirectory);
        var mesh = Path.Combine(root, "meshes", "armor_1.nif");
        Directory.CreateDirectory(Path.GetDirectoryName(mesh)!);
        File.WriteAllText(mesh, "synthetic-discovery-only");
        var osp = Path.Combine(projectDirectory, fileName);
        File.WriteAllText(osp, """
            <SliderSetInfo>
              <SliderSet name="ArmorProject">
                <OutputPath>meshes</OutputPath>
                <OutputFile>armor</OutputFile>
                <Slider name="RegressionSlider" />
              </SliderSet>
            </SliderSetInfo>
            """);
        var armor = new ImportedArmor(root, [mesh], [], [], [osp]);

        var result = await BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None);

        Assert.Contains("RegressionSlider", result.Sliders);
        Assert.Equal(1, result.Sliders.Count(slider => slider == "RegressionSlider"));
        Assert.NotNull(result.SourceAssetSupport);
        Assert.True(result.SourceAssetSupport.HasOsp);
        Assert.Equal(1, result.SourceAssetSupport.SourceObservedSliderCount);
        Assert.Equal("source-observed-plus-profile-defaults", result.SourceAssetSupport.SliderDataProvenance);
        Assert.Equal("parsed-not-version-validated", result.SourceAssetSupport.SliderDataVerificationStatus);
        Assert.Equal(1, result.SourceAssetSupport.DiscoveredFileCount);
        Assert.True(result.SourceAssetSupport.DiscoveryMilliseconds >= 0);
    }

    [Fact]
    public async Task ResolveAsync_WithoutSourceAssets_ReportsProfileDefaultsOnly()
    {
        var result = await BodySlideSourceProjectSupport.ResolveAsync(
            new ImportedArmor(root, [], [], [], []), "CBBE", CancellationToken.None);

        Assert.NotNull(result.SourceAssetSupport);
        Assert.Equal(0, result.SourceAssetSupport!.SourceObservedSliderCount);
        Assert.Equal("profile-defaults-only", result.SourceAssetSupport.SliderDataProvenance);
        Assert.Equal("not-observed", result.SourceAssetSupport.SliderDataVerificationStatus);
    }

    [Fact]
    public async Task ResolveAsync_DirectoryLinkCycle_IsNotTraversed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var bodySlideRoot = Path.Combine(root, "BodySlide");
        Directory.CreateDirectory(bodySlideRoot);
        var link = Path.Combine(bodySlideRoot, "BodySlide");
        Directory.CreateSymbolicLink(link, bodySlideRoot);
        var armor = new ImportedArmor(root, [], [], [], []);
        try
        {
            var result = await BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None);
            Assert.Equal(0, result.SourceAssetSupport!.DiscoveredFileCount);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void SourceScan_DirectoryLinkCycle_IsNotTraversed()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(root);
        var mesh = Path.Combine(root, "armor.nif");
        File.WriteAllText(mesh, "synthetic-discovery-only");
        var link = Path.Combine(root, "loop");
        Directory.CreateSymbolicLink(link, root);
        try
        {
            var files = BatchConversionRunner.SourceScanEnumerator.EnumerateFiles(root, [".nif"]);
            Assert.Equal([mesh], files);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
