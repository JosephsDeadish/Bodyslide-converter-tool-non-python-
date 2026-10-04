using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class BodySlideSourceAssociationTests
{
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
}
