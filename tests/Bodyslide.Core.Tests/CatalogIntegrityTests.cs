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

    [Fact]
    public void BuiltInBodyMetadataCatalog_AllProfilesHaveRequiredConversionMetadata()
    {
        var requiredRegions = new[]
        {
            "chest", "waist", "pelvis", "legs", "shoulders", "breasts",
            "butt", "belly", "arms", "thighs", "calves"
        };

        Assert.Equal(28, BuiltInBodyMetadataCatalog.All.Count);
        foreach (var body in BuiltInBodyMetadataCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(body.Gender), $"{body.Name} is missing gender metadata.");
            Assert.Contains(body.DefaultPhysics, new[] { "none", "cbpc", "smp", "smp+cbpc" });
            Assert.False(string.IsNullOrWhiteSpace(body.SkeletonFoundation), $"{body.Name} is missing its skeleton foundation.");
            Assert.False(string.IsNullOrWhiteSpace(body.SkeletonFramework), $"{body.Name} is missing its skeleton framework.");
            Assert.NotEmpty(body.Notes);
            Assert.NotEmpty(body.ReferenceTokens);
            Assert.NotEmpty(body.DetectionTokens);
            Assert.NotEmpty(body.SliderNames);
            Assert.True(body.VertexCountMin > 0, $"{body.Name} must have a positive minimum vertex-count hint.");
            Assert.True(body.VertexCountMax >= body.VertexCountMin, $"{body.Name} has an invalid vertex-count range.");
            foreach (var bone in body.AvailablePhysicsBones)
            {
                Assert.True(
                    SkeletonMappingCatalog.TryResolveSupportedBone(bone, body.SkeletonFramework, out _),
                    $"{body.Name} physics bone '{bone}' does not resolve in framework '{body.SkeletonFramework}'.");
            }

            Assert.All(requiredRegions, region =>
                Assert.True(body.TransformationField.TryGetValue(region, out var value) &&
                            double.IsFinite(value) && value > 0,
                    $"{body.Name} is missing a valid '{region}' transformation value."));
        }
    }

    [Fact]
    public void BuiltInBodyMetadataCatalog_PreservesKeyFemaleAndMaleFamilyDistinctions()
    {
        Assert.True(BuiltInBodyMetadataCatalog.TryGet("CBBE", out var cbbe));
        Assert.True(BuiltInBodyMetadataCatalog.TryGet("3BA", out var threeBa));
        Assert.True(BuiltInBodyMetadataCatalog.TryGet("BHUNP", out var bhunp));
        Assert.True(BuiltInBodyMetadataCatalog.TryGet("HIMBO", out var himbo));
        Assert.True(BuiltInBodyMetadataCatalog.TryGet("SOS", out var sos));

        Assert.Equal("none", cbbe.DefaultPhysics);
        Assert.Equal("smp+cbpc", threeBa.DefaultPhysics);
        Assert.Equal("smp+cbpc", bhunp.DefaultPhysics);
        Assert.Contains("CBBE topology", threeBa.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UUNP-family topology", bhunp.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("male", himbo.Gender);
        Assert.Equal("male", sos.Gender);
        Assert.Contains(himbo.AvailablePhysicsBones, bone => bone.Contains("Pec", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sos.AvailablePhysicsBones, bone => bone.StartsWith("SOS ", StringComparison.OrdinalIgnoreCase));
    }
}
