using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace Bodyslide.Core.Tests;

public sealed class PhysicsReadinessRegressionTests
{
    [Theory]
    [InlineData("smp+cbpc")]
    [InlineData("cbpc+smp")]
    public async Task ThreeBaDefaultsIncludeFrameworkSupportedThighPhysics(string profile)
    {
        var weighted = new WeightedMesh("mixed", "default", false);
        var physics = await new BasicPhysicsSupportService().BuildAsync(weighted, "3BA", profile, CancellationToken.None);
        Assert.False(weighted.PhysicsWeightsTransferred);
        foreach (var xml in new[] { physics.CbpcConfigXml, physics.SmpConfigXml })
        {
            var bones = XDocument.Parse(xml!).Descendants("bone").Select(b => (string)b.Attribute("name")!).ToArray();
            Assert.Contains("NPC L Thigh", bones);
            Assert.Contains("NPC R Thigh", bones);
            Assert.All(bones, bone => Assert.True(
                SkeletonMappingCatalog.IsBoneSupportedByFramework("xpmsse-female-advanced", bone), bone));
        }
        var report = BuildCompatibilityReport(physics);
        Assert.True(report.GeneratedPhysicsSlotCount >= 4);
        Assert.True(report.GeneratedPhysicsFamilyCount >= 4);
        Assert.Contains("NPC L Thigh", report.GeneratedPhysicsBones);
    }

    [Fact]
    public async Task UnclassifiedDeclaredBoneDoesNotProduceEmptyCbpcButRemainsAvailableToSmp()
    {
        const string bone = "CustomSecondaryNode";
        var physics = await new BasicPhysicsSupportService().BuildAsync(
            new WeightedMesh("mixed", "default", true, TargetPhysicsBones: [bone]),
            "Custom", "cbpc+smp", CancellationToken.None);

        Assert.Null(physics.CbpcConfigXml);
        Assert.Equal(bone, (string?)Assert.Single(XDocument.Parse(physics.SmpConfigXml!).Descendants("bone")).Attribute("name"));
    }

    [Fact]
    public async Task WhitespaceOnlyDeclaredBonesDoNotProduceEmptyRuntimeConfigs()
    {
        var physics = await new BasicPhysicsSupportService().BuildAsync(
            new WeightedMesh("mixed", "default", true, TargetPhysicsBones: ["", " "]),
            "Custom", "cbpc+smp", CancellationToken.None);

        Assert.Null(physics.CbpcConfigXml);
        Assert.Null(physics.SmpConfigXml);
    }

    [Theory]
    [InlineData("<CBPCConfig/>", "<system/>")]
    [InlineData("<CBPCConfig><bone name=\"NPC L Breast01\"/>", "<system><bone name=\"NPC L Breast01\"/>")]
    [InlineData("<unrelated><bone name=\"NPC L Breast01\"/></unrelated>", "<unrelated><bone name=\"NPC L Breast01\"/></unrelated>")]
    [InlineData("<CBPCConfig><bone name=\" \"/></CBPCConfig>", "<system><bone name=\" \"/></system>")]
    [InlineData("<CBPCConfig><!-- <bone name=\"NPC L Breast01\"/> --></CBPCConfig>", "<system><!-- <bone name=\"NPC L Breast01\"/> --></system>")]
    [InlineData("<!DOCTYPE CBPCConfig [<!ENTITY bone 'NPC L Breast01'>]><CBPCConfig><bone name=\"&bone;\"/></CBPCConfig>", "<!DOCTYPE system [<!ENTITY bone 'NPC L Breast01'>]><system><bone name=\"&bone;\"/></system>")]
    public void InvalidPhysicsContentCannotCountAsGeneratedRuntimeConfigs(string cbpc, string smp)
    {
        var report = BuildCompatibilityReport(new PhysicsConfig("cbpc+smp", cbpc, smp));

        Assert.Empty(report.GeneratedRuntimeConfigs);
        Assert.Contains("cbpc-config.xml", report.MissingRuntimeConfigs);
        Assert.Contains("smp-config.xml", report.MissingRuntimeConfigs);
        Assert.False(report.HasRequiredRuntimeConfigs);
        Assert.False(report.IsCompatible);
    }

    [Fact]
    public void ValidSmpCannotHideEmptyCbpcInHybridReadiness()
    {
        var report = BuildCompatibilityReport(new PhysicsConfig("cbpc+smp",
            "<CBPCConfig/>", "<system><bone name=\"NPC L Breast01\"/></system>"));

        Assert.Equal("smp-config.xml", Assert.Single(report.GeneratedRuntimeConfigs));
        Assert.Equal("cbpc-config.xml", Assert.Single(report.MissingRuntimeConfigs));
        Assert.False(report.IsCompatible);
    }

    private static PhysicsCompatibilityReport BuildCompatibilityReport(PhysicsConfig physics) =>
        (PhysicsCompatibilityReport)typeof(LocalExportService)
            .GetMethod("BuildPhysicsCompatibilityReport", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [new ImportedArmor("unused", [], [], [], []), "3BA",
                new SkeletonMappingResult("xpmsse", "xpmsse", [], []), physics, Array.Empty<string>()])!;

