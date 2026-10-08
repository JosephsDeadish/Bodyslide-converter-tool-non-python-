using System.Xml.Linq;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class BodySlideBuildPathTests
{
    [Fact]
    public void ShapeLinkedOsdExport_MapsSliderDataToEachNamedShape()
    {
        const string ospXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <SliderSetInfo version="1">
              <SliderSet name="Demo" baseShape="Base Shape">
                <DataFolder>Demo</DataFolder>
                <SourceFile>mesh.nif</SourceFile>
                <OutputPath>meshes\</OutputPath>
                <OutputFile gender="female" GenWeights="false">mesh</OutputFile>
                <Slider name="Belly" default="0" zap="false" />
                <Slider name="HideSleeves" default="0" zap="true" />
              </SliderSet>
            </SliderSetInfo>
            """;
        var shapes = new SkyrimSseNifShape[]
        {
            new("Torso", [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [0, 1, 2], 12),
            new("Sleeves", [new(0, 0, 1), new(1, 0, 1), new(0, 1, 1)], [0, 1, 2], 12)
        };

        var result = LocalExportService.BuildShapeLinkedOsdExport(
            ospXml,
            "Demo",
            new Dictionary<string, IReadOnlyList<SkyrimSseNifShape>>(StringComparer.OrdinalIgnoreCase)
            {
                ["mesh.nif"] = shapes
            },
            new Dictionary<string, double>());
        Assert.Equal(2, result.SyntheticMorphRecordCount);
        var document = XDocument.Parse(result.OspXml);
        var sliderSet = Assert.Single(document.Descendants("SliderSet"));
        var ospShapes = sliderSet.Elements("Shape").ToArray();
        Assert.Equal(new[] { "Torso", "Sleeves" }, ospShapes.Select(static shape => shape.Value).ToArray());
        Assert.Equal(new[] { "Torso", "Sleeves" }, ospShapes.Select(static shape => (string?)shape.Attribute("target")).ToArray());

        var bellySlider = sliderSet.Elements("Slider")
            .Single(static slider => (string?)slider.Attribute("name") == "Belly");
        var dataLinks = bellySlider.Elements("Data").ToArray();
        Assert.Equal(2, dataLinks.Length);
        Assert.Equal(new[] { "Torso", "Sleeves" }, dataLinks.Select(static data => (string?)data.Attribute("target")).ToArray());
        Assert.All(dataLinks, data =>
        {
            Assert.Equal("true", (string?)data.Attribute("local"));
            Assert.EndsWith(".osd\\" + data.Attribute("name")!.Value, data.Value, StringComparison.Ordinal);
        });

        var zapSlider = sliderSet.Elements("Slider")
            .Single(static slider => (string?)slider.Attribute("name") == "HideSleeves");
        Assert.Empty(zapSlider.Elements("Data"));
        var osdFile = Assert.Single(result.OsdFiles);
        Assert.Equal("Demo.osd", osdFile.FileName);
        Assert.True(OsdMorphReader.TryRead(osdFile.Bytes, out var payload));
        Assert.Equal(dataLinks.Select(static data => (string)data.Attribute("name")!).OrderBy(static name => name),
            payload!.Morphs.Select(static morph => morph.Name).OrderBy(static name => name));
        Assert.All(payload.Morphs, static morph => Assert.NotEmpty(morph.SparseDeltas));
    }

    [Fact]
    public void ValidateShapeDataLinks_RejectsMissingShapeLinkAndOsdRecord()
    {
        const string ospXml = """
            <SliderSetInfo version="1">
              <SliderSet name="Demo">
                <DataFolder>Demo</DataFolder>
                <SourceFile>mesh.nif</SourceFile>
                <Slider name="Belly" default="0" />
              </SliderSet>
            </SliderSetInfo>
            """;
        var shapes = new SkyrimSseNifShape[]
        {
            new("Torso", [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [0, 1, 2], 12),
            new("Sleeves", [new(0, 0, 1), new(1, 0, 1), new(0, 1, 1)], [0, 1, 2], 12)
        };
        var export = LocalExportService.BuildShapeLinkedOsdExport(
            ospXml,
            "Demo",
            new Dictionary<string, IReadOnlyList<SkyrimSseNifShape>>(StringComparer.OrdinalIgnoreCase)
            {
                ["mesh.nif"] = shapes
            },
            new Dictionary<string, double>());
        var shapeDataDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            foreach (var (fileName, bytes) in export.OsdFiles)
            {
                File.WriteAllBytes(Path.Combine(shapeDataDirectory, fileName), bytes);
            }

            var document = XDocument.Parse(export.OspXml);
            Assert.Empty(LocalExportService.ValidateShapeDataLinks(document, shapeDataDirectory));

            var slider = Assert.Single(document.Descendants("Slider"));
            slider.Elements("Data").First().Remove();
            Assert.Contains(LocalExportService.ValidateShapeDataLinks(document, shapeDataDirectory),
                static problem => problem.Contains("exactly one OSD data link", StringComparison.Ordinal));

            slider.Add(new XElement("Data",
                new XAttribute("name", "missing-record"),
                new XAttribute("target", "Torso"),
                new XAttribute("local", "true"),
                "Demo.osd\\missing-record"));
            Assert.Contains(LocalExportService.ValidateShapeDataLinks(document, shapeDataDirectory),
                static problem => problem.Contains("missing OSD record 'missing-record'", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(shapeDataDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("GNDgloves.nif", null)]
    [InlineData("GNDgloves.v2.nif", null)]
    [InlineData("BDE_Gloves_0.nif", "BDE_Gloves_1.nif")]
    [InlineData("BDE_Gloves_0.nif", null)]
    [InlineData("BDE_Gloves_1.nif", null)]
    public async Task VersionOneProjectResolvesSourceUsingBodySlideLoaderSemantics(string inputName, string? highName)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var paired = highName is not null;
            var low = Path.Combine(root, inputName);
            var high = Path.Combine(root, highName ?? inputName);
            var armor = new ImportedArmor(low, paired ? [low, high] : [low], [], [], []);
            var project = await new BodySlideOspProjectService().GenerateAsync(armor,
                new ConvertedMesh("plate", "test", armor.MeshFiles.Count, new Dictionary<string, double>()),
                "3BA", CancellationToken.None);
            var shapeData = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData");
            var projectDirectory = Path.Combine(shapeData, project.ProjectName);
            Directory.CreateDirectory(projectDirectory);
            await File.WriteAllTextAsync(Path.Combine(projectDirectory, Path.GetFileName(low)), "input");
            var document = XDocument.Parse(project.OspXml);
            Assert.Equal("1", document.Root!.Attribute("version")!.Value);
            var set = Assert.Single(document.Descendants("SliderSet"));
            var folder = set.Element("DataFolder")?.Value ?? string.Empty;
            var source = set.Element("SourceFile")!.Value;
            Assert.Equal(project.ProjectName, folder);
            Assert.Equal(Path.GetFileName(low), source);
            var resolved = Path.Combine(shapeData, folder.Replace('\\', Path.DirectorySeparatorChar),
                source.Replace('\\', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(resolved), $"BodySlide could not resolve {resolved}");
            Assert.Null(set.Element("SetFolder"));
            var output = Assert.Single(set.Elements("OutputFile"));
            Assert.Equal(paired ? "true" : "false", output.Attribute("GenWeights")!.Value);
            Assert.Equal(paired ? "BDE_Gloves" : Path.GetFileNameWithoutExtension(inputName), output.Value);
            foreach (var slider in set.Elements("Slider"))
            {
                Assert.Equal("0", slider.Attribute(paired ? "small" : "default")?.Value);
                Assert.Equal(paired ? "0" : null, slider.Attribute(paired ? "big" : "small")?.Value);
                Assert.Null(slider.Element("Low"));
                Assert.Null(slider.Element("High"));
            }
            var builtFileNames = paired
                ? new[] { output.Value + "_0.nif", output.Value + "_1.nif" }
                : new[] { output.Value + ".nif" };
            Assert.Equal(armor.MeshFiles.Select(Path.GetFileName), builtFileNames);
        }
        finally { Directory.Delete(root, true); }
    }
}
