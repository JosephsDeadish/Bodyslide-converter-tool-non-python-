using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class CatalogIntegrityTests
{
    [Fact]
    public void BuiltInBodyMetadataCatalog_AllSkeletonFoundationsResolve()
    {
        foreach (var body in BuiltInBodyMetadataCatalog.All)
        {
            Assert.True(
                SkeletonFoundationAliasCatalog.TryResolve(body.SkeletonFoundation, out var foundation),
                $"{body.Name} has an unresolved skeleton foundation: {body.SkeletonFoundation}");
            Assert.False(string.IsNullOrWhiteSpace(foundation));
        }
    }

    [Theory]
    [InlineData("COCO CBBE", "xpmsse")]
    [InlineData("COCO UUNP", "xpmsse")]
    [InlineData("UBE", "ube-extended")]
    [InlineData("Vanilla", "vanilla-skyrim")]
    [InlineData("Vanilla Beast", "beast-humanoid")]
    [InlineData("Serpentine Humanoid", "serpentine-humanoid")]
    [InlineData("Goat Humanoid", "horned-humanoid")]
    [InlineData("Hagraven", "winged-humanoid")]
    [InlineData("Spriggan", "spriggan-branch")]
    [InlineData("Equine Humanoid", "equine-humanoid")]
    [InlineData("Avian Humanoid", "avian-humanoid")]
    [InlineData("Draconic Humanoid", "draconic-humanoid")]
    public void BuiltInBodyMetadataCatalog_DescriptiveFoundationsPreserveRigFamily(
        string bodyName, string expectedFoundation)
    {
        Assert.True(BuiltInBodyMetadataCatalog.TryGet(bodyName, out var body));
        Assert.True(SkeletonFoundationAliasCatalog.TryResolve(body.SkeletonFoundation, out var foundation));
        Assert.Equal(expectedFoundation, foundation);
    }

    [Fact]
    public void BuiltInBodyMetadataCatalog_AliasesRoundTripAndPhysicsBonesStayDistinct()
    {
        foreach (var body in BuiltInBodyMetadataCatalog.All)
        {
            Assert.True(BuiltInBodyMetadataCatalog.TryResolveCanonicalName(body.Name, out var canonicalBodyName));
            Assert.Equal(body.Name, canonicalBodyName);
            Assert.True(BuiltInBodyMetadataCatalog.TryGet(body.Name, out var resolvedBody));
            Assert.Equal(body.Name, resolvedBody.Name);

            foreach (var alias in body.Aliases)
            {
                Assert.False(string.IsNullOrWhiteSpace(alias));
                Assert.True(BuiltInBodyMetadataCatalog.TryResolveCanonicalName(alias, out var aliasCanonicalBodyName));
                Assert.Equal(body.Name, aliasCanonicalBodyName);
                Assert.True(BuiltInBodyMetadataCatalog.TryGet(alias, out var aliasResolvedBody));
                Assert.Equal(body.Name, aliasResolvedBody.Name);
            }

            Assert.Equal(body.AvailablePhysicsBones.Count, body.AvailablePhysicsBones.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(body.AvailablePhysicsBones, bone => Assert.False(string.IsNullOrWhiteSpace(bone)));
        }
    }
}
