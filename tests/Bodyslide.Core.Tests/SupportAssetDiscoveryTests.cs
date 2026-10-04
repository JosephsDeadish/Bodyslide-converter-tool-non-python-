using Bodyslide.Core;
using System.Reflection;
using System.Threading;

namespace Bodyslide.Core.Tests;

public sealed class SupportAssetDiscoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportBodySlideSupportPreservesMeshAndGeneratedOutputExclusions(bool excludeSupport)
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-support-import", Guid.NewGuid().ToString("N"));
        var supportRoot = Path.Combine(root, "CalienteTools", "BodySlide");
        var generatedRoot = Path.Combine(root, "generated");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "meshes"));
            Directory.CreateDirectory(Path.Combine(supportRoot, "ShapeData"));
            Directory.CreateDirectory(Path.Combine(supportRoot, "SliderSets"));
            Directory.CreateDirectory(Path.Combine(generatedRoot, "CalienteTools", "BodySlide", "SliderSets"));
            var mesh = Path.Combine(root, "meshes", "armor_0.nif");
            var reference = Path.Combine(supportRoot, "ShapeData", "reference.nif");
            var project = Path.Combine(supportRoot, "SliderSets", "armor.osp");
            var generatedProject = Path.Combine(generatedRoot, "CalienteTools", "BodySlide", "SliderSets", "generated.osp");
            await File.WriteAllBytesAsync(mesh, new byte[64]);
            await File.WriteAllBytesAsync(reference, new byte[64]);
            await File.WriteAllTextAsync(project, "<SliderSetInfo />");
            await File.WriteAllTextAsync(generatedProject, "<SliderSetInfo />");
            await File.WriteAllTextAsync(Path.Combine(generatedRoot, "conversion-manifest.json"), "{}");

            var armor = await new LocalArmorImportService().ImportAsync(
                root, CancellationToken.None, excludeSupport ? [supportRoot] : null);

            Assert.Equal(mesh, Assert.Single(armor.MeshFiles));
            Assert.DoesNotContain(generatedProject, armor.BodyReferenceFiles);
            if (excludeSupport)
            {
                Assert.DoesNotContain(reference, armor.BodyReferenceFiles);
                Assert.DoesNotContain(project, armor.BodyReferenceFiles);
            }
            else
            {
                Assert.Contains(reference, armor.BodyReferenceFiles);
                Assert.Contains(project, armor.BodyReferenceFiles);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DiscoverSupportAssets_ReturnsPluginsAndMaterialsFromSingleTreeScan()
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-support-assets", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "mods", "pack");
        var outputRoot = Path.Combine(root, "mods", "pack", "output");

        try
        {
            Directory.CreateDirectory(Path.Combine(sourceRoot, "meshes"));
            Directory.CreateDirectory(Path.Combine(sourceRoot, "textures"));
            Directory.CreateDirectory(Path.Combine(sourceRoot, "plugins"));
            Directory.CreateDirectory(outputRoot);

            var nifPath = Path.Combine(sourceRoot, "meshes", "armor.nif");
            var materialPath = Path.Combine(sourceRoot, "textures", "armor.bgsm");
            var pluginPath = Path.Combine(sourceRoot, "plugins", "armor.esp");
            var excludedPluginPath = Path.Combine(outputRoot, "ignored.esm");

            File.WriteAllText(nifPath, "mesh");
            File.WriteAllText(materialPath, "material");
            File.WriteAllText(pluginPath, "plugin");
            File.WriteAllText(excludedPluginPath, "ignored");

            var discovery = (LocalExportService.SupportAssetDiscoveryResult)typeof(LocalExportService)
                .GetMethod("DiscoverSupportAssets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, [sourceRoot, outputRoot, null, CancellationToken.None])!;

            Assert.Contains(pluginPath, discovery.PluginFiles);
            Assert.DoesNotContain(excludedPluginPath, discovery.PluginFiles);
            Assert.Contains(materialPath, discovery.MaterialFiles);
            Assert.DoesNotContain(nifPath, discovery.MaterialFiles);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void DiscoverSupportAssets_PreservesMixedCaseExtensionsAndFreshExcludedScans()
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-support-assets-fresh", Guid.NewGuid().ToString("N"));
        var excludedRoot = Path.Combine(root, "excluded");
        var sharedRoot = Path.Combine(root, "shared");
        try
        {
            Directory.CreateDirectory(excludedRoot);
            Directory.CreateDirectory(sharedRoot);
            var firstPlugin = Path.Combine(root, "ZArmor.ESP");
            var secondPlugin = Path.Combine(root, "aArmor.EsL");
            var firstMaterial = Path.Combine(root, "ZArmor.BGSM");
            var secondMaterial = Path.Combine(root, "aArmor.BgEm");
            File.WriteAllText(firstPlugin, "plugin");
            File.WriteAllText(firstMaterial, "material");
            File.WriteAllText(Path.Combine(excludedRoot, "ignored.ESP"), "excluded");
            File.WriteAllText(Path.Combine(sharedRoot, "ignored.BGSM"), "excluded");

            var first = LocalExportService.DiscoverSupportAssets(root, excludedRoot, sharedRoot);
            Assert.Equal(firstPlugin, Assert.Single(first.PluginFiles));
            Assert.Equal(firstMaterial, Assert.Single(first.MaterialFiles));

            File.WriteAllText(secondPlugin, "plugin");
            File.WriteAllText(secondMaterial, "material");
            var added = LocalExportService.DiscoverSupportAssets(root, excludedRoot, sharedRoot);
            Assert.Equal(new[] { secondPlugin, firstPlugin }, added.PluginFiles);
            Assert.Equal(new[] { secondMaterial, firstMaterial }, added.MaterialFiles);

            File.Delete(firstPlugin);
            File.Delete(firstMaterial);
            var removed = LocalExportService.DiscoverSupportAssets(root, excludedRoot, sharedRoot);
            Assert.Equal(secondPlugin, Assert.Single(removed.PluginFiles));
            Assert.Equal(secondMaterial, Assert.Single(removed.MaterialFiles));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DiscoverSupportAssets_HonorsCancellationBeforeTreeScan()
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-support-assets-cancel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var exception = Assert.Throws<TargetInvocationException>(() =>
                typeof(LocalExportService)
                    .GetMethod("DiscoverSupportAssets", BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null, [root, null, null, cancellation.Token]));

            Assert.IsType<OperationCanceledException>(exception.InnerException);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void BodySlideSupportXmlDetection_RecognizesSliderGroupsMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-support-xml", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var sliderGroups = Path.Combine(root, "CalienteTools", "BodySlide", "SliderGroups", "DemoArmor.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(sliderGroups)!);
            File.WriteAllText(sliderGroups, "<SliderGroups><SliderSet name=\"DemoArmor\" /></SliderGroups>");

            var unrelatedXml = Path.Combine(root, "notes.xml");
            File.WriteAllText(unrelatedXml, "<root />");

            Assert.True(BodySlideSourceProjectSupport.IsLikelyBodySlideSupportXml(sliderGroups));
            Assert.False(BodySlideSourceProjectSupport.IsLikelyBodySlideSupportXml(unrelatedXml));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

}
