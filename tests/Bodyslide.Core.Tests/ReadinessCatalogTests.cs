using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class ReadinessCatalogTests
{
    [Theory]
    [InlineData("Caliente's Beautiful Bodies Edition", "CBBE")]
    [InlineData("DIMONIZED UNP", "UNP")]
    [InlineData("DIMONIZED UNP female body", "UNP")]
    [InlineData("SevenBase", "UUNP")]
    [InlineData("7B", "UUNP")]
    [InlineData("7Base Bombshell", "UUNP")]
    [InlineData("Shape Atlas for Men Light", "SAM Light")]
    [InlineData("Schlongs of Skyrim SE", "SOS")]
    public void KnownAliases_ResolveWithoutChangingTheCanonicalBody(string alias, string canonical)
    {
        foreach (var spelling in new[] { alias, $"  {alias.ToLowerInvariant()}  ", SkeletonTextNormalization.Slugify(alias) })
        {
            Assert.True(BuiltInBodyMetadataCatalog.TryResolveCanonicalName(spelling, out var resolved));
            Assert.Equal(canonical, resolved);
            Assert.True(BuiltInBodyMetadataCatalog.TryGet(spelling, out var body));
            Assert.Equal(canonical, body.Name);
        }
    }

    [Fact]
    public void Aliases_HaveOneOwnerEvenAfterNormalization()
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var body in BuiltInBodyMetadataCatalog.All)
        {
            foreach (var name in body.Aliases.Prepend(body.Name))
            {
                var key = SkeletonTextNormalization.Slugify(name);
                Assert.False(string.IsNullOrWhiteSpace(key));
                if (owners.TryGetValue(key, out var owner))
                {
                    Assert.Equal(owner, body.Name);
                }
                else
                {
                    owners.Add(key, body.Name);
                }
            }
        }
    }

    [Fact]
    public void CommonSkeleton_PreservesBothFeetToesAndEveryFingerSegment()
    {
        foreach (var side in new[] { "L", "R" })
        {
            foreach (var bone in new[] { $"NPC {side} Foot", $"NPC {side} Toe0", $"NPC {side} Hand" })
            {
                AssertSupportedBone(bone, "vanilla-skyrim", bone);
            }

            for (var finger = 0; finger < 5; finger++)
            {
                for (var segment = 0; segment < 3; segment++)
                {
                    var bone = $"NPC {side} Finger{finger}{segment}";
                    Assert.Contains(bone, SkeletonMappingCatalog.CommonBones);
                    AssertSupportedBone(bone, "vanilla-skyrim", bone);
                    AssertSupportedBone($"{bone} [{side}F{finger}{segment}]", "vanilla-skyrim", bone);
                }
            }
        }
    }

    [Theory]
    [InlineData("NPC Head [Head]", "NPC Head")]
    [InlineData("NPC L Hand [LHnd]", "NPC L Hand")]
    [InlineData("NPC R Hand [RHnd]", "NPC R Hand")]
    [InlineData("NPC L Foot [Lft ]", "NPC L Foot")]
    [InlineData("NPC R Foot [Rft ]", "NPC R Foot")]
    [InlineData("NPC L Toe0 [LToe]", "NPC L Toe0")]
    [InlineData("NPC R Toe0 [RToe]", "NPC R Toe0")]
    public void TaggedSkeletonNames_ResolveToCanonicalCommonBones(string source, string expected) =>
        AssertSupportedBone($"  {source}  ", "vanilla-skyrim", expected);

    [Theory]
    [InlineData("NPC Tail [Tail]", "NPC Tail")]
    [InlineData("NPC TailBone001", "Tail1")]
    [InlineData("NPC TailBone002", "Tail2")]
    [InlineData("NPC TailBone003", "Tail3")]
    [InlineData("NPC TailBone001 [Tlb1]", "Tail1")]
    [InlineData("NPC TailBone002 [Tlb2]", "Tail2")]
    [InlineData("NPC TailBone003 [Tlb3]", "Tail3")]
    public void CanonicalBeastTailNames_MapOnlyToTailBearingFrameworks(string source, string expected)
    {
        foreach (var framework in new[] { "beast-humanoid", "equine-humanoid", "digitigrade-beast", "serpentine-humanoid" })
        {
            AssertSupportedBone(source, framework, expected);
        }

        Assert.False(SkeletonMappingCatalog.TryResolveSupportedBone(source, "vanilla-skyrim", out _));
        Assert.False(SkeletonMappingCatalog.TryResolveSupportedBone(source, "xpmsse-female-cbbe", out _));
    }

    [Fact]
    public void AdvertisedPhysicsBones_HaveSupportedMappingsAndRepairGroups()
    {
        foreach (var body in BuiltInBodyMetadataCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(body.SkeletonFramework));
            foreach (var bone in body.AvailablePhysicsBones)
            {
                Assert.True(
                    SkeletonMappingCatalog.TryResolveSupportedBone(bone, body.SkeletonFramework, out var mapped),
                    $"{body.Name}: {bone} has no supported mapping in {body.SkeletonFramework}.");
                Assert.True(SkeletonMappingCatalog.IsBoneSupportedByFramework(body.SkeletonFramework, mapped));
                Assert.True(PhysicsRepairCatalog.TryMatchGroup(bone, out _), $"{body.Name}: {bone} has no repair group.");
            }
        }
    }

    [Fact]
    public void BilateralPhysicsBones_HavePartnersAndRepairBothSides()
    {
        foreach (var body in BuiltInBodyMetadataCatalog.All)
        {
            foreach (var bone in body.AvailablePhysicsBones)
            {
                var partner = bone.StartsWith("NPC L ", StringComparison.Ordinal)
                    ? bone.Replace("NPC L ", "NPC R ", StringComparison.Ordinal)
                    : bone.StartsWith("NPC R ", StringComparison.Ordinal)
                        ? bone.Replace("NPC R ", "NPC L ", StringComparison.Ordinal)
                        : bone.EndsWith(".L", StringComparison.Ordinal)
                            ? $"{bone[..^2]}.R"
                            : bone.EndsWith(".R", StringComparison.Ordinal)
                                ? $"{bone[..^2]}.L"
                                : null;
                if (partner is null)
                {
                    continue;
                }

                Assert.Contains(partner, body.AvailablePhysicsBones);
                Assert.True(PhysicsRepairCatalog.TryMatchGroup(bone, out var group));
                Assert.True(PhysicsRepairCatalog.TryMatchGroup(partner, out var partnerGroup));
                Assert.Equal(group, partnerGroup);
                var repaired = PhysicsRepairCatalog.RepairTargetBones([bone], body.AvailablePhysicsBones, []);
                Assert.Contains(bone, repaired);
                Assert.Contains(partner, repaired);
            }
        }
    }

    [Theory]
    [InlineData("UBE", "HDT JawLower", "jaw")]
    [InlineData("UBE", "HDT TongueMid", "tongue")]
    [InlineData("TNG", "HDT Throat", "throat")]
    [InlineData("SAM Light", "HDT Mouth", "mouth")]
    [InlineData("Feline Humanoid", "TongueTip", "tongue")]
    [InlineData("Canine Humanoid", "JawLower", "jaw")]
    [InlineData("Equine Humanoid", "TongueMid", "tongue")]
    public void MouthAnchors_StayAlignedWithAdvertisedBonesAndRepairGroups(string bodyName, string bone, string region)
    {
        Assert.True(BuiltInBodyMetadataCatalog.TryGet(bodyName, out var body));
        Assert.Contains(bone, body.AvailablePhysicsBones);
        Assert.Contains(region, body.ExpectedSemanticRegions);
        Assert.True(SemanticAnchorCatalog.TryGet(bodyName, out var profile));
        Assert.Contains(bone, profile.Anchors["mouth"]);
        Assert.True(PhysicsRepairCatalog.TryMatchGroup(bone, out var actualGroup));
        Assert.Equal("mouth", actualGroup);
        AssertSupportedBone(bone, body.SkeletonFramework, bone);
    }

    [Theory]
    [InlineData("HeelIK.L", "NPC L Foot")]
    [InlineData("HeelIK.R", "NPC R Foot")]
    [InlineData("GroundContact.L", "NPC L Foot")]
    [InlineData("GroundContact.R", "NPC R Foot")]
    public void HeelContactFallbacks_PreserveTheCorrectSide(string bone, string expected)
    {
        AssertSupportedBone(bone, "vanilla-skyrim", expected);
        if (bone.StartsWith("HeelIK.", StringComparison.Ordinal))
        {
            AssertSupportedBone(bone, "xpmsse-female-heelik-extended", bone);
        }
    }

    [Fact]
    public void HeadMouthHeelAndBeastSemanticAliases_RemainAvailable()
    {
        Assert.Contains("head", SemanticBoneAliasCatalog.All["head"]);
        Assert.Contains("face", SemanticBoneAliasCatalog.All["head"]);
        Assert.Contains("jaw", SemanticBoneAliasCatalog.All["mouth"]);
        Assert.Contains("tongue", SemanticBoneAliasCatalog.All["mouth"]);
        Assert.Contains("foot", SemanticBoneAliasCatalog.All["heel"]);
        Assert.Contains("pawpad", SemanticBoneAliasCatalog.All["heel"]);
        Assert.Contains("caudal", SemanticBoneAliasCatalog.All["tail"]);
        AssertSupportedBone("TailTip", "beast-humanoid", "Tail3");
        AssertSupportedBone("Tail4", "beast-humanoid", "Tail3");
        AssertSupportedBone("Hock.L", "vanilla-skyrim", "NPC L Calf");
        AssertSupportedBone("Hock.R", "vanilla-skyrim", "NPC R Calf");
        Assert.False(SkeletonMappingCatalog.TryResolveSupportedBone("UnknownCustomBone", "vanilla-skyrim", out _));
    }

    private static void AssertSupportedBone(string source, string framework, string expected)
    {
        Assert.True(SkeletonMappingCatalog.TryResolveSupportedBone(source, framework, out var resolved));
        Assert.Equal(expected, resolved);
        Assert.True(SkeletonMappingCatalog.IsBoneSupportedByFramework(framework, resolved));
    }
}
