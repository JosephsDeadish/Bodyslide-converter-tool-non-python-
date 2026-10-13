using System.Xml.Linq;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class BodySlideBuildPathTests
{
    [Fact]
    public void ConvertedNifDestinationsPreserveMeshesSubdirectoriesAndResolveResidualCollisions()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "meshes", "slidesmith", "CBBE");
        var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var firstSource = Path.Combine(sourceRoot, "meshes", "armor-a", "shared.nif");
        var secondSource = Path.Combine(sourceRoot, "meshes", "armor-b", "shared.nif");

        var first = LocalExportService.GetConvertedNifDestinationPath(outputRoot, firstSource, usedPaths, @"meshes\slidesmith\CBBE\armor-a\");
        var second = LocalExportService.GetConvertedNifDestinationPath(outputRoot, secondSource, usedPaths, @"meshes\slidesmith\CBBE\armor-b\");
        var duplicate = LocalExportService.GetConvertedNifDestinationPath(outputRoot, firstSource, usedPaths, @"meshes\slidesmith\CBBE\armor-a\");

        Assert.EndsWith(Path.Combine("armor-a", "shared.nif"), first, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("armor-b", "shared.nif"), second, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("armor-a", "shared__2.nif"), duplicate, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, usedPaths.Count);
    }

    [Fact]
    public void DuplicateBodySlideMeshNamesReceiveDistinctBuildOutputPaths()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var firstSource = Path.Combine(sourceRoot, "meshes", "armor-a", "shared_0.nif");
        var secondSource = Path.Combine(sourceRoot, "meshes", "armor-b", "shared_0.nif");

        var outputPaths = LocalExportService.BuildCollisionSafeBodySlideMeshOutputPaths(
            [firstSource, secondSource],
            "CBBE");

        Assert.Equal(@"meshes\slidesmith\cbbe\armor-a\", outputPaths[firstSource]);
        Assert.Equal(@"meshes\slidesmith\cbbe\armor-b\", outputPaths[secondSource]);
    }

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
            new Dictionary<string, IReadOnlyList<SkyrimSseNifShape>>(StringComparer.OrdinalIgnoreCase)
            {
                ["mesh.nif"] = shapes
            });
        Assert.Equal(2, result.WithheldMorphRecordCount);
        Assert.Empty(result.OsdFiles);
        var document = XDocument.Parse(result.OspXml);
        var sliderSet = Assert.Single(document.Descendants("SliderSet"));
        var ospShapes = sliderSet.Elements("Shape").ToArray();
        Assert.Equal(new[] { "Torso", "Sleeves" }, ospShapes.Select(static shape => shape.Value).ToArray());
        Assert.Equal(new[] { "Torso", "Sleeves" }, ospShapes.Select(static shape => (string?)shape.Attribute("target")).ToArray());

        var bellySlider = sliderSet.Elements("Slider")
            .Single(static slider => (string?)slider.Attribute("name") == "Belly");
        var dataLinks = bellySlider.Elements("Data").ToArray();
        Assert.Empty(dataLinks);

        var zapSlider = sliderSet.Elements("Slider")
            .Single(static slider => (string?)slider.Attribute("name") == "HideSleeves");
        Assert.Empty(zapSlider.Elements("Data"));
    }

    [Fact]
    public void BodySlideSourceReferences_KeepDuplicateOutputFilenamesDistinct()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var firstMesh = Path.Combine(outputDirectory, "meshes", "armor-a", "shared_0.nif");
        var secondMesh = Path.Combine(outputDirectory, "meshes", "armor-b", "shared_0.nif");
        Directory.CreateDirectory(Path.GetDirectoryName(firstMesh)!);
        Directory.CreateDirectory(Path.GetDirectoryName(secondMesh)!);
        File.WriteAllBytes(firstMesh, "first mesh"u8.ToArray());
        File.WriteAllBytes(secondMesh, "second mesh"u8.ToArray());
        const string ospXml = """
            <SliderSetInfo version="1">
              <SliderSet name="Armor A">
                <SourceFile>shared_0.nif</SourceFile>
                <OutputPath>meshes\armor-a\</OutputPath>
                <OutputFile gender="female" GenWeights="true">shared</OutputFile>
              </SliderSet>
              <SliderSet name="Armor B">
                <SourceFile>shared_0.nif</SourceFile>
                <OutputPath>meshes\armor-b\</OutputPath>
                <OutputFile gender="female" GenWeights="true">shared</OutputFile>
              </SliderSet>
            </SliderSetInfo>
            """;

        try
        {
            var result = LocalExportService.BuildBodySlideSourceNifReferences(
                ospXml,
                outputDirectory,
                [firstMesh, secondMesh]);

            Assert.Empty(result.UnresolvedSliderSets);
            Assert.Equal(new[] { "shared_0.nif", "shared_0__2.nif" }, result.References.Select(static reference => reference.FileName));
            Assert.Equal(
                new[] { "first mesh", "second mesh" },
                result.References.Select(reference => System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(reference.SourcePath))));
            var sourceFiles = XDocument.Parse(result.OspXml)
                .Descendants("SliderSet")
                .Select(set => (string?)set.Element("SourceFile"))
                .ToArray();
            Assert.Equal(new[] { "shared_0.nif", "shared_0__2.nif" }, sourceFiles);

            var shapeExport = LocalExportService.BuildShapeLinkedOsdExport(
                result.OspXml,
                new Dictionary<string, IReadOnlyList<SkyrimSseNifShape>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["shared_0.nif"] = [new("ArmorAShape", [new(0, 0, 0)], [], 12)],
                    ["shared_0__2.nif"] = [new("ArmorBShape", [new(0, 0, 0)], [], 12)]
                });
            var shapeNames = XDocument.Parse(shapeExport.OspXml)
                .Descendants("SliderSet")
                .Select(set => (string?)set.Element("Shape"))
                .ToArray();
            Assert.Equal(new[] { "ArmorAShape", "ArmorBShape" }, shapeNames);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void BodySlideSourceReferencesUseOutputPathContextForDuplicateMeshNames()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var firstMesh = Path.Combine(outputDirectory, "meshes", "slidesmith", "CBBE", "armor-a", "shared_0.nif");
        var secondMesh = Path.Combine(outputDirectory, "meshes", "slidesmith", "CBBE", "armor-b", "shared_0.nif");
        Directory.CreateDirectory(Path.GetDirectoryName(firstMesh)!);
        Directory.CreateDirectory(Path.GetDirectoryName(secondMesh)!);
        File.WriteAllBytes(firstMesh, "armor A"u8.ToArray());
        File.WriteAllBytes(secondMesh, "armor B"u8.ToArray());
        const string ospXml = """
            <SliderSetInfo version="1">
              <SliderSet name="Armor A">
                <SourceFile>shared_0.nif</SourceFile>
                <OutputPath>meshes\armor-a\</OutputPath>
                <OutputFile gender="female" GenWeights="true">shared</OutputFile>
              </SliderSet>
              <SliderSet name="Armor B">
                <SourceFile>shared_0.nif</SourceFile>
                <OutputPath>meshes\armor-b\</OutputPath>
                <OutputFile gender="female" GenWeights="true">shared</OutputFile>
              </SliderSet>
            </SliderSetInfo>
            """;

        try
        {
            var result = LocalExportService.BuildBodySlideSourceNifReferences(
                ospXml,
                outputDirectory,
                [firstMesh, secondMesh]);

            Assert.Empty(result.UnresolvedSliderSets);
            Assert.Equal(
                new[] { "armor A", "armor B" },
                result.References.Select(reference => System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(reference.SourcePath))).ToArray());
            Assert.Equal(
                new[] { "shared_0.nif", "shared_0__2.nif" },
                XDocument.Parse(result.OspXml)
                    .Descendants("SliderSet")
                    .Select(set => (string?)set.Element("SourceFile"))
                    .ToArray());
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void ValidateShapeDataLinks_ReportsWithheldMorphsAndValidatesAuthoredLinks()
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
            new Dictionary<string, IReadOnlyList<SkyrimSseNifShape>>(StringComparer.OrdinalIgnoreCase)
            {
                ["mesh.nif"] = shapes
            });
        Assert.Equal(2, export.WithheldMorphRecordCount);
        Assert.Empty(export.OsdFiles);
        var shapeDataDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            File.WriteAllBytes(
                Path.Combine(shapeDataDirectory, "mesh.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso", "Sleeves"));
            foreach (var (fileName, bytes) in export.OsdFiles)
            {
                File.WriteAllBytes(Path.Combine(shapeDataDirectory, fileName), bytes);
            }

            var document = XDocument.Parse(export.OspXml);
            Assert.Contains(LocalExportService.ValidateShapeDataLinks(document, shapeDataDirectory),
                static problem => problem.Contains("exactly one OSD data link for shape 'Torso'", StringComparison.Ordinal));

            var slider = Assert.Single(document.Descendants("Slider"));
            slider.Add(
                new XElement("Data", new XAttribute("name", "torso-fit"), new XAttribute("target", "Torso"), new XAttribute("local", "true"), "Demo.osd\\torso-fit"),
                new XElement("Data", new XAttribute("name", "sleeves-fit"), new XAttribute("target", "Sleeves"), new XAttribute("local", "true"), "Demo.osd\\sleeves-fit"));
            File.WriteAllBytes(
                Path.Combine(shapeDataDirectory, "Demo.osd"),
                CreateEmptyOsdPayload("torso-fit", "sleeves-fit"));
            Assert.Empty(LocalExportService.ValidateShapeDataLinks(document, shapeDataDirectory));

            var sleevesShape = document.Descendants("Shape")
                .Single(static shape => (string?)shape.Attribute("target") == "Sleeves");
            sleevesShape.SetAttributeValue("target", "UnknownShape");
            Assert.Contains(LocalExportService.ValidateShapeDataLinks(document, shapeDataDirectory),
                static problem => problem.Contains("Shape targets do not match", StringComparison.Ordinal));
            sleevesShape.SetAttributeValue("target", "Sleeves");

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

    [Fact]
    public void ValidateShapeDataLinks_RejectsMissingOrUnsupportedSourceNif()
    {
        const string ospXml = """
            <SliderSetInfo version="1">
              <SliderSet name="Demo">
                <DataFolder>Demo</DataFolder>
                <SourceFile>mesh.nif</SourceFile>
                <Shape target="Torso">Torso</Shape>
                <Slider name="Belly" default="0">
                  <Data name="morph" target="Torso" local="true">Demo.osd\morph</Data>
                </Slider>
              </SliderSet>
            </SliderSetInfo>
            """;
        var shapeDataDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var document = XDocument.Parse(ospXml);
            Assert.Contains(LocalExportService.ValidateShapeDataLinks(document, shapeDataDirectory),
                static problem => problem.Contains("SourceFile is missing, unreadable, or uses an unsupported NIF layout", StringComparison.Ordinal));

            File.WriteAllBytes(Path.Combine(shapeDataDirectory, "mesh.nif"), "not a NIF"u8.ToArray());
            Assert.Contains(LocalExportService.ValidateShapeDataLinks(document, shapeDataDirectory),
                static problem => problem.Contains("SourceFile is missing, unreadable, or uses an unsupported NIF layout", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(shapeDataDirectory, recursive: true);
        }
    }

    [Fact]
    public void ValidateShapeDataLinks_RejectsOsdVertexIndexOutsideTargetShape()
    {
        const string ospXml = """
            <SliderSetInfo version="1">
              <SliderSet name="Demo">
                <DataFolder>Demo</DataFolder>
                <SourceFile>mesh.nif</SourceFile>
                <Shape target="Torso">Torso</Shape>
                <Slider name="Belly" default="0">
                  <Data name="morph" target="Torso" local="true">Demo.osd\morph</Data>
                </Slider>
              </SliderSet>
            </SliderSetInfo>
            """;
        var shapeDataDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            File.WriteAllBytes(
                Path.Combine(shapeDataDirectory, "mesh.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            File.WriteAllBytes(
                Path.Combine(shapeDataDirectory, "Demo.osd"),
                CreateOsdPayload("morph", vertexIndex: 3));

            var problems = LocalExportService.ValidateShapeDataLinks(XDocument.Parse(ospXml), shapeDataDirectory);

            Assert.Contains(problems, static problem =>
                problem.Contains("vertex index outside target shape 'Torso' (3 vertices)", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(shapeDataDirectory, recursive: true);
        }
    }

    [Fact]
    public void ShapeLinkedOsdExport_ReportsSyntheticFallbackWithoutShapeProvenance()
    {
        const string ospXml = """
            <SliderSetInfo version="1">
              <SliderSet name="Demo">
                <SourceFile>mesh.nif</SourceFile>
                <Slider name="Belly" default="0" />
              </SliderSet>
            </SliderSetInfo>
            """;
        var shape = new SkyrimSseNifShape(
            "Torso",
            [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)],
            [0, 1, 2],
            12);
        var export = LocalExportService.BuildShapeLinkedOsdExport(
            ospXml,
            new Dictionary<string, IReadOnlyList<SkyrimSseNifShape>>(StringComparer.OrdinalIgnoreCase)
            {
                ["mesh.nif"] = [shape]
            });

        Assert.Equal(1, export.WithheldMorphRecordCount);
        Assert.Empty(export.OsdFiles);
        Assert.Empty(XDocument.Parse(export.OspXml).Descendants("Data"));
    }

    private static byte[] CreateOsdPayload(string recordName, ushort vertexIndex)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(new byte[] { 0x4f, 0x53, 0x44, 0x00 });
        writer.Write(3);
        writer.Write(1);
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(recordName);
        writer.Write((byte)nameBytes.Length);
        writer.Write(nameBytes);
        writer.Write((ushort)1);
        writer.Write(vertexIndex);
        writer.Write(0.1f);
        writer.Write(0f);
        writer.Write(0f);
        return stream.ToArray();
    }

    private static byte[] CreateEmptyOsdPayload(params string[] recordNames)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(new byte[] { 0x4f, 0x53, 0x44, 0x00 });
        writer.Write(3);
        writer.Write(recordNames.Length);
        foreach (var recordName in recordNames)
        {
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(recordName);
            writer.Write((byte)nameBytes.Length);
            writer.Write(nameBytes);
            writer.Write((ushort)0);
        }

        return stream.ToArray();
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
            var hasWeightVariant = inputName.EndsWith("_0.nif", StringComparison.OrdinalIgnoreCase) ||
                                   inputName.EndsWith("_1.nif", StringComparison.OrdinalIgnoreCase);
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
            Assert.Equal(paired || hasWeightVariant ? "true" : "false", output.Attribute("GenWeights")!.Value);
            Assert.Equal(
                hasWeightVariant ? Path.GetFileNameWithoutExtension(inputName)[..^2] : Path.GetFileNameWithoutExtension(inputName),
                output.Value);
            foreach (var slider in set.Elements("Slider"))
            {
                var generatesWeights = paired || hasWeightVariant;
                Assert.Equal("0", slider.Attribute(generatesWeights ? "small" : "default")?.Value);
                Assert.Equal(generatesWeights ? "0" : null, slider.Attribute(generatesWeights ? "big" : "small")?.Value);
                Assert.Null(slider.Element("Low"));
                Assert.Null(slider.Element("High"));
            }
            var builtFileNames = paired || hasWeightVariant
                ? new[] { output.Value + "_0.nif", output.Value + "_1.nif" }
                : new[] { output.Value + ".nif" };
            Assert.Equal(paired || hasWeightVariant ? 2 : 1, builtFileNames.Length);
        }
        finally { Directory.Delete(root, true); }
    }
}
