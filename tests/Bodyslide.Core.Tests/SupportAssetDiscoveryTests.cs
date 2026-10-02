using Bodyslide.Core;
using System.Reflection;
using System.Threading;

namespace Bodyslide.Core.Tests;

public sealed class SupportAssetDiscoveryTests
{
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
