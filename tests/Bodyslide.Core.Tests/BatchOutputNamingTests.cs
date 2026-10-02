namespace Bodyslide.Core.Tests;

public sealed class BatchOutputNamingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameNamedWeightPairsProduceSeparatePackagesWithDefaultModules(bool caseDistinctDirectories)
    {
        if (caseDistinctDirectories && OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-batch-collision", Guid.NewGuid().ToString("N"));
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "results");
        try
        {
            foreach (var folder in caseDistinctDirectories ? new[] { "Armor", "armor" } : new[] { "first", "second" })
            {
                var directory = Path.Combine(input, "meshes", "armor", folder);
                Directory.CreateDirectory(directory);
                foreach (var weight in new[] { 0, 1 })
                {
                    await File.WriteAllBytesAsync(Path.Combine(directory, $"cuirass_{weight}.nif"), new byte[64]);
                }
            }

            var results = await new BatchConversionRunner(StandaloneConversionModules.CreateDefault())
                .ConvertAsync(new ConversionRequest(input, "CBBE", output));

            Assert.Equal(2, results.Count);
            Assert.All(results, result =>
            {
                Assert.True(result.Success);
                Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "conversion-manifest.json")));
                var produced = Directory.GetFiles(Path.Combine(result.OutputDirectory, "meshes"), "*.nif", SearchOption.AllDirectories);
                Assert.Contains(produced, path => Path.GetFileName(path) == "cuirass_0.nif");
                Assert.Contains(produced, path => Path.GetFileName(path) == "cuirass_1.nif");
            });
            Assert.Equal(2, results.Select(result => result.OutputDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            var plugins = Directory.GetFiles(output, "*.esp", SearchOption.TopDirectoryOnly);
            Assert.Equal(2, plugins.Length);
            foreach (var result in results)
            {
                var identity = Path.GetFileName(result.OutputDirectory);
                var plugin = Assert.Single(plugins, path => Path.GetFileName(path).Contains($"_{identity}_cuirass_0", StringComparison.Ordinal));
                var content = System.Text.Encoding.ASCII.GetString(await File.ReadAllBytesAsync(plugin)).Replace('\\', '/');
                foreach (var name in new[] { "cuirass_0.nif", "cuirass_1.nif", "cuirass_1stperson_0.nif", "cuirass_ground.nif" })
                {
                    if (name != "cuirass_1.nif")
                    {
                        Assert.Contains($"slidesmith/cbbe/{identity}/{name}", content, StringComparison.OrdinalIgnoreCase);
                    }
                    Assert.True(File.Exists(Path.Combine(result.OutputDirectory, "meshes", "slidesmith", "cbbe", identity, name)));
                }
                var projectPath = Assert.Single(Directory.GetFiles(result.OutputDirectory, "*.osp", SearchOption.AllDirectories));
                var project = System.Xml.Linq.XDocument.Load(projectPath);
                Assert.All(project.Descendants("OutputPath"), element =>
                    Assert.Equal($"meshes/slidesmith/cbbe/{identity}/", element.Value.Replace('\\', '/')));
                Assert.All(project.Descendants("SliderSet"), element =>
                    Assert.StartsWith(identity + "_", element.Attribute("name")!.Value, StringComparison.Ordinal));
            }
            Assert.Equal(2, results.Select(result => Path.GetFileName(Assert.Single(
                Directory.GetFiles(result.OutputDirectory, "*.osp", SearchOption.AllDirectories))))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CollisionsReserveNaturalNamesAndRemainStableRegardlessOfInputOrder()
    {
        var root = Path.Combine(Path.GetTempPath(), "batch-naming");
        string[] files =
        [
            Path.Combine(root, "a", "armor_0.nif"),
            Path.Combine(root, "b", "armor_0.nif"),
            Path.Combine(root, "c", "armor_2.nif"),
            Path.Combine(root, "d", "armor_3.nif"),
            Path.Combine(root, "e", "ARMOR_0.nif"),
            Path.Combine(root, "f", "unique.nif")
        ];

        var names = BatchConversionRunner.BuildBatchOutputNames(files);
        var reversed = BatchConversionRunner.BuildBatchOutputNames(files.Reverse().ToArray());

        Assert.Equal(files.Length, names.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("armor", names[files[0]]);
        Assert.Equal("armor_4", names[files[1]]);
        Assert.Equal("armor_2", names[files[2]]);
        Assert.Equal("armor_3", names[files[3]]);
        Assert.Equal("ARMOR_5", names[files[4]]);
        Assert.Equal("unique", names[files[5]]);
        Assert.All(files, path => Assert.Equal(names[path], reversed[path]));
    }
}
