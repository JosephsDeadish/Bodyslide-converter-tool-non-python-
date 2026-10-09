using Bodyslide.Core;
using System.Text;

namespace Bodyslide.Core.Tests;

public sealed class BodySlideSourceAssociationTests
{
    [Theory]
    [InlineData(@"meshes\armor\first\", @"meshes\armor\second\")]
    [InlineData("armor/first", "armor/second")]
    [InlineData("meshes/armor/first/", "meshes/armor/first_extra/")]
    public async Task SameNamedMeshesUseDeclaredOutputDirectory(string ownPath, string otherPath)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var meshDirectory = Path.Combine(root, "meshes", "armor", "first");
            var projects = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
            Directory.CreateDirectory(meshDirectory);
            Directory.CreateDirectory(projects);
            var mesh = Path.Combine(meshDirectory, "jacket_0.nif");
            var project = Path.Combine(projects, "pack.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, $"""
                <SliderSetInfo>
                  <SliderSet name="First"><OutputPath>{ownPath}</OutputPath><OutputFile>jacket</OutputFile>
                    <Slider name="FirstFit"/><Slider name="HideFirst" zap="true"/>
                  </SliderSet>
                  <SliderSet name="Second"><OutputPath>{otherPath}</OutputPath><OutputFile>jacket</OutputFile>
                    <Slider name="SecondFit"/><Slider name="HideSecond" zap="true"/>
                  </SliderSet>
                </SliderSetInfo>
                """);
            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);
            Assert.Contains("FirstFit", result.Sliders);
            Assert.Contains("HideFirst", result.ZapSliders);
            Assert.DoesNotContain("SecondFit", result.Sliders);
            Assert.DoesNotContain("HideSecond", result.ZapSliders);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportedPackProjectsDoNotAddUnrelatedArmorSliders(bool singleProjectFile)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var meshes = Path.Combine(root, "meshes");
            var projects = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
            Directory.CreateDirectory(meshes);
            Directory.CreateDirectory(projects);
            var mesh = Path.Combine(meshes, "jacket_0.nif");
            await File.WriteAllTextAsync(mesh, "mesh");
            var first = Path.Combine(projects, "jacket.osp");
            var second = Path.Combine(projects, "boots.osp");
            const string own = "<SliderSet name=\"Jacket\"><OutputFile>jacket</OutputFile><Slider name=\"JacketFit\"/><Slider name=\"HideJacket\" zap=\"true\"/></SliderSet>";
            const string other = "<SliderSet name=\"Boots\"><OutputFile>boots.nif</OutputFile><Slider name=\"BootFit\"/><Slider name=\"HideBoots\" zap=\"true\"/></SliderSet>";
            await File.WriteAllTextAsync(first, "<SliderSetInfo>" + own + (singleProjectFile ? other : "") + "</SliderSetInfo>");
            if (!singleProjectFile) await File.WriteAllTextAsync(second, "<SliderSetInfo>" + other + "</SliderSetInfo>");
            var armor = new ImportedArmor(mesh, [mesh], [], [], singleProjectFile ? [first] : [first, second]);
            var result = await BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None);

            Assert.Contains("JacketFit", result.Sliders);
            Assert.Contains("HideJacket", result.ZapSliders);
            Assert.DoesNotContain("BootFit", result.Sliders);
            Assert.DoesNotContain("HideBoots", result.ZapSliders);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("jacket_1.nif", true)]
    [InlineData("jacket", true)]
    [InlineData("jackets", false)]
    [InlineData("boots", false)]
    public async Task OutputMetadataUsesExactWeightNormalizedMeshIdentity(string outputFile, bool matches)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project,
                $"<SliderSetInfo><SliderSet><OutputFile>{outputFile}</OutputFile><Slider name=\"SourceFit\"/></SliderSet></SliderSetInfo>");
            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);
            Assert.Equal(matches, result.Sliders.Contains("SourceFit"));
            Assert.Equal(matches, result.SourceAssetSupport!.HasOsp);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MatchedOspReportsSettingsThatAreNotPreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket" baseShape="Custom Base" bsversion="19" seamNormals="true" customFlag="true">
                    <DataFolder customRoot="true">../shared-data</DataFolder>
                    <SourceFile variant="reference">base.nif</SourceFile>
                    <OutputPath mode="custom">meshes\custom\</OutputPath>
                    <OutputFile gender="female" GenWeights="true" options="custom">jacket</OutputFile>
                    <Slider name="Fit" default="0.25" small="0.1" big="0.8" invert="true" uv="true">
                      <Data target="Torso">shared.osd#Fit</Data>
                      <Low value="5" custom="true" />
                      <High value="95" />
                      <CustomSliderSetting enabled="true" />
                    </Slider>
                    <Reference>reference.nif</Reference>
                    <Zap target="Sleeves" />
                    <Shape target="Torso">jacket.nif</Shape>
                    <CustomOption enabled="true" />
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Equal(
                new[]
                {
                    "custom-base-shape",
                    "external-data-folder",
                    "inverted-sliders",
                    "nonzero-slider-defaults",
                    "output-options",
                    "output-path-rebuilt",
                    "seam-or-lock-normal-settings",
                    "slider-set-unknown-attributes",
                    "slider-unknown-elements",
                    "slider-weight-range-options",
                    "slider-weight-ranges-rebuilt",
                    "source-osp-version",
                    "source-path-options",
                    "source-reference-links",
                    "source-shape-mappings",
                    "source-slider-data-links",
                    "unknown-slider-set-elements",
                    "uv-slider-data",
                    "weight-variant-output-mode-rebuilt",
                    "zap-target-semantics"
                },
                result.SourceAssetSupport!.UnsupportedOspSemantics);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MatchedOspReportsWeightVariantModeAsRebuilt()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket" baseShape="Base Shape" bsversion="20">
                    <DataFolder>Jacket</DataFolder>
                    <SourceFile>jacket.nif</SourceFile>
                    <OutputPath>meshes\armor\</OutputPath>
                    <OutputFile gender="female" GenWeights="true">jacket</OutputFile>
                    <Slider name="Fit" small="0" big="0" invert="false" zap="false" uv="false" />
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Equal(
                ["output-path-rebuilt", "weight-variant-output-mode-rebuilt"],
                result.SourceAssetSupport!.UnsupportedOspSemantics);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BodyTriMorphsRetainTheirSourceShapeThroughSliderAssociation()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
            var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
            var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
            Directory.CreateDirectory(meshDirectory);
            Directory.CreateDirectory(projectDirectory);
            Directory.CreateDirectory(shapeDataDirectory);

            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="TravelerProject">
                    <DataFolder>TravelerProject</DataFolder>
                    <OutputPath>meshes\armor\traveler\</OutputPath>
                    <OutputFile>traveler_0.nif</OutputFile>
                    <Slider name="Waist" />
                  </SliderSet>
                </SliderSetInfo>
                """);
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "traveler.tri"),
                BodyTri(
                    ("Torso", "Waist", (ushort)0, (short)2),
                    ("ArmorOverlay", "Waist", (ushort)2, (short)3)));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.NotNull(result.ReusableMorphPayloads);
            var waistCandidates = result.ReusableMorphPayloads!["Waist"].Payloads!;
            Assert.Equal(2, waistCandidates.Count);
            Assert.Contains(waistCandidates, candidate =>
                candidate.SourceShapeName == "Torso" &&
                candidate.VertexCount == 1 &&
                candidate.ShapeIdentityStatus == "unresolved" &&
                candidate.VertexOrderStatus == "unverified");
            Assert.Contains(waistCandidates, candidate =>
                candidate.SourceShapeName == "ArmorOverlay" &&
                candidate.VertexCount == 3 &&
                candidate.ShapeIdentityStatus == "unresolved" &&
                candidate.VertexOrderStatus == "unverified");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static byte[] BodyTri(params (string Shape, string Morph, ushort Index, short X)[] shapes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write("PIRT"u8);
        writer.Write((ushort)shapes.Length);
        foreach (var (shape, morph, index, x) in shapes)
        {
            var shapeBytes = Encoding.UTF8.GetBytes(shape);
            var morphBytes = Encoding.UTF8.GetBytes(morph);
            writer.Write((byte)shapeBytes.Length);
            writer.Write(shapeBytes);
            writer.Write((ushort)1);
            writer.Write((byte)morphBytes.Length);
            writer.Write(morphBytes);
            writer.Write(1f);
            writer.Write((ushort)1);
            writer.Write(index);
            writer.Write(x);
            writer.Write((short)0);
            writer.Write((short)0);
        }
        return stream.ToArray();
    }
}