    [Theory]
    [InlineData("Vanilla Beast")]
    [InlineData("3BA")]
    [InlineData("UBE")]
    public async Task GeneratedPhysicsReferencesOnlyResolvedTargetBones(string target)
    {
        var mesh = new ConvertedMesh("creature", "vertex-projection", 1, new Dictionary<string, double>());
        var weighted = await new BasicWeightTransferService().TransferAsync(
            mesh, new MeshAnalysis("creature", true, 1), target, null, CancellationToken.None);
        var physics = await new BasicPhysicsSupportService().BuildAsync(weighted, target, "cbpc+smp", CancellationToken.None);
        Assert.NotEmpty(weighted.TargetPhysicsBones!);
        Assert.True(BuiltInBodyMetadataCatalog.TryGet(target, out var metadata));
        foreach (var xml in new[] { physics.CbpcConfigXml, physics.SmpConfigXml })
        {
            Assert.NotNull(xml);
            var bones = XDocument.Parse(xml!).Descendants("bone").Select(b => (string)b.Attribute("name")!).ToArray();
            Assert.NotEmpty(bones);
            Assert.All(bones, bone =>
            {
                Assert.Contains(bone, weighted.TargetPhysicsBones!);
                Assert.True(SkeletonMappingCatalog.IsBoneSupportedByFramework(metadata.SkeletonFramework, bone), bone);
            });
        }
    }

    [Theory]
    [InlineData("HDT Mouth", "MouthPhysics")]
    [InlineData("HDT Mouth & \"<Tip>", "MouthPhysics")]
    [InlineData("HDT HighHeel_L", "HeelPhysics")]
    [InlineData("WingTip.L", "WingPhysics")]
    public async Task CbpcSecondaryGroupsRetainExactDeclaredBoneReferences(string bone, string group)
    {
        var physics = await new BasicPhysicsSupportService().BuildAsync(
            new WeightedMesh("mixed", "default", true, TargetPhysicsBones: [bone]),
            "Custom", "cbpc", CancellationToken.None);
        var section = XDocument.Parse(physics.CbpcConfigXml!).Root!.Element(group);
        Assert.NotNull(section);
        Assert.Equal(bone, (string?)Assert.Single(section!.Elements("bone")).Attribute("name"));
    }

    [Fact]
    public async Task HeadgearDoesNotRegenerateHumanBodyPhysicsFallbacks()
    {
        var weighted = await new BasicWeightTransferService().TransferAsync(
            new ConvertedMesh("headgear", "rigid-headgear", 1, new Dictionary<string, double>()),
            new MeshAnalysis("headgear", true, 1, HeadgearSubType: HeadgearSubTypes.FullHelmet),
            "3BA", null, CancellationToken.None);
        var physics = await new BasicPhysicsSupportService().BuildAsync(weighted, "3BA", "cbpc+smp", CancellationToken.None);
        Assert.Null(physics.CbpcConfigXml);
        Assert.Null(physics.SmpConfigXml);
    }

    [Fact]
    public async Task UnknownTargetDoesNotGenerateHumanPhysicsFallbacks()
    {
        var weighted = await new BasicWeightTransferService().TransferAsync(
            new ConvertedMesh("creature", "vertex-projection", 1, new Dictionary<string, double>()),
            new MeshAnalysis("creature", true, 1), "UndeclaredCustomRig", null, CancellationToken.None);
        Assert.Empty(weighted.TargetPhysicsBones!);
        var physics = await new BasicPhysicsSupportService().BuildAsync(weighted, "UndeclaredCustomRig", "smp", CancellationToken.None);
        Assert.Null(physics.SmpConfigXml);
    }

