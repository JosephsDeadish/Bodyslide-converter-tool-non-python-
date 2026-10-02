using System.Reflection;

namespace Bodyslide.Core.Tests;

[Collection("NonParallel")]
public sealed class BodySlideDirectoryCacheTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "slidesmith-directory-cache", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DirectorySnapshot_UnchangedDirectory_ReusesListing()
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "project.osp"), "<SliderSetInfo />");
        var first = ReadSnapshot(root);
        var second = ReadSnapshot(root);
        Assert.Same(first, second);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DirectorySnapshot_AddedOrDeletedEntries_InvalidatesListing(bool add)
    {
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "project.osp");
        File.WriteAllText(file, "<SliderSetInfo />");
        var first = ReadSnapshot(root);
        var before = Directory.GetLastWriteTimeUtc(root);
        if (add)
        {
            File.WriteAllText(Path.Combine(root, "another.osp"), "<SliderSetInfo />");
        }
        else
        {
            File.Delete(file);
        }
        Directory.SetLastWriteTimeUtc(root, before.AddSeconds(1));

        var second = ReadSnapshot(root);
        Assert.NotSame(first, second);
        var files = (IReadOnlyList<string>)second.GetType().GetProperty("Files")!.GetValue(second)!;
        Assert.Equal(add ? 2 : 0, files.Count);
    }

    [Fact]
    public async Task ResolveAsync_CachedDirectory_StillReloadsModifiedOsp()
    {
        var sliderSets = Path.Combine(root, "BodySlide", "SliderSets");
        Directory.CreateDirectory(sliderSets);
        var mesh = Path.Combine(root, "armor_1.nif");
        File.WriteAllText(mesh, "synthetic-discovery-only");
        var osp = Path.Combine(sliderSets, "project.osp");
        WriteOsp(osp, "FirstSlider");
        var armor = new ImportedArmor(root, [mesh], [], [], []);
        var first = await BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None);
        Assert.Contains("FirstSlider", first.Sliders);

        var previous = File.GetLastWriteTimeUtc(osp);
        WriteOsp(osp, "OtherSlider");
        File.SetLastWriteTimeUtc(osp, previous.AddSeconds(1));
        var second = await BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None);

        Assert.Contains("OtherSlider", second.Sliders);
        Assert.DoesNotContain("FirstSlider", second.Sliders);
    }

    [Fact]
    public void DirectorySnapshot_CancellationIsHonoredEvenOnCacheHit()
    {
        Directory.CreateDirectory(root);
        ReadSnapshot(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = Assert.Throws<TargetInvocationException>(() => ReadSnapshot(root, cancellation.Token));
        Assert.IsType<OperationCanceledException>(exception.InnerException);
    }

    private static object ReadSnapshot(string path, CancellationToken cancellationToken = default) =>
        typeof(BodySlideSourceProjectSupport)
            .GetMethod("ReadDirectorySnapshot", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [path, cancellationToken])!;

    [Fact]
    public async Task ResolveAsync_ExcessiveDirectoryFanout_FailsInsteadOfTruncatingDiscovery()
    {
        var bodySlideRoot = Path.Combine(root, "BodySlide");
        Directory.CreateDirectory(bodySlideRoot);
        for (var index = 0; index < 10001; index++)
        {
            Directory.CreateDirectory(Path.Combine(bodySlideRoot, $"project-{index:D5}"));
        }

        var armor = new ImportedArmor(root, [], [], [], []);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None));

        Assert.Contains("select a narrower input folder", error.Message);
    }

    [Fact]
    public async Task ResolveAsync_PreviouslyInvalidOsp_RetriesAfterFileStampChanges()
    {
        var sliderSets = Path.Combine(root, "BodySlide", "SliderSets");
        Directory.CreateDirectory(sliderSets);
        var osp = Path.Combine(sliderSets, "project.osp");
        File.WriteAllText(osp, "<invalid");
        var mesh = Path.Combine(root, "armor_1.nif");
        File.WriteAllText(mesh, "synthetic-discovery-only");
        var armor = new ImportedArmor(root, [mesh], [], [], []);
        var first = await BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None);
        Assert.DoesNotContain("RecoveredSlider", first.Sliders);

        var previous = File.GetLastWriteTimeUtc(osp);
        WriteOsp(osp, "RecoveredSlider");
        File.SetLastWriteTimeUtc(osp, previous.AddSeconds(1));
        var second = await BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None);
        Assert.Contains("RecoveredSlider", second.Sliders);
    }

    private static void WriteOsp(string path, string slider) =>
        File.WriteAllText(path, $"""
            <SliderSetInfo>
              <SliderSet name="ArmorProject">
                <OutputPath>meshes</OutputPath>
                <OutputFile>armor</OutputFile>
                <Slider name="{slider}" />
              </SliderSet>
            </SliderSetInfo>
            """);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
