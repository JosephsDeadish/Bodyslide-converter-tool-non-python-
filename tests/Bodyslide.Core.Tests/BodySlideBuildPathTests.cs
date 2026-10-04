using System.Xml.Linq;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class BodySlideBuildPathTests
{
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
            var builtFileNames = paired
                ? new[] { output.Value + "_0.nif", output.Value + "_1.nif" }
                : new[] { output.Value + ".nif" };
            Assert.Equal(armor.MeshFiles.Select(Path.GetFileName), builtFileNames);
        }
        finally { Directory.Delete(root, true); }
    }
}
