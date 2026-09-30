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

            var discovery = ConversionPipeline.DiscoverSupportAssets(sourceRoot, outputRoot);

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
}
