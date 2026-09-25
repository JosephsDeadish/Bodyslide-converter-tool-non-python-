namespace Bodyslide.Core.Tests;

public sealed class SkeletonSupportPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "slidesmith-skeleton-support-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryResolveSkeletonNifPath_ReturnsDirectNifPath()
    {
        Directory.CreateDirectory(_root);
        var skeletonPath = Path.Combine(_root, "skeleton.nif");
        File.WriteAllText(skeletonPath, "test");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(skeletonPath, out var result);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(skeletonPath), result);
    }

    [Fact]
    public void TryResolveSkeletonNifPath_ResolvesFromPexSiblingModStructure()
    {
        var scriptsDirectory = Path.Combine(_root, "XPMSSE", "scripts");
        var meshesDirectory = Path.Combine(_root, "XPMSSE", "meshes", "actors", "character", "character assets");
        Directory.CreateDirectory(scriptsDirectory);
        Directory.CreateDirectory(meshesDirectory);

        var pexPath = Path.Combine(scriptsDirectory, "XPMSEWeaponStyleScaleEffect.pex");
        var skeletonPath = Path.Combine(meshesDirectory, "skeleton.nif");
        File.WriteAllText(pexPath, "pex");
        File.WriteAllText(skeletonPath, "nif");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(pexPath, out var result);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(skeletonPath), result);
    }

    [Fact]
    public void TryResolveSkeletonNifPath_ResolvesFromModFolder()
    {
        var modRoot = Path.Combine(_root, "XPMSSE");
        var meshesDirectory = Path.Combine(modRoot, "meshes", "actors", "character", "character assets");
        Directory.CreateDirectory(meshesDirectory);

        var skeletonPath = Path.Combine(meshesDirectory, "skeleton.nif");
        File.WriteAllText(skeletonPath, "nif");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(modRoot, out var result);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(skeletonPath), result);
    }

    [Fact]
    public void TryResolveSkeletonNifPath_ReturnsFalseWhenNoSkeletonExists()
    {
        Directory.CreateDirectory(_root);
        var pexPath = Path.Combine(_root, "only-script.pex");
        File.WriteAllText(pexPath, "pex");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(pexPath, out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    [Fact]
    public void TryResolveSkeletonNifPath_PrefersBaseCharacterAssetsSkeletonOverGenderedVariants()
    {
        var modRoot = Path.Combine(_root, "XPMSSE");
        var baseDirectory = Path.Combine(modRoot, "meshes", "actors", "character", "character assets");
        var femaleDirectory = Path.Combine(modRoot, "meshes", "actors", "character", "character assets female");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(femaleDirectory);

        var baseSkeletonPath = Path.Combine(baseDirectory, "skeleton.nif");
        var femaleSkeletonPath = Path.Combine(femaleDirectory, "skeleton.nif");
        File.WriteAllText(baseSkeletonPath, "base");
        File.WriteAllText(femaleSkeletonPath, "female");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(modRoot, out var result);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(baseSkeletonPath), result);
    }

    [Fact]
    public void TryResolveSkeletonNifPath_ResolvesCustomRigNamedNifFromPreferredDirectory()
    {
        var modRoot = Path.Combine(_root, "CustomRigPack");
        var meshesDirectory = Path.Combine(modRoot, "meshes", "actors", "character", "character assets");
        Directory.CreateDirectory(meshesDirectory);

        var rigPath = Path.Combine(meshesDirectory, "feline_rig.nif");
        File.WriteAllText(rigPath, "rig");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(modRoot, out var result);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(rigPath), result);
    }

    [Fact]
    public void TryResolveSkeletonNifPath_PrefersExactSkeletonNameOverHeuristicRigNamesInSameDirectory()
    {
        var modRoot = Path.Combine(_root, "MixedRigPack");
        var meshesDirectory = Path.Combine(modRoot, "meshes", "actors", "character", "character assets");
        Directory.CreateDirectory(meshesDirectory);

        var rigPath = Path.Combine(meshesDirectory, "xpmsse_rig.nif");
        var skeletonPath = Path.Combine(meshesDirectory, "skeleton.nif");
        File.WriteAllText(rigPath, "rig");
        File.WriteAllText(skeletonPath, "skeleton");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(modRoot, out var result);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(skeletonPath), result);
    }

    [Fact]
    public void TryResolveSkeletonNifPath_DirectBodyMeshFallsBackToContainingSkeletonDirectory()
    {
        var modRoot = Path.Combine(_root, "BodyMeshInputPack");
        var meshesDirectory = Path.Combine(modRoot, "meshes", "actors", "character", "character assets");
        Directory.CreateDirectory(meshesDirectory);

        var bodyPath = Path.Combine(meshesDirectory, "femalebody_0.nif");
        var skeletonPath = Path.Combine(meshesDirectory, "skeleton.nif");
        File.WriteAllText(bodyPath, "body");
        File.WriteAllText(skeletonPath, "skeleton");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(bodyPath, out var result);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(skeletonPath), result);
    }

    [Fact]
    public void TryResolveSkeletonNifPath_DoesNotMistakeBodyMeshesForSkeletons()
    {
        var modRoot = Path.Combine(_root, "BodyOnlyPack");
        var meshesDirectory = Path.Combine(modRoot, "meshes", "actors", "character", "character assets");
        Directory.CreateDirectory(meshesDirectory);

        File.WriteAllText(Path.Combine(meshesDirectory, "femalebody_0.nif"), "body");

        var resolved = SkeletonSupportPathResolver.TryResolveSkeletonNifPath(modRoot, out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