    [Fact]
    public async Task BundledSourceSkeletonDoesNotProveUnknownTargetBoneSupport()
    {
        var root = CreateFixtureDirectory();
        try
        {
            const string bone = "NPC XyzzyQz";
            var skeleton = Path.Combine(root, "skeleton.nif");
            using (var stream = File.Create(skeleton))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
                writer.Write(new byte[17 + 3]);
                writer.Write((ushort)0);
                writer.Write((uint)1);
                writer.Write((uint)bone.Length);
                writer.Write((uint)bone.Length);
                writer.Write(Encoding.ASCII.GetBytes(bone));
            }
            Assert.Contains(bone, SkeletonNifBoneParser.ExtractBoneNames(File.ReadAllBytes(skeleton)));
            var armor = new ImportedArmor(root, [], [], [], [skeleton]);
            var mapping = await new BasicSkeletonMappingService().MapAsync(armor, "3BA", CancellationToken.None);
            Assert.Contains(bone, mapping.UnsupportedBones);
            Assert.DoesNotContain(mapping.BoneMappings, m => m.TargetBone == bone);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task UndeclaredCustomFrameworkDoesNotInheritHumanPhysicsBones()
    {
        var root = CreateFixtureDirectory();
        try
        {
            var config = Path.Combine(root, "source.xml");
            await File.WriteAllTextAsync(config, "<system><bone name=\"NPC L Breast01\" /></system>");
            var armor = new ImportedArmor(root, [], [], [config], [], CustomBodyProfiles:
            [
                new CustomBodyProfile("UnknownRig", [], [], [], 0, 0, new Dictionary<string, double>(),
                    SkeletonFramework: "unsupported-framework")
            ]);
            var mapping = await new BasicSkeletonMappingService().MapAsync(armor, "UnknownRig", CancellationToken.None);
            Assert.Contains("NPC L Breast01", mapping.UnsupportedBones);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SparseBootBottomBandIsNotRaisedHeelOrAnkleEvidence()
    {
        var vertices = Enumerable.Range(0, 20).Select(i => new MeshVertex(0, 0, i == 0 ? 0 : 10)).ToArray();
        var signature = new MeshGeometrySignature(20, vertices, 0, 1, 0, 1, 0, 10);
        var method = typeof(NifGeometrySignatureReader).GetMethod("AnalyzeHeelProfile", BindingFlags.Static | BindingFlags.NonPublic)!;
        var report = Assert.IsType<HeelAnalysisReport>(method.Invoke(null,
            ["boots_0.nif", new NifGeometrySignatureReader.NifMeshMetadata(null, [37, 38], []), signature]));
        Assert.Equal("footwear", report.Profile);
        Assert.Contains("geometry:mesh-local-bottom-band-not-ground-or-ankle-proof", report.Evidence);
        Assert.DoesNotContain(report.Evidence, e => e.StartsWith("low-ground-contact:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("zip")]
    [InlineData("tar")]
    [InlineData("tar.gz")]
    public void ArchiveLimitsRejectWholePackAndRemovePartialWorkspace(string format)
    {
        var root = CreateFixtureDirectory();
        var prefix = "readiness-limit-" + Guid.NewGuid().ToString("N");
        try
        {
            var path = Path.Combine(root, "input." + format);
            WriteArchive(path, format);
            foreach (var limits in new[]
            {
                new ArchiveExtractionHelper.ExtractionLimits(MaximumEntryBytes: 3),
                new ArchiveExtractionHelper.ExtractionLimits(MaximumTotalBytes: 7),
                new ArchiveExtractionHelper.ExtractionLimits(MaximumEntries: 1)
            })
            {
                Assert.Throws<InvalidDataException>(() =>
                    ArchiveExtractionHelper.ExtractToTemporaryWorkspace(path, prefix, limits: limits));
                var extractionRoot = Path.Combine(Path.GetTempPath(), prefix);
                Assert.True(!Directory.Exists(extractionRoot) || !Directory.EnumerateDirectories(extractionRoot).Any());
            }
        }
        finally
        {
            Directory.Delete(root, true);
            var extractionRoot = Path.Combine(Path.GetTempPath(), prefix);
            if (Directory.Exists(extractionRoot)) Directory.Delete(extractionRoot, true);
        }
    }

    [Fact]
    public void StreamByteLimitIsEnforcedBeforeWritingEvenWithoutProgress()
    {
        var budgetType = typeof(ArchiveExtractionHelper).GetNestedType("ExtractionBudget", BindingFlags.NonPublic)!;
        var budget = Activator.CreateInstance(budgetType, new ArchiveExtractionHelper.ExtractionLimits(MaximumTotalBytes: 3));
        var method = typeof(ArchiveExtractionHelper).GetMethod("CopyStreamWithCancellation", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var input = new MemoryStream(new byte[4]);
        using var output = new MemoryStream();
        var error = Assert.Throws<TargetInvocationException>(() =>
            method.Invoke(null, [input, output, CancellationToken.None, null, long.MaxValue, budget]));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(0, output.Length);
    }

    [Theory]
    [InlineData("zip")]
    [InlineData("tar")]
    [InlineData("tar.gz")]
    public void ArchiveWithoutProgressStillExtractsEveryByte(string format)
    {
        var root = CreateFixtureDirectory();
        string? extracted = null;
        try
        {
            var path = Path.Combine(root, "input." + format);
            WriteArchive(path, format);
            extracted = ArchiveExtractionHelper.ExtractToTemporaryWorkspace(path, "readiness-complete");
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(Path.Combine(extracted, "first.nif")));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(Path.Combine(extracted, "second.nif")));
        }
        finally
        {
            Directory.Delete(root, true);
            if (extracted is not null) Directory.Delete(extracted, true);
        }
    }

    private static string CreateFixtureDirectory()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), ".readiness-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteArchive(string path, string format)
    {
        using var file = File.Create(path);
        if (format == "zip")
        {
            using var archive = new ZipArchive(file, ZipArchiveMode.Create);
            foreach (var name in new[] { "first.nif", "second.nif" })
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write([1, 2, 3, 4]);
            }
            return;
        }

        using var gzip = format == "tar.gz" ? new GZipStream(file, CompressionMode.Compress, leaveOpen: true) : null;
        using var tar = new TarWriter(gzip is null ? file : gzip, leaveOpen: true);
        foreach (var name in new[] { "first.nif", "second.nif" })
        {
            using var data = new MemoryStream([1, 2, 3, 4]);
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = data });
        }
    }
}
