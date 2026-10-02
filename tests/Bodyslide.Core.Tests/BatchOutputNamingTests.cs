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
                var plugin = Assert.Single(plugins, path => Path.GetFileName(path) == $"SlideSmith_{identity}_0.esp");
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
    public async Task ScratchPluginNamesDoNotCollideWithNaturalArmorNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-batch-plugin-names", Guid.NewGuid().ToString("N"));
        try
        {
            var input = Path.Combine(root, "input");
            var output = Path.Combine(root, "output");
            foreach (var (folder, name) in new[] { ("first", "cuirass"), ("second", "cuirass"), ("third", "cuirass_cuirass") })
            {
                var directory = Path.Combine(input, folder);
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(Path.Combine(directory, $"{name}_0.nif"), new byte[64]);
            }
            var results = await new BatchConversionRunner(StandaloneConversionModules.CreateDefault())
                .ConvertAsync(new ConversionRequest(input, "CBBE", output));
            Assert.Equal(3, results.Count);
            Assert.All(results, result => Assert.True(result.Success));
            Assert.Equal(3, Directory.GetFiles(output, "*.esp", SearchOption.TopDirectoryOnly).Length);
            Assert.Equal(3, results.Select(result => Path.GetFileName(Assert.Single(
                Directory.GetFiles(Path.Combine(result.OutputDirectory, "CalienteTools", "BodySlide", "SliderSets"), "*.osp"))))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedSourcePluginsRetainRewriteMappingsFromEveryBatchItem(bool archived)
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-batch-source-plugins", Guid.NewGuid().ToString("N"));
        try
        {
            var input = Path.Combine(root, "input");
            var output = Path.Combine(root, "output");
            var fixture = GetFixtureDirectory("RealisticLinkedModularFrameworkModPack");
            foreach (var file in Directory.GetFiles(fixture, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(input, Path.GetRelativePath(fixture, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            var source = input;
            if (archived)
            {
                source = Path.Combine(root, "pack.zip");
                System.IO.Compression.ZipFile.CreateFromDirectory(input, source);
            }
            var results = await new BatchConversionRunner(StandaloneConversionModules.CreateDefault())
                .ConvertAsync(new ConversionRequest(source, "CBBE", output));
            Assert.True(results.Count > 1);
            Assert.All(results, result => Assert.True(result.Success));
            var mappings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var result in results)
            {
                using var report = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(
                    Path.Combine(result.OutputDirectory, "plugin-patches.json")));
                foreach (var mapping in report.RootElement.GetProperty("RewriteMappings").EnumerateArray())
                    mappings.Add(mapping.GetProperty("RewrittenMeshPath").GetString()!.Replace('\\', '/'));
                var projectName = Assert.Single(result.Steps, step => step.StartsWith("bodyslide:", StringComparison.Ordinal))
                    ["bodyslide:".Length..].Split(',')[0];
                var projectPath = Path.Combine(result.OutputDirectory, "CalienteTools", "BodySlide", "SliderSets", projectName + ".osp");
                var project = System.Xml.Linq.XDocument.Load(projectPath);
                foreach (var set in project.Descendants("SliderSet"))
                {
                    var buildPath = set.Element("OutputPath")!.Value.Replace('\\', Path.DirectorySeparatorChar);
                    foreach (var mesh in set.Elements("OutputFile"))
                        Assert.True(File.Exists(Path.Combine(result.OutputDirectory, buildPath, mesh.Value)),
                            $"BodySlide build target was not staged: {buildPath}{mesh.Value}");
                }
            }
            Assert.True(mappings.Count > 1);
            var plugins = Directory.GetFiles(output, "*_patched.esp", SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(plugins);
            var content = string.Join("\n", await Task.WhenAll(plugins.Select(async plugin =>
                System.Text.Encoding.ASCII.GetString(await File.ReadAllBytesAsync(plugin)).Replace('\\', '/'))));
            Assert.All(mappings, mapping => Assert.Contains(mapping, content, StringComparison.OrdinalIgnoreCase));
            var overrides = Directory.GetFiles(output, "*_SlidesmithPatch.esp", SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(overrides);
            var overrideContent = string.Join("\n", await Task.WhenAll(overrides.Select(async plugin =>
                System.Text.Encoding.ASCII.GetString(await File.ReadAllBytesAsync(plugin)).Replace('\\', '/'))));
            Assert.All(mappings, mapping => Assert.Contains(mapping, overrideContent, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BodySlidePluginTargetsNormalizeOptionalMeshesPrefix()
    {
        var source = Path.Combine(Path.GetTempPath(), "meshes", "armor", "example", "cuirass_0.nif");
        foreach (var reference in new[] { "meshes/armor/example/cuirass_0.nif", "armor/example/cuirass_0.nif" })
        {
            var analysis = new PluginAnalysisResult(["Example.esp"],
                [new PluginArmorAddon("Example.esp", [reference])], string.Empty);
            var targets = LocalExportService.BuildBodySlideMeshOutputPaths(analysis, [source], "CBBE");
            Assert.Equal(@"meshes\slidesmith\cbbe\armor\example\", targets[source]);
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

    private static string GetFixtureDirectory(string name, [System.Runtime.CompilerServices.CallerFilePath] string currentFilePath = "") =>
        Path.Combine(Path.GetDirectoryName(currentFilePath)!, "Fixtures", name);
}
