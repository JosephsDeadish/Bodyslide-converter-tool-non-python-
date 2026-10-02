using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class CatalogIntegrityTests
{
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
