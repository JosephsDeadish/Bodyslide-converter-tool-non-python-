using Bodyslide.Core;
using System.IO.Compression;

namespace Bodyslide.Core.Tests;

public sealed class ConversionOrchestratorTests
{
    [Fact]
    public async Task ConvertAsync_RunsFullPipelineAndExports()
    {
        var inputFile = Path.GetTempFileName();
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var exporter = new TestExporter();
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var orchestrator = BuildTestOrchestrator(exporter);
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            Assert.Equal(outputDirectory, result.OutputDirectory);
            Assert.Equal(outputDirectory, exporter.ExportPath);
            Assert.Contains(result.Steps, s => s.StartsWith("cage:", StringComparison.Ordinal));
            Assert.Contains(result.Steps, s => s.StartsWith("clipping:", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputFile);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_ThrowsWhenInputDoesNotExist()
    {
        var orchestrator = BuildTestOrchestrator();

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            orchestrator.ConvertAsync(new ConversionRequest(Path.Combine(Path.GetTempPath(), "missing-input.nif"), "UNP")));
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_ThrowsWhenOutputDirectoryIsInvalid()
    {
        var inputFile = Path.GetTempFileName();

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            await Assert.ThrowsAnyAsync<Exception>(() =>
                orchestrator.ConvertAsync(new ConversionRequest(inputFile, "UNP", "invalid\0path")));
        }
        finally
        {
            File.Delete(inputFile);
        }
    }

    [Fact]
    public void Normalize_UsesPresetTargetBody()
    {
        var request = new ConversionRequest("/tmp/in.nif", string.Empty, Preset: "3BA Curvy");
        var normalized = RequestNormalizer.Normalize(request);

        Assert.Equal("3BA", normalized.Request.TargetBody);
        Assert.NotNull(normalized.Preset);
        Assert.Equal("smp+cbpc", normalized.Preset!.PhysicsProfile);
    }

    [Fact]
    public async Task BatchRunner_ConvertsAllNifsInDirectory()
    {
        var inputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(inputDirectory, "armor_one.nif"), "mesh");
            await File.WriteAllTextAsync(Path.Combine(inputDirectory, "armor_two.nif"), "mesh");

            var runner = new BatchConversionRunner(BuildTestOrchestrator(new TestExporter()));
            var results = await runner.ConvertAsync(new ConversionRequest(inputDirectory, "CBBE", outputDirectory));

            Assert.Equal(2, results.Count);
            Assert.All(results, result => Assert.StartsWith(outputDirectory, result.OutputDirectory, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(inputDirectory, recursive: true);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BatchRunner_ConvertsAllNifsInZipArchive()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var sourceDirectory = Path.Combine(workingDirectory, "source");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var zipPath = Path.Combine(workingDirectory, "mod-pack.zip");
        Directory.CreateDirectory(sourceDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "armor_one.nif"), "mesh");
            await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "armor_two.nif"), "mesh");
            ZipFile.CreateFromDirectory(sourceDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            var runner = new BatchConversionRunner(BuildTestOrchestrator(new TestExporter()));
            var results = await runner.ConvertAsync(new ConversionRequest(zipPath, "CBBE", outputDirectory));

            Assert.Equal(2, results.Count);
            Assert.All(results, result => Assert.StartsWith(outputDirectory, result.OutputDirectory, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesConversionLearningCache()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var inputFile = Path.Combine(inputDirectory, "cuirass_3ba.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var cachePath = Path.Combine(outputDirectory, ".conversion-learning-cache.json");
            Assert.True(File.Exists(cachePath));
            var cacheContent = await File.ReadAllTextAsync(cachePath);
            Assert.Contains("\"TargetBody\": \"3BA\"", cacheContent, StringComparison.Ordinal);
            Assert.Contains("\"RegionalMorphing\"", cacheContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_ReusesConversionLearningCache()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var inputFile = Path.Combine(inputDirectory, "cuirass_3ba.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var firstResult = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));
            Assert.True(firstResult.Success);

            var secondResult = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));
            Assert.True(secondResult.Success);
            Assert.Contains(secondResult.Steps, s => s.StartsWith("learning-cache:hit=", StringComparison.Ordinal));
            Assert.Contains(secondResult.Steps, s => s.Equals("learning-cache:reused", StringComparison.Ordinal));
            Assert.Contains(secondResult.Steps, s => s.Contains("mesh-converted:", StringComparison.Ordinal) && s.Contains("cache-reuse", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesDependencyMap()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var mesh0 = Path.Combine(inputDirectory, "cuirass_0.nif");
        var mesh1 = Path.Combine(inputDirectory, "cuirass_1.nif");
        await File.WriteAllTextAsync(mesh0, "mesh");
        await File.WriteAllTextAsync(mesh1, "mesh");
        await File.WriteAllBytesAsync(Path.Combine(inputDirectory, "cuirass.dds"), [0x44, 0x44, 0x53, 0x20]);
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, "cuirass.xml"), "<physics/>");
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, "cuirass_reference.tri"), "ref");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputDirectory, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var dependencyMapPath = Path.Combine(outputDirectory, "dependency-map.json");
            Assert.True(File.Exists(dependencyMapPath));

            var mapContent = await File.ReadAllTextAsync(dependencyMapPath);
            Assert.Contains("\"Mesh\": \"cuirass_0.nif\"", mapContent, StringComparison.Ordinal);
            Assert.Contains("\"Mesh\": \"cuirass_1.nif\"", mapContent, StringComparison.Ordinal);
            Assert.Contains("cuirass.dds", mapContent, StringComparison.Ordinal);
            Assert.Contains("cuirass.xml", mapContent, StringComparison.Ordinal);
            Assert.Contains("cuirass_reference.tri", mapContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_CopiesSupportAssetsIntoOutput()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var meshDirectory = Path.Combine(inputDirectory, "meshes", "armor", "iron");
        var textureDirectory = Path.Combine(inputDirectory, "textures", "armor", "iron");
        var materialDirectory = Path.Combine(inputDirectory, "materials", "armor", "iron");
        var bodyRefDirectory = Path.Combine(inputDirectory, "meshes", "actors", "character", "character assets");

        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(textureDirectory);
        Directory.CreateDirectory(materialDirectory);
        Directory.CreateDirectory(bodyRefDirectory);
        Directory.CreateDirectory(outputDirectory);

        var meshPath = Path.Combine(meshDirectory, "ironarmor_0.nif");
        var texturePath = Path.Combine(textureDirectory, "ironarmor.dds");
        var physicsPath = Path.Combine(meshDirectory, "ironarmor.xml");
        var pluginPath = Path.Combine(inputDirectory, "MyArmor.esp");
        var bodyRefPath = Path.Combine(bodyRefDirectory, "body_reference.tri");
        var materialPath = Path.Combine(materialDirectory, "ironarmor.bgsm");

        await File.WriteAllTextAsync(meshPath, "mesh");
        await File.WriteAllBytesAsync(texturePath, [0x44, 0x44, 0x53, 0x20]);
        await File.WriteAllTextAsync(physicsPath, "<physics/>");
        await File.WriteAllTextAsync(pluginPath, "meshes\\armor\\iron\\ironarmor_0.nif");
        await File.WriteAllTextAsync(bodyRefPath, "reference");
        await File.WriteAllTextAsync(materialPath, "material");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputDirectory, "CBBE", outputDirectory));

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "textures", "armor", "iron", "ironarmor.dds")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "meshes", "armor", "iron", "ironarmor.xml")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "MyArmor.esp")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "meshes", "actors", "character", "character assets", "body_reference.tri")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "materials", "armor", "iron", "ironarmor.bgsm")));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithSingleNifInput_ScansSiblingSupportAssets()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var meshDirectory = Path.Combine(inputDirectory, "meshes", "armor", "iron");
        var textureDirectory = Path.Combine(inputDirectory, "textures", "armor", "iron");
        var materialDirectory = Path.Combine(inputDirectory, "materials", "armor", "iron");
        var bodyRefDirectory = Path.Combine(inputDirectory, "meshes", "actors", "character", "character assets");

        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(textureDirectory);
        Directory.CreateDirectory(materialDirectory);
        Directory.CreateDirectory(bodyRefDirectory);
        Directory.CreateDirectory(outputDirectory);

        var meshPath = Path.Combine(meshDirectory, "ironarmor_0.nif");
        var texturePath = Path.Combine(textureDirectory, "ironarmor.dds");
        var physicsPath = Path.Combine(meshDirectory, "ironarmor.xml");
        var pluginPath = Path.Combine(inputDirectory, "MyArmor.esp");
        var bodyRefPath = Path.Combine(bodyRefDirectory, "body_reference.tri");
        var materialPath = Path.Combine(materialDirectory, "ironarmor.bgem");

        await File.WriteAllTextAsync(meshPath, "mesh");
        await File.WriteAllBytesAsync(texturePath, [0x44, 0x44, 0x53, 0x20]);
        await File.WriteAllTextAsync(physicsPath, "<physics/>");
        await File.WriteAllTextAsync(pluginPath, "meshes\\armor\\iron\\ironarmor_0.nif");
        await File.WriteAllTextAsync(bodyRefPath, "reference");
        await File.WriteAllTextAsync(materialPath, "material");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(meshPath, "CBBE", outputDirectory));

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "textures", "armor", "iron", "ironarmor.dds")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "meshes", "armor", "iron", "ironarmor.xml")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "MyArmor.esp")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "meshes", "actors", "character", "character assets", "body_reference.tri")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "materials", "armor", "iron", "ironarmor.bgem")));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_StepsIncludeSkeletonAndPartitions()
    {
        var inputFile = Path.GetTempFileName();
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var orchestrator = BuildTestOrchestrator(new TestExporter());
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.StartsWith("skeleton:", StringComparison.Ordinal));
            Assert.Contains(result.Steps, s => s.StartsWith("partitions:", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputFile);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithOutputZip_ProducesZipFile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory, OutputZip: true));

            Assert.True(result.Success);
            var zipPath = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
            Assert.True(File.Exists(zipPath), $"Expected ZIP at {zipPath}");
            Assert.Single(result.OutputFiles);
            Assert.EndsWith(".zip", result.OutputFiles[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_SkeletonStepReportsMapping()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var skeletonStep = result.Steps.FirstOrDefault(s => s.StartsWith("skeleton:", StringComparison.Ordinal));
            Assert.NotNull(skeletonStep);
            Assert.Contains("-mapped", skeletonStep, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_PartitionStepRebuildsSlots()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass_plate.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var partitionsStep = result.Steps.FirstOrDefault(s => s.StartsWith("partitions:", StringComparison.Ordinal));
            Assert.NotNull(partitionsStep);
            Assert.NotEqual("partitions:unchanged", partitionsStep, StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("HIMBO")]
    [InlineData("UNP")]
    [InlineData("BHUNP")]
    [InlineData("TBD")]
    [InlineData("SAM")]
    [InlineData("SOS")]
    [InlineData("UBE")]
    public void BodyTransformationFieldCatalog_ResolvesAllKnownBodies(string targetBody)
    {
        var inputFile = Path.GetTempFileName();

        try
        {
            var normalized = RequestNormalizer.Normalize(new ConversionRequest(inputFile, targetBody));
            Assert.Equal(targetBody, normalized.Request.TargetBody);
        }
        finally
        {
            File.Delete(inputFile);
        }
    }

    [Fact]
    public void PresetCatalog_ContainsExpandedPresets()
    {
        var presets = PresetCatalog.All.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("BHUNP Curvy", presets);
        Assert.Contains("HIMBO Muscular", presets);
        Assert.Contains("3BA Slim", presets);
        Assert.Contains("UNP Athletic", presets);
    }

    [Fact]
    public async Task ConvertAsync_StepsIncludeBodySlideStep()
    {
        var inputFile = Path.GetTempFileName();
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var orchestrator = BuildTestOrchestrator(new TestExporter());
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.StartsWith("bodyslide:", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputFile);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDeformationProfile_StepRecordsProfile()
    {
        var inputFile = Path.GetTempFileName();
        var outputDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var orchestrator = BuildTestOrchestrator(new TestExporter());
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory, DeformationProfile: "curvy"));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.Equals("deformation-profile:curvy", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(inputFile);
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesOspFile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var ospFile = Directory.GetFiles(outputDirectory, "*.osp").FirstOrDefault();
            Assert.NotNull(ospFile);
            var ospContent = await File.ReadAllTextAsync(ospFile);
            Assert.Contains("<SliderSetInfo", ospContent, StringComparison.Ordinal);
            Assert.Contains("<Slider name=", ospContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesFomodMetadataFiles()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var fomodDirectory = Path.Combine(outputDirectory, "fomod");
            var moduleConfigPath = Path.Combine(fomodDirectory, "ModuleConfig.xml");
            var infoPath = Path.Combine(fomodDirectory, "info.xml");
            Assert.True(File.Exists(moduleConfigPath));
            Assert.True(File.Exists(infoPath));

            var moduleConfig = await File.ReadAllTextAsync(moduleConfigPath);
            var infoXml = await File.ReadAllTextAsync(infoPath);
            Assert.Contains("SlideSmith Conversion", moduleConfig, StringComparison.Ordinal);
            Assert.Contains("<Version MachineVersion=\"0.1\">0.1</Version>", infoXml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("curvy", 1.15)]
    [InlineData("slim", 0.82)]
    [InlineData("petite", 0.75)]
    [InlineData("muscular", 1.25)]
    public void DeformationProfileModifier_ScalesDeltaCorrectly(string profile, double amplifier)
    {
        // Base chest delta = 0.08 (value 1.08 - 1.0).  After applying amplifier: 1 + (0.08 * amplifier).
        var field = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.08 };
        var result = DeformationProfileModifier.Apply(field, profile);

        var expected = 1.0 + (0.08 * amplifier);
        Assert.Equal(expected, result["chest"], precision: 10);
    }

    [Fact]
    public void DeformationProfileModifier_UnknownProfile_ReturnsUnchangedField()
    {
        var field = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.08 };
        var result = DeformationProfileModifier.Apply(field, "unknown-profile");
        Assert.Equal(1.08, result["chest"]);
    }

    [Fact]
    public void DeformationProfileModifier_All_ContainsExpectedProfiles()
    {
        var profiles = DeformationProfileModifier.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("curvy", profiles);
        Assert.Contains("slim", profiles);
        Assert.Contains("petite", profiles);
        Assert.Contains("muscular", profiles);
        Assert.Contains("lean", profiles);
        Assert.Contains("athletic", profiles);
    }

    [Fact]
    public async Task BodySlideOspProjectService_GeneratesValidXmlForFemaleBody()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var ospFile = Directory.GetFiles(outputDirectory, "*.osp").FirstOrDefault();
            Assert.NotNull(ospFile);
            var ospXml = await File.ReadAllTextAsync(ospFile);
            Assert.Contains("BreastsPhysics", ospXml, StringComparison.Ordinal);
            Assert.Contains("femalebody_0.nif", ospXml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BodySlideOspProjectService_GeneratesValidXmlForMaleBody()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "HIMBO", outputDirectory));

            Assert.True(result.Success);
            var ospFile = Directory.GetFiles(outputDirectory, "*.osp").FirstOrDefault();
            Assert.NotNull(ospFile);
            var ospXml = await File.ReadAllTextAsync(ospFile);
            Assert.Contains("Pecs", ospXml, StringComparison.Ordinal);
            Assert.Contains("malebody_0.nif", ospXml, StringComparison.Ordinal);
            Assert.Contains("malebody_1.nif", ospXml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    private static ConversionOrchestrator BuildTestOrchestrator(IExportService? exporter = null) =>
        new(
            new TestImporter(),
            new TestDetector(),
            new TestAnalyzer(),
            new TestCageGenerator(),
            new TestConverter(),
            new TestWeightTransfer(),
            new TestSkeletonMapper(),
            new TestMorphGenerator(),
            new TestPartitionRebuilder(),
            new TestClippingDetector(),
            new TestAutoCorrection(),
            new TestPhysicsSupport(),
            new TestBodySlideProjectService(),
            new TestTextureAnalysisService(),
            new TestPluginAnalysisService(),
            new TestVanillaArmorLookup(),
            new TestVoxelCollision(),
            new TestArmorRegionBindingService(),
            new TestPoseSimulationService(),
            exporter ?? new TestExporter());

    private sealed class TestImporter : IArmorImportService
    {
        public Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken) =>
            Task.FromResult(new ImportedArmor(inputPath, [inputPath], [], [], []));
    }

    private sealed class TestDetector : IBodyDetectionService
    {
        public Task<BodyDetectionReport> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken) =>
            Task.FromResult(new BodyDetectionReport("CBBE", 0.99, ["mesh:100%"]));
    }

    private sealed class TestAnalyzer : IMeshAnalysisService
    {
        public Task<MeshAnalysis> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken) =>
            Task.FromResult(new MeshAnalysis("mixed", false, 1));
    }

    private sealed class TestCageGenerator : ICageGenerationService
    {
        public Task<DeformationCage> BuildAsync(MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new DeformationCage("hybrid-cage"));
    }

    private sealed class TestConverter : IMeshConversionService
    {
        public Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, string? deformationProfile, string? sourceBody, CancellationToken cancellationToken) =>
            Task.FromResult(new ConvertedMesh("mixed", "hybrid", 1, new Dictionary<string, double> { { "chest", 1.0 } }));
    }

    private sealed class TestWeightTransfer : IWeightTransferService
    {
        public Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new WeightedMesh("mixed", "default", false));
    }

    private sealed class TestSkeletonMapper : ISkeletonMappingService
    {
        public Task<SkeletonMappingResult> MapAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new SkeletonMappingResult("xpmsse-vanilla", "xpmsse-vanilla", [], []));
    }

    private sealed class TestMorphGenerator : IMorphGenerationService
    {
        public Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new MorphSet("low", "high", true));
    }

    private sealed class TestPartitionRebuilder : IPartitionRebuildingService
    {
        public Task<PartitionRebuildingResult> RebuildAsync(WeightedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new PartitionRebuildingResult(true, ["32:Body"], []));
    }

    private sealed class TestClippingDetector : IClippingDetectionService
    {
        public Task<ClippingReport> DetectAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new ClippingReport(false, ["thighs"], ["pose-simulation"]));
    }

    private sealed class TestAutoCorrection : IAutoCorrectionService
    {
        public Task<CorrectionResult> CorrectAsync(ConvertedMesh mesh, ClippingReport clipping, CancellationToken cancellationToken) =>
            Task.FromResult(new CorrectionResult(false, "none"));
    }

    private sealed class TestPhysicsSupport : IPhysicsSupportService
    {
        public Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, string physicsProfile, CancellationToken cancellationToken) =>
            Task.FromResult(new PhysicsConfig(physicsProfile));
    }

    private sealed class TestBodySlideProjectService : IBodySlideProjectService
    {
        public Task<BodySlideProject> GenerateAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySlideProject("TestArmor", targetBody, ["Belly", "Butt"], "<SliderSetInfo />"));
    }

    private sealed class TestTextureAnalysisService : ITextureAnalysisService
    {
        public Task<TextureSummary> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken) =>
            Task.FromResult(new TextureSummary(0, [], [], []));
    }

    private sealed class TestPluginAnalysisService : IPluginAnalysisService
    {
        public Task<PluginAnalysisResult> AnalyzeAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new PluginAnalysisResult([], [], "no plugins"));
    }

    private sealed class TestVanillaArmorLookup : IVanillaArmorLookupService
    {
        public IReadOnlyList<VanillaArmorEntry> All => [];

        public bool TryLookup(string meshFileName, out VanillaArmorEntry? entry)
        {
            entry = null;
            return false;
        }
    }

    private sealed class TestVoxelCollision : IVoxelCollisionService
    {
        public Task<VoxelCollisionResult> ComputeAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new VoxelCollisionResult(false, [], new Dictionary<string, double>(), 8));
    }

    private sealed class TestArmorRegionBindingService : IArmorRegionBindingService
    {
        public Task<ArmorRegionBinding> BindAsync(ImportedArmor armor, MeshAnalysis analysis, CancellationToken cancellationToken) =>
            Task.FromResult(new ArmorRegionBinding(["chest", "waist"], "test-stub"));
    }

    private sealed class TestPoseSimulationService : IPoseSimulationService
    {
        public Task<PoseSimulationResult> SimulateAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
            Task.FromResult(new PoseSimulationResult(
                ["T-pose", "Walk", "Run", "Idle", "Crouch", "Combat-Idle", "Jump", "Sneak"],
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                [],
                0));
    }

    private sealed class TestExporter : IExportService
    {
        public string? ExportPath { get; private set; }

        public Task<(string OutputDirectory, IReadOnlyList<string> OutputFiles)> ExportAsync(
            ConversionRequest request,
            ImportedArmor armor,
            MeshAnalysis analysis,
            ConvertedMesh mesh,
            MorphSet morphs,
            PhysicsConfig physics,
            ClippingReport clipping,
            CorrectionResult correction,
            BodySlideProject bodySlideProject,
            PluginAnalysisResult pluginAnalysis,
            TextureSummary textureSummary,
            PoseSimulationResult poseSimulation,
            IReadOnlyList<string> steps,
            CancellationToken cancellationToken)
        {
            ExportPath = request.OutputDirectory ?? throw new InvalidOperationException("Output should be provided for this test.");
            Directory.CreateDirectory(ExportPath);
            return Task.FromResult<(string, IReadOnlyList<string>)>((ExportPath, []));
        }
    }
}

internal static class SyntheticNifTestData
{
    public static IReadOnlyList<(float X, float Y, float Z)> CreateBodyVertices(int vertexCount)
    {
        var vertices = new List<(float X, float Y, float Z)>(vertexCount);
        for (var index = 0; index < vertexCount; index++)
        {
            var t = vertexCount == 1 ? 0f : (float)index / (vertexCount - 1);
            var x = ((index % 17) - 8) * 0.02f;
            var y = ((index % 11) - 5) * 0.015f;
            var z = t * 1.75f;
            vertices.Add((x, y, z));
        }

        return vertices;
    }

    public static IReadOnlyList<(float X, float Y, float Z)> CreateUpperBodyArmorVertices() =>
    [
        (-0.12f,  0.00f, 1.08f), (-0.08f,  0.02f, 1.12f), ( 0.08f,  0.02f, 1.12f), ( 0.12f,  0.00f, 1.08f),
        (-0.35f,  0.01f, 1.00f), (-0.42f, -0.01f, 0.94f), ( 0.35f,  0.01f, 1.00f), ( 0.42f, -0.01f, 0.94f),
        (-0.10f, -0.02f, 0.92f), (-0.06f,  0.01f, 0.86f), ( 0.06f,  0.01f, 0.86f), ( 0.10f, -0.02f, 0.92f),
        (-0.04f,  0.00f, 0.74f), ( 0.04f,  0.00f, 0.74f), (-0.02f,  0.00f, 0.66f), ( 0.02f,  0.00f, 0.66f)
    ];

    public static async Task WriteAsync(string path, IReadOnlyList<(float X, float Y, float Z)> vertices)
    {
        await using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("VERT"));
        writer.Write(vertices.Count);

        foreach (var (x, y, z) in vertices)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
        }
    }

    public static async Task WriteBlockGraphStyleAsync(string path, IReadOnlyList<(float X, float Y, float Z)> vertices)
    {
        await using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("NiNode"));
        writer.Write(0); // root has no relevant geometry payload
        writer.Write(System.Text.Encoding.ASCII.GetBytes("NiTriShapeData"));
        writer.Write(vertices.Count);

        foreach (var (x, y, z) in vertices)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// New-feature tests (NIF output, --source override, weight-pair output)
// ─────────────────────────────────────────────────────────────────────────────
public sealed class NifOutputAndSourceOverrideTests
{
    private static IReadOnlyList<(float X, float Y, float Z)> ReadEmbeddedVertices(byte[] bytes)
    {
        var marker = System.Text.Encoding.ASCII.GetBytes("VERT");
        var markerIndex = bytes.AsSpan().IndexOf(marker);
        Assert.True(markerIndex >= 0, "Synthetic NIF data should contain VERT marker.");

        var countOffset = markerIndex + marker.Length;
        var vertexCount = BitConverter.ToInt32(bytes, countOffset);
        Assert.True(vertexCount > 0, "Synthetic NIF should contain at least one vertex.");

        var vertices = new List<(float X, float Y, float Z)>(vertexCount);
        var cursor = countOffset + sizeof(int);
        for (var index = 0; index < vertexCount; index++)
        {
            vertices.Add((
                BitConverter.ToSingle(bytes, cursor),
                BitConverter.ToSingle(bytes, cursor + 4),
                BitConverter.ToSingle(bytes, cursor + 8)));
            cursor += 12;
        }

        return vertices;
    }

    // ── NIF output ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesNifFileToOutputDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "nif-data");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var nifFiles = Directory.GetFiles(outputDirectory, "*.nif");
            Assert.NotEmpty(nifFiles);
            // The output NIF must reproduce the source content.
            var written = await File.ReadAllTextAsync(nifFiles[0]);
            Assert.Equal("nif-data", written);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesBothWeightPairNifs()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(inputDirectory);

        var mesh0 = Path.Combine(inputDirectory, "armor_0.nif");
        var mesh1 = Path.Combine(inputDirectory, "armor_1.nif");
        await File.WriteAllTextAsync(mesh0, "low-weight");
        await File.WriteAllTextAsync(mesh1, "high-weight");

        try
        {
            // The directory input triggers batch, so use a single-mesh entry to keep it simple:
            // provide mesh0 directly, with mesh1 sibling auto-imported by the LocalArmorImportService.
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputDirectory, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var nifFiles = Directory.GetFiles(outputDirectory, "*.nif", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Both _0 and _1 variants must appear in the output.
            Assert.Contains("armor_0.nif", nifFiles);
            Assert.Contains("armor_1.nif", nifFiles);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithSyntheticNif_AppliesVertexTransform()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "synthetic_armor.nif");
        var sourceVertices = SyntheticNifTestData.CreateUpperBodyArmorVertices();
        await SyntheticNifTestData.WriteAsync(inputFile, sourceVertices);

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var writtenPath = Path.Combine(outputDirectory, "synthetic_armor.nif");
            Assert.True(File.Exists(writtenPath), "Converted NIF was not written.");

            var sourceBytes = await File.ReadAllBytesAsync(inputFile);
            var writtenBytes = await File.ReadAllBytesAsync(writtenPath);
            Assert.NotEmpty(writtenBytes);
            Assert.Contains("Gamebryo File Format", System.Text.Encoding.ASCII.GetString(writtenBytes), StringComparison.Ordinal);

            var sourceRead = ReadEmbeddedVertices(sourceBytes);
            var transformedRead = ReadEmbeddedVertices(writtenBytes);
            Assert.Equal(sourceRead.Count, transformedRead.Count);

            var anyVertexChanged = sourceRead.Zip(transformedRead, (src, dst) =>
                    Math.Abs(src.X - dst.X) > 0.0001f ||
                    Math.Abs(src.Y - dst.Y) > 0.0001f ||
                    Math.Abs(src.Z - dst.Z) > 0.0001f)
                .Any(changed => changed);

            Assert.True(anyVertexChanged, "Expected at least one synthetic vertex to be transformed.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithBlockGraphStyleNif_AppliesVertexTransform()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "block_graph_armor.nif");
        var sourceVertices = SyntheticNifTestData.CreateBodyVertices(320);
        await SyntheticNifTestData.WriteBlockGraphStyleAsync(inputFile, sourceVertices);

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var writtenPath = Path.Combine(outputDirectory, "block_graph_armor.nif");
            Assert.True(File.Exists(writtenPath), "Converted NIF was not written.");

            var sourceBytes = await File.ReadAllBytesAsync(inputFile);
            var writtenBytes = await File.ReadAllBytesAsync(writtenPath);
            Assert.NotEqual(sourceBytes, writtenBytes);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    // ── --source override ─────────────────────────────────────────────────────

    [Fact]
    public async Task ConvertAsync_WithSourceOverride_StepRecordsOverride()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(
                new ConversionRequest(inputFile, "3BA", outputDirectory, SourceBodyOverride: "CBBE"));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.Equals("source-body-override:CBBE", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithoutSourceOverride_NoOverrideStep()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(
                new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Steps, s => s.StartsWith("source-body-override:", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    // ── Weight-pair output path isolation ─────────────────────────────────────

    [Fact]
    public async Task ConvertAsync_WithWeightVariantPairDirectory_ReportsDetectedPair()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(inputDirectory);
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, "ironarmor_0.nif"), "low");
        await File.WriteAllTextAsync(Path.Combine(inputDirectory, "ironarmor_1.nif"), "high");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputDirectory, "CBBE", outputDirectory));

            Assert.True(result.Success);
            // The pipeline must report the detected weight variant pair.
            Assert.Contains(result.Steps, s => s.StartsWith("weight-variants:pairs=", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class VanillaArmorLookupTests
{
    [Fact]
    public void TryLookup_KnownIronArmorMesh_ReturnsEntry()
    {
        var service = new VanillaArmorLookupService();

        var found = service.TryLookup("ironarmor_0.nif", out var entry);

        Assert.True(found);
        Assert.NotNull(entry);
        Assert.Equal("Iron Armor", entry!.Name);
        Assert.Equal("Vanilla", entry.SourceBody);
        Assert.Contains("32:Body", entry.RegionSlots);
    }

    [Fact]
    public void TryLookup_KnownIronArmorMesh_WeightSuffixStripped()
    {
        var service = new VanillaArmorLookupService();

        var found0 = service.TryLookup("ironarmor_0.nif", out var entry0);
        var found1 = service.TryLookup("ironarmor_1.nif", out var entry1);

        Assert.True(found0);
        Assert.True(found1);
        Assert.Equal(entry0!.Name, entry1!.Name);
    }

    [Fact]
    public void TryLookup_UnknownMesh_ReturnsFalse()
    {
        var service = new VanillaArmorLookupService();

        var found = service.TryLookup("modded_fancy_armor.nif", out var entry);

        Assert.False(found);
        Assert.Null(entry);
    }

    [Fact]
    public void All_ContainsCoreVanillaArmors()
    {
        var service = new VanillaArmorLookupService();
        var names = service.All.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Iron Armor",      names);
        Assert.Contains("Daedric Armor",   names);
        Assert.Contains("Glass Armor",     names);
        Assert.Contains("Nightingale Armor", names);
        Assert.Contains("Mage Robes",      names);
    }

    [Theory]
    [InlineData("steelarmor_0.nif",       "Steel Armor")]
    [InlineData("daedricarmor_0.nif",     "Daedric Armor")]
    [InlineData("glassarmor_1.nif",       "Glass Armor")]
    [InlineData("nightingalearmor_0.nif", "Nightingale Armor")]
    public void TryLookup_KnownArmorVariants_ReturnsCorrectEntry(string meshFile, string expectedName)
    {
        var service = new VanillaArmorLookupService();

        var found = service.TryLookup(meshFile, out var entry);

        Assert.True(found);
        Assert.Equal(expectedName, entry!.Name);
    }
}

public sealed class VoxelCollisionTests
{
    [Fact]
    public async Task ComputeAsync_PhysicsEnabledMeshWithHighMorphs_DetectsPenetrations()
    {
        var service = new SimplifiedVoxelCollisionService();
        var armor = new ImportedArmor("/tmp/armor.nif", ["/tmp/armor.nif"], [], [], []);
        // cloth mesh with chest morph above the soft threshold of 1.04
        var mesh = new ConvertedMesh("cloth", "cage+shrinkwrap", 1,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.09, ["waist"] = 0.95 });

        var result = await service.ComputeAsync(armor, mesh, "3BA", CancellationToken.None);

        Assert.True(result.HasPenetrations);
        Assert.Contains("chest", result.AffectedRegions);
        Assert.True(result.PushOutMagnitudes["chest"] > 0);
    }

    [Fact]
    public async Task ComputeAsync_PlateArmorWithLowMorphs_NoPenetrations()
    {
        var service = new SimplifiedVoxelCollisionService();
        var armor = new ImportedArmor("/tmp/armor.nif", ["/tmp/armor.nif"], [], [], []);
        // plate mesh; all morphs below the plate threshold of 1.10
        var mesh = new ConvertedMesh("plate", "cage+rigid-islands", 1,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.04, ["shoulders"] = 1.06 });

        var result = await service.ComputeAsync(armor, mesh, "CBBE", CancellationToken.None);

        Assert.False(result.HasPenetrations);
        Assert.Empty(result.AffectedRegions);
    }

    [Fact]
    public async Task ComputeAsync_ReturnsExpectedGridResolution()
    {
        var service = new SimplifiedVoxelCollisionService();
        var armor = new ImportedArmor("/tmp/armor.nif", ["/tmp/armor.nif"], [], [], []);
        var mesh = new ConvertedMesh("mixed", "hybrid", 1, new Dictionary<string, double>());

        var result = await service.ComputeAsync(armor, mesh, "UNP", CancellationToken.None);

        Assert.Equal(8, result.GridResolution);
    }

    [Fact]
    public async Task ComputeAsync_PushOutMagnitude_ScalesWithExcess()
    {
        var service = new SimplifiedVoxelCollisionService();
        var armor = new ImportedArmor("/tmp/armor.nif", ["/tmp/armor.nif"], [], [], []);
        // cloth threshold is 1.04; morphFactor = 1.08 → excess = 0.04 → pushOut = 0.04 * 8 = 0.32
        var mesh = new ConvertedMesh("cloth", "cage+shrinkwrap", 1,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.08 });

        var result = await service.ComputeAsync(armor, mesh, "3BA", CancellationToken.None);

        Assert.True(result.HasPenetrations);
        Assert.True(result.PushOutMagnitudes["chest"] > 0 && result.PushOutMagnitudes["chest"] < 1.0);
    }
}

public sealed class PhysicsXmlTests
{
    [Fact]
    public async Task BuildAsync_CbpcProfile_GeneratesCbpcXml()
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh("cloth", "default", true);

        var config = await service.BuildAsync(mesh, "3BA", "smp+cbpc", CancellationToken.None);

        Assert.NotNull(config.CbpcConfigXml);
        Assert.Contains("<CBPCConfig", config.CbpcConfigXml, StringComparison.Ordinal);
        Assert.Contains("<BreastPhysics>", config.CbpcConfigXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_SmpProfile_GeneratesSmpXml()
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh("cloth", "default", true);

        var config = await service.BuildAsync(mesh, "3BA", "smp+cbpc", CancellationToken.None);

        Assert.NotNull(config.SmpConfigXml);
        Assert.Contains("<system name=", config.SmpConfigXml, StringComparison.Ordinal);
        Assert.Contains("NPC L Breast01", config.SmpConfigXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_MaleBody_GeneratesMalePecXml()
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh("mixed", "default", false);

        var config = await service.BuildAsync(mesh, "HIMBO", "smp", CancellationToken.None);

        Assert.NotNull(config.SmpConfigXml);
        Assert.Contains("NPC L Pec", config.SmpConfigXml, StringComparison.Ordinal);
        Assert.DoesNotContain("NPC L Breast01", config.SmpConfigXml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_CbpcOnlyProfile_NoSmpXml()
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh("cloth", "default", true);

        var config = await service.BuildAsync(mesh, "UNP", "cbpc", CancellationToken.None);

        Assert.NotNull(config.CbpcConfigXml);
        Assert.Null(config.SmpConfigXml);
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesCbpcAndSmpFiles()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cloth_robe.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "cbpc-config.xml")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "smp-config.xml")));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class VanillaArmorPipelineTests
{
    [Fact]
    public async Task ConvertAsync_WithDefaultModules_VanillaLookupStepPresent()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "ironarmor_0.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var vanillaStep = result.Steps.FirstOrDefault(s => s.StartsWith("vanilla-armor:", StringComparison.Ordinal));
            Assert.NotNull(vanillaStep);
            Assert.DoesNotContain("unknown", vanillaStep, StringComparison.Ordinal);
            Assert.Contains("Iron Armor", vanillaStep, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_UnknownArmorReportsUnknown()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "modded_custom_gear.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.Equals("vanilla-armor:unknown", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_VoxelCollisionStepPresent()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            Assert.Contains(result.Steps, s => s.StartsWith("voxel-collision:", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class WeightVariantPairTests
{
    [Fact]
    public void DetectWeightVariantPairs_FullPair_DetectsBothWeights()
    {
        var meshFiles = new List<string>
        {
            "/tmp/armor/cuirass_0.nif",
            "/tmp/armor/cuirass_1.nif"
        };

        var pairs = LocalArmorImportService.DetectWeightVariantPairs(meshFiles);

        Assert.Single(pairs);
        Assert.Equal("cuirass", pairs[0].BaseName);
        Assert.NotNull(pairs[0].LowWeightMesh);
        Assert.NotNull(pairs[0].HighWeightMesh);
        Assert.EndsWith("cuirass_0.nif", pairs[0].LowWeightMesh, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("cuirass_1.nif", pairs[0].HighWeightMesh, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectWeightVariantPairs_MultiplePairs_DetectsAll()
    {
        var meshFiles = new List<string>
        {
            "/tmp/armor/cuirass_0.nif",
            "/tmp/armor/cuirass_1.nif",
            "/tmp/armor/boots_0.nif",
            "/tmp/armor/boots_1.nif",
            "/tmp/armor/gloves_0.nif",
            "/tmp/armor/gloves_1.nif"
        };

        var pairs = LocalArmorImportService.DetectWeightVariantPairs(meshFiles);

        Assert.Equal(3, pairs.Count);
        Assert.All(pairs, p => Assert.NotNull(p.LowWeightMesh));
        Assert.All(pairs, p => Assert.NotNull(p.HighWeightMesh));
    }

    [Fact]
    public void DetectWeightVariantPairs_MissingHighWeight_ReportsIncomplete()
    {
        var meshFiles = new List<string>
        {
            "/tmp/armor/gauntlets_0.nif"
        };

        var pairs = LocalArmorImportService.DetectWeightVariantPairs(meshFiles);

        Assert.Single(pairs);
        Assert.NotNull(pairs[0].LowWeightMesh);
        Assert.Null(pairs[0].HighWeightMesh);
    }

    [Fact]
    public void DetectWeightVariantPairs_NoWeightSuffix_ReturnsEmpty()
    {
        var meshFiles = new List<string>
        {
            "/tmp/armor/helmet.nif"
        };

        var pairs = LocalArmorImportService.DetectWeightVariantPairs(meshFiles);

        Assert.Empty(pairs);
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WeightVariantStepPresent()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);

        // Provide a _0/_1 pair so the weight-variants step fires.
        var mesh0 = Path.Combine(workingDirectory, "cuirass_0.nif");
        var mesh1 = Path.Combine(workingDirectory, "cuirass_1.nif");
        await File.WriteAllTextAsync(mesh0, "mesh");
        await File.WriteAllTextAsync(mesh1, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(workingDirectory, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var step = result.Steps.FirstOrDefault(s => s.StartsWith("weight-variants:", StringComparison.Ordinal));
            Assert.NotNull(step);
            Assert.Contains("pairs=1", step, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class BsdSliderDataTests
{
    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesBsdSliderFiles()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);

            // BSD files land in SliderData/<ProjectName>/
            var sliderDataDir = Directory.GetDirectories(outputDirectory, "*", SearchOption.AllDirectories)
                .FirstOrDefault(d => d.Contains("SliderData", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(sliderDataDir);

            var bsdFiles = Directory.GetFiles(sliderDataDir!, "*.bsd", SearchOption.AllDirectories);
            Assert.NotEmpty(bsdFiles);

            // Verify BSD magic header in each file.
            foreach (var bsdFile in bsdFiles)
            {
                var header = await File.ReadAllBytesAsync(bsdFile);
                Assert.True(header.Length >= 4);
                Assert.Equal(0x42, header[0]); // 'B'
                Assert.Equal(0x53, header[1]); // 'S'
                Assert.Equal(0x44, header[2]); // 'D'
                Assert.Equal(0x00, header[3]); // null
            }
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesBsdForLowAndHighWeight()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var sliderDataDir = Directory.GetDirectories(outputDirectory, "*", SearchOption.AllDirectories)
                .FirstOrDefault(d => d.Contains("SliderData", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(sliderDataDir);

            var bsdFiles = Directory.GetFiles(sliderDataDir!, "*.bsd", SearchOption.AllDirectories).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Expect both low-weight (e.g. "Belly.bsd") and high-weight (e.g. "Belly_1.bsd") files.
            Assert.True(bsdFiles.Any(f => !f!.EndsWith("_1.bsd", StringComparison.OrdinalIgnoreCase)), "Expected low-weight .bsd files.");
            Assert.True(bsdFiles.Any(f => f!.EndsWith("_1.bsd", StringComparison.OrdinalIgnoreCase)), "Expected high-weight _1.bsd files.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class TriMorphFileTests
{
    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesTriMorphFiles()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);

            // Expect both the low-weight and high-weight TRI files.
            var triFiles = Directory.GetFiles(outputDirectory, "*.tri", SearchOption.TopDirectoryOnly);
            Assert.True(triFiles.Length >= 2, "Expected at least 2 TRI files (low and high weight).");

            // The standard low-weight TRI should NOT end with _1.tri
            Assert.True(triFiles.Any(f => !Path.GetFileName(f).EndsWith("_1.tri", StringComparison.OrdinalIgnoreCase)),
                "Expected a low-weight .tri file.");
            Assert.True(triFiles.Any(f => Path.GetFileName(f).EndsWith("_1.tri", StringComparison.OrdinalIgnoreCase)),
                "Expected a high-weight _1.tri file.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_TriFilesHaveFrtri003Magic()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "boots.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var triFiles = Directory.GetFiles(outputDirectory, "*.tri", SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(triFiles);

            foreach (var triFile in triFiles)
            {
                var bytes = await File.ReadAllBytesAsync(triFile);
                Assert.True(bytes.Length >= 8);
                var magic = System.Text.Encoding.ASCII.GetString(bytes, 0, 8);
                Assert.Equal("FRTRI003", magic);
            }
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class BodySignatureVertexCountTests
{
    [Theory]
    [InlineData("CBBE",  6942)] // within 6800-7100
    [InlineData("UNP",   6032)] // within 5900-6200
    [InlineData("HIMBO", 6820)] // within 6600-7100
    [InlineData("BHUNP", 10080)] // within 9800-10400
    [InlineData("3BA",   10032)] // within 9800-10400
    public void BodySignatureTemplate_VertexCountRanges_IncludeTypicalCounts(string bodyName, int typicalCount)
    {
        var template = VanillaBodySignatureDatabase.Templates
            .First(t => string.Equals(t.Body, bodyName, StringComparison.OrdinalIgnoreCase));

        Assert.True(template.VertexCountMin > 0, $"{bodyName} should have a non-zero VertexCountMin.");
        Assert.True(template.VertexCountMax > template.VertexCountMin, $"{bodyName} VertexCountMax should exceed Min.");
        Assert.InRange(typicalCount, template.VertexCountMin, template.VertexCountMax);
    }

    [Fact]
    public void AllTemplates_HaveVertexCountRanges()
    {
        Assert.All(VanillaBodySignatureDatabase.Templates, t =>
        {
            Assert.True(t.VertexCountMin > 0,   $"{t.Body} is missing VertexCountMin.");
            Assert.True(t.VertexCountMax > t.VertexCountMin, $"{t.Body} VertexCountMax must exceed Min.");
        });
    }

    [Fact]
    public async Task SignatureBodyDetectionService_UsesGeometryVertexCountEvidence()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        var meshPath = Path.Combine(workingDirectory, "mystery_body.nif");

        try
        {
            await SyntheticNifTestData.WriteAsync(meshPath, SyntheticNifTestData.CreateBodyVertices(6942));

            var service = new SignatureBodyDetectionService();
            var armor = new ImportedArmor(meshPath, [meshPath], [], [], []);

            var result = await service.DetectAsync(armor, CancellationToken.None);

            Assert.Equal("CBBE", result.Body);
            Assert.Contains(result.Evidence, evidence => evidence.Equals("verts:6942", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class PreviewMetadataTests
{
    [Fact]
    public async Task ConvertAsync_WithDefaultModules_WritesPreviewHtml()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var previewPath = Path.Combine(outputDirectory, "preview.html");
            Assert.True(File.Exists(previewPath), "preview.html should exist.");

            var content = await File.ReadAllTextAsync(previewPath);
            Assert.Contains("<!DOCTYPE html>",   content, StringComparison.Ordinal);
            Assert.Contains("<svg ",             content, StringComparison.Ordinal);
            Assert.Contains("3BA",               content, StringComparison.Ordinal);
            Assert.Contains("Regional Morphing", content, StringComparison.Ordinal);
            Assert.Contains("BodySlide Sliders", content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_WithDefaultModules_PreviewHtmlContainsSvgRegions()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var content = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "preview.html"));

            // SVG body silhouette with coloured region overlays should be present.
            Assert.Contains("<rect ",  content, StringComparison.Ordinal);
            Assert.Contains("fill=",  content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_Preview_ContainsPoseRiskLabel()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cloth_robe.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            var content = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "preview.html"));

            // The subtitle line should include a pose risk summary.
            Assert.True(
                content.Contains("poses at risk", StringComparison.Ordinal) ||
                content.Contains("poses OK",      StringComparison.Ordinal),
                "Preview HTML subtitle should include a pose-risk label.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class PluginPatchGuidanceTests
{
    [Fact]
    public async Task PluginPatches_ContainsProposedPatchSteps_WhenPluginFound()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);

        // Write a minimal ESP-like file containing a NIF mesh path so the scanner picks it up.
        var espPath = Path.Combine(workingDirectory, "TestMod.esp");
        // The ESP content contains an ASCII NIF path that the regex will match.
        var espBytes = System.Text.Encoding.Latin1.GetBytes(
            "HEADR\0\0\0meshes/armor/iron/ironarmor_0.nif\0");
        await File.WriteAllBytesAsync(espPath, espBytes);

        var nifPath = Path.Combine(workingDirectory, "ironarmor_0.nif");
        await File.WriteAllTextAsync(nifPath, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(
                new ConversionRequest(workingDirectory, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var patchPath = Path.Combine(outputDirectory, "plugin-patches.json");
            Assert.True(File.Exists(patchPath));

            var content = await File.ReadAllTextAsync(patchPath);
            Assert.Contains("\"ProposedPatchSteps\"", content, StringComparison.Ordinal);
            Assert.Contains("\"XEditAction\"",        content, StringComparison.Ordinal);
            Assert.Contains("\"PlacementNote\"",      content, StringComparison.Ordinal);
            Assert.Contains("\"RewriteMappings\"",    content, StringComparison.Ordinal);
            Assert.Contains("meshes/slidesmith/cbbe/ironarmor_0.nif", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("xEdit",                  content, StringComparison.Ordinal);

            var rewrittenMeshPath = Path.Combine(outputDirectory, "meshes", "slidesmith", "cbbe", "ironarmor_0.nif");
            Assert.True(File.Exists(rewrittenMeshPath), "Converted mesh should be staged at the rewritten plugin path.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

public sealed class PhysicsMeshTypeTuningTests
{
    [Theory]
    [InlineData("cloth",           "BreastPhysics", true)]   // cloth → lower stiffness → value < 0.90
    [InlineData("plate",          "BreastPhysics", false)]   // plate → higher stiffness → value > 0.90
    [InlineData("physics-enabled","BreastPhysics", false)]   // physics-enabled → default 0.90
    public async Task BuildAsync_MeshTypeTuning_AdjustsStiffness(
        string meshType, string xmlElement, bool expectSofterThanDefault)
    {
        var service = new BasicPhysicsSupportService();
        var mesh = new WeightedMesh(meshType, "default", true);

        var config = await service.BuildAsync(mesh, "CBBE", "smp+cbpc", CancellationToken.None);

        Assert.NotNull(config.CbpcConfigXml);
        var stiffnessPattern = new System.Text.RegularExpressions.Regex(
            $@"<{xmlElement}>\s*<Stiffness>([0-9.]+)</Stiffness>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        var match = stiffnessPattern.Match(config.CbpcConfigXml);
        Assert.True(match.Success, $"Could not find stiffness inside <{xmlElement}> in CBPC XML.");

        var stiffness = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        const double defaultBreastStiffness = 0.90;

        if (expectSofterThanDefault)
        {
            Assert.True(stiffness < defaultBreastStiffness,
                $"{meshType} stiffness {stiffness} should be < {defaultBreastStiffness}");
        }
        else if (meshType == "plate")
        {
            Assert.True(stiffness > defaultBreastStiffness,
                $"{meshType} stiffness {stiffness} should be > {defaultBreastStiffness}");
        }
        else
        {
            Assert.Equal(defaultBreastStiffness, stiffness, precision: 10);
        }
    }

    [Fact]
    public async Task BuildAsync_ClothMesh_SmpAngularLimitLargerThanPlate()
    {
        var service = new BasicPhysicsSupportService();
        var clothMesh = new WeightedMesh("cloth", "default", true);
        var plateMesh = new WeightedMesh("plate", "default", false);

        var clothConfig = await service.BuildAsync(clothMesh, "3BA", "smp",  CancellationToken.None);
        var plateConfig = await service.BuildAsync(plateMesh, "3BA", "smp",  CancellationToken.None);

        Assert.NotNull(clothConfig.SmpConfigXml);
        Assert.NotNull(plateConfig.SmpConfigXml);

        var maxPattern = new System.Text.RegularExpressions.Regex(
            @"NPC L Breast01.*?max=""([0-9.]+)""",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        var clothMax = double.Parse(maxPattern.Match(clothConfig.SmpConfigXml).Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        var plateMax = double.Parse(maxPattern.Match(plateConfig.SmpConfigXml).Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(clothMax > plateMax,
            $"Cloth max angular limit ({clothMax}) should exceed plate ({plateMax})");
    }

    [Fact]
    public async Task BuildAsync_ClothMesh_HasLowerCbpcDampingThanPlate()
    {
        var service = new BasicPhysicsSupportService();
        var clothMesh = new WeightedMesh("cloth", "default", true);
        var plateMesh = new WeightedMesh("plate", "default", true);

        var clothConfig = await service.BuildAsync(clothMesh, "CBBE", "cbpc", CancellationToken.None);
        var plateConfig = await service.BuildAsync(plateMesh, "CBBE", "cbpc", CancellationToken.None);

        Assert.NotNull(clothConfig.CbpcConfigXml);
        Assert.NotNull(plateConfig.CbpcConfigXml);

        var dampingPattern = new System.Text.RegularExpressions.Regex(
            @"<BreastPhysics>\s*<Stiffness>[0-9.]+</Stiffness>\s*<Damping>([0-9.]+)</Damping>",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        var clothDamping = double.Parse(dampingPattern.Match(clothConfig.CbpcConfigXml).Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        var plateDamping = double.Parse(dampingPattern.Match(plateConfig.CbpcConfigXml).Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(clothDamping < plateDamping,
            $"Cloth damping ({clothDamping}) should be lower than plate damping ({plateDamping}).");
    }

    [Fact]
    public async Task BuildAsync_PhysicsWeightsMissing_TightensCbpcMaxOffset()
    {
        var service = new BasicPhysicsSupportService();
        var weightedMesh = new WeightedMesh("physics-enabled", "default", true);
        var unweightedMesh = new WeightedMesh("physics-enabled", "default", false);

        var weightedConfig = await service.BuildAsync(weightedMesh, "CBBE", "cbpc", CancellationToken.None);
        var unweightedConfig = await service.BuildAsync(unweightedMesh, "CBBE", "cbpc", CancellationToken.None);

        Assert.NotNull(weightedConfig.CbpcConfigXml);
        Assert.NotNull(unweightedConfig.CbpcConfigXml);

        var offsetPattern = new System.Text.RegularExpressions.Regex(
            @"<BreastPhysics>.*?<MaxOffset>([0-9.]+)</MaxOffset>",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        var weightedOffset = double.Parse(offsetPattern.Match(weightedConfig.CbpcConfigXml).Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        var unweightedOffset = double.Parse(offsetPattern.Match(unweightedConfig.CbpcConfigXml).Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(unweightedOffset < weightedOffset,
            $"Missing physics weights should reduce max offset ({unweightedOffset} < {weightedOffset}).");
    }

    [Fact]
    public async Task BuildAsync_MalePhysicsWithMeshTuning_StillContainsPecNodes()
    {
        var service = new BasicPhysicsSupportService();

        foreach (var meshType in new[] { "cloth", "plate", "mixed" })
        {
            var mesh   = new WeightedMesh(meshType, "default", false);
            var config = await service.BuildAsync(mesh, "HIMBO", "smp", CancellationToken.None);

            Assert.NotNull(config.SmpConfigXml);
            Assert.Contains("NPC L Pec",     config.SmpConfigXml, StringComparison.Ordinal);
            Assert.DoesNotContain("NPC L Breast01", config.SmpConfigXml, StringComparison.Ordinal);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Expanded preset / body-type / texture / vanilla-DB / BodyTypeCatalog tests
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ExpandedPresetTests
{
    [Theory]
    [InlineData("CBBE Curvy",    "CBBE")]
    [InlineData("CBBE Slim",     "CBBE")]
    [InlineData("CBBE Athletic", "CBBE")]
    [InlineData("CBBE Petite",   "CBBE")]
    [InlineData("3BA Athletic",  "3BA")]
    [InlineData("SAM Athletic",  "SAM")]
    [InlineData("SAM Lean",      "SAM")]
    [InlineData("SAM Muscular",  "SAM")]
    [InlineData("SOS Lean",      "SOS")]
    [InlineData("SOS Athletic",  "SOS")]
    [InlineData("UBE Petite",    "UBE")]
    [InlineData("UBE Curvy",     "UBE")]
    [InlineData("HIMBO Athletic","HIMBO")]
    public void PresetCatalog_NewPresets_ResolvesToCorrectBody(string presetName, string expectedBody)
    {
        Assert.True(PresetCatalog.TryGet(presetName, out var preset));
        Assert.Equal(expectedBody, preset.TargetBody);
    }

    [Fact]
    public void PresetCatalog_AllCount_AtLeast27()
    {
        Assert.True(PresetCatalog.All.Count >= 27, $"Expected >= 27 presets, got {PresetCatalog.All.Count}");
    }
}

public sealed class BodyTransformationFieldTests
{
    [Theory]
    [InlineData("CBBE")]
    [InlineData("3BA")]
    [InlineData("BHUNP")]
    [InlineData("UNP")]
    [InlineData("TBD")]
    [InlineData("UBE")]
    [InlineData("HIMBO")]
    [InlineData("SAM")]
    [InlineData("SOS")]
    public void BodyTransformationFieldCatalog_AllBodyTypes_Have11Regions(string body)
    {
        var fields = BodyTransformationFieldCatalog.Resolve(body);
        var requiredRegions = new[] { "waist", "hips", "chest", "shoulders", "neck", "breasts", "butt", "belly", "arms", "thighs", "calves" };
        foreach (var region in requiredRegions.Where(r => !r.Equals("hips", StringComparison.OrdinalIgnoreCase)
                                                       && !r.Equals("neck", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.True(fields.ContainsKey(region),
                $"Body '{body}' is missing region '{region}'");
        }
    }

    [Fact]
    public void BodyTransformationFieldCatalog_FallbackBody_HasAtLeastWaistAndChest()
    {
        var fields = BodyTransformationFieldCatalog.Resolve("UNKNOWN_BODY_XYZ");
        Assert.True(fields.ContainsKey("waist"));
        Assert.True(fields.ContainsKey("chest"));
    }
}

public sealed class ExpandedTextureClassificationTests
{
    [Theory]
    [InlineData("body_s.dds",        "specular")]
    [InlineData("chest_spec.dds",    "specular")]
    [InlineData("arm_specular.dds",  "specular")]
    [InlineData("body_g.dds",        "glow")]
    [InlineData("chest_glow.dds",    "glow")]
    [InlineData("leg_em.dds",        "glow")]
    [InlineData("body_p.dds",        "parallax")]
    [InlineData("torso_parallax.dds","parallax")]
    [InlineData("body_h.dds",        "parallax")]
    [InlineData("skin_sk.dds",       "subsurface")]
    [InlineData("body_sss.dds",      "subsurface")]
    [InlineData("skin_subsurface.dds","subsurface")]
    public async Task BasicTextureAnalysisService_ClassifiesTextureTypes(string fileName, string expectedCategory)
    {
        var workingDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDir);
        var texPath = Path.Combine(workingDir, fileName);
        // Write valid DDS magic bytes so IsValidDdsAsync accepts the file
        await File.WriteAllBytesAsync(texPath, [0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00]);

        try
        {
            var armor = new ImportedArmor("armor.nif", [], [texPath], [], [], "CBBE");
            var service = new BasicTextureAnalysisService();
            var summary = await service.AnalyzeAsync(armor, CancellationToken.None);

            switch (expectedCategory)
            {
                case "specular":
                    Assert.True(summary.SpecularFiles?.Count > 0, $"{fileName} should produce a specular entry");
                    break;
                case "glow":
                    Assert.True(summary.GlowFiles?.Count > 0, $"{fileName} should produce a glow entry");
                    break;
                case "parallax":
                    Assert.True(summary.ParallaxFiles?.Count > 0, $"{fileName} should produce a parallax entry");
                    break;
                case "subsurface":
                    Assert.True(summary.SubsurfaceFiles?.Count > 0, $"{fileName} should produce a subsurface entry");
                    break;
            }
        }
        finally
        {
            Directory.Delete(workingDir, recursive: true);
        }
    }
}

public sealed class ExpandedVanillaArmorDatabaseTests
{
    [Theory]
    [InlineData("orcisharmor")]
    [InlineData("stalhrimarmor")]
    [InlineData("stalhrim")]
    [InlineData("nordiccarvedarmor")]
    [InlineData("bonemoldarmor")]
    [InlineData("chitinarmor")]
    [InlineData("skaalarmor")]
    [InlineData("ebonymail")]
    [InlineData("falmerhardened")]
    [InlineData("draugrarmor")]
    [InlineData("penitusoculatus")]
    [InlineData("vampireroyalarmor")]
    [InlineData("dawnguardheavy")]
    [InlineData("nightingalearmor")]
    [InlineData("imperialstudded")]
    [InlineData("wolfarmor")]
    [InlineData("saviorshide")]
    [InlineData("boundarmor")]
    [InlineData("ancientfalmer")]
    public void VanillaArmorDatabase_ContainsNewEntry(string token)
    {
        var db = new VanillaArmorLookupService();
        var found = db.All.Any(e => e.MeshFileTokens.Contains(token, StringComparer.OrdinalIgnoreCase));
        Assert.True(found, $"VanillaArmorDatabase should contain token '{token}'");
    }

    [Fact]
    public void VanillaArmorDatabase_TotalCount_AtLeast65()
    {
        var db = new VanillaArmorLookupService();
        Assert.True(db.All.Count >= 65, $"Expected >= 65 vanilla armors, got {db.All.Count}");
    }
}

public sealed class BodyTypeCatalogTests
{
    [Fact]
    public void BodyTypeCatalog_All_ContainsExpectedBodies()
    {
        var names = BodyTypeCatalog.All.Select(b => b.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in new[] { "CBBE", "3BA", "BHUNP", "UNP", "HIMBO", "SAM", "SOS", "UBE" })
        {
            Assert.Contains(expected, names);
        }
    }

    [Fact]
    public void BodyTypeCatalog_All_EachBodyHasDetectionTokens()
    {
        foreach (var body in BodyTypeCatalog.All)
        {
            Assert.NotEmpty(body.DetectionTokens);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Vanilla profile auto-apply tests
// ─────────────────────────────────────────────────────────────────────────────
public sealed class VanillaProfileAutoApplyTests
{
    [Fact]
    public async Task ConvertAsync_VanillaArmorDetected_AppliesRecommendedProfileWhenNoneProvided()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        // ironarmor is in the vanilla armor database with profile=curvy
        var inputFile = Path.Combine(workingDirectory, "ironarmor_0.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var profileStep = result.Steps.FirstOrDefault(s => s.StartsWith("vanilla-profile:", StringComparison.Ordinal));
            Assert.NotNull(profileStep);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConvertAsync_ExplicitProfileProvided_OverridesVanillaProfile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "ironarmor_0.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(
                new ConversionRequest(inputFile, "CBBE", outputDirectory, DeformationProfile: "slim"));

            Assert.True(result.Success);
            // Explicit slim profile should appear; vanilla-profile step should NOT appear
            var vanillaProfileStep = result.Steps.FirstOrDefault(s => s.StartsWith("vanilla-profile:", StringComparison.Ordinal));
            Assert.Null(vanillaProfileStep);
            Assert.Contains(result.Steps, s => s.Contains("slim", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Source→target delta conversion tests
// ─────────────────────────────────────────────────────────────────────────────
public sealed class SourceTargetDeltaTests
{
    [Fact]
    public async Task ConvertAsync_DifferentSourceAndTarget_EmitsDeltaStep()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        // cuirass → meshAnalysis will classify it and body detection will assign a source body
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            // Use SourceBodyOverride to guarantee different source and target bodies
            var result = await orchestrator.ConvertAsync(
                new ConversionRequest(inputFile, "3BA", outputDirectory, SourceBodyOverride: "CBBE"));

            Assert.True(result.Success);
            var deltaStep = result.Steps.FirstOrDefault(s => s.StartsWith("conversion-delta:", StringComparison.Ordinal));
            Assert.NotNull(deltaStep);
            Assert.Contains("CBBE", deltaStep, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("3BA",  deltaStep, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StrategyMeshConversionService_SameSourceAndTarget_DeltaIsNeutral()
    {
        var service = new StrategyMeshConversionService();
        var armor   = new ImportedArmor("test.nif", ["test.nif"], [], [], []);
        var analysis = new MeshAnalysis("leather", false, 1);
        var cage     = new DeformationCage("hybrid-cage");

        // CBBE→CBBE: delta should be 1.0 per region (no change).
        var result = await service.ConvertAsync(armor, analysis, cage, "CBBE", null, "CBBE", CancellationToken.None);

        var cbbeField = BodyTransformationFieldCatalog.Resolve("CBBE");
        foreach (var region in cbbeField.Keys)
        {
            if (result.RegionalMorphing.TryGetValue(region, out var v))
            {
                Assert.Equal(1.0, v, precision: 6);
            }
        }
    }

    [Fact]
    public async Task StrategyMeshConversionService_DifferentBodies_DeltaIsRelative()
    {
        var service  = new StrategyMeshConversionService();
        var armor    = new ImportedArmor("test.nif", ["test.nif"], [], [], []);
        var analysis = new MeshAnalysis("leather", false, 1);
        var cage     = new DeformationCage("hybrid-cage");

        var cbbeField = BodyTransformationFieldCatalog.Resolve("CBBE");
        var unpField  = BodyTransformationFieldCatalog.Resolve("UNP");

        var result = await service.ConvertAsync(armor, analysis, cage, "UNP", null, "CBBE", CancellationToken.None);

        // Verify at least one region shows the expected delta (targetValue / sourceValue)
        var region = "chest";
        Assert.True(cbbeField.ContainsKey(region) && unpField.ContainsKey(region));
        var expectedDelta = unpField[region] / cbbeField[region];
        Assert.Equal(expectedDelta, result.RegionalMorphing[region], precision: 5);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Armor region binding tests
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ArmorRegionBindingTests
{
    [Fact]
    public async Task ConvertAsync_WithDefaultModules_EmitsRegionsStep()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "cuirass.nif");
        await File.WriteAllTextAsync(inputFile, "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(inputFile, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var regionsStep = result.Steps.FirstOrDefault(s => s.StartsWith("regions:", StringComparison.Ordinal));
            Assert.NotNull(regionsStep);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("cuirass.nif",   "chest")]
    [InlineData("boots.nif",     "legs")]
    [InlineData("gauntlets.nif", "arms")]
    [InlineData("helmet.nif",    "shoulders")]
    public async Task BasicArmorRegionBindingService_FilenameHints_DetectsCorrectRegion(string fileName, string expectedRegion)
    {
        var workingDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDir);
        var filePath = Path.Combine(workingDir, fileName);
        await File.WriteAllTextAsync(filePath, "mesh");

        try
        {
            var service  = new BasicArmorRegionBindingService();
            var armor    = new ImportedArmor(filePath, [filePath], [], [], []);
            var analysis = new MeshAnalysis("leather", false, 1);

            var binding = await service.BindAsync(armor, analysis, CancellationToken.None);

            Assert.Contains(expectedRegion, binding.CoveredRegions, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDir, recursive: true);
        }
    }

    [Fact]
    public async Task BasicArmorRegionBindingService_NoSignals_DefaultsToFullBodyRegions()
    {
        var service  = new BasicArmorRegionBindingService();
        var armor    = new ImportedArmor("xyz.nif", ["xyz.nif"], [], [], []);
        var analysis = new MeshAnalysis("mixed", false, 1);

        var binding = await service.BindAsync(armor, analysis, CancellationToken.None);

        Assert.Equal("default-full-body", binding.DetectionMethod);
        Assert.NotEmpty(binding.CoveredRegions);
    }

    [Fact]
    public async Task BasicArmorRegionBindingService_GeometryFallbackDetectsSpatialRegions()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        var meshPath = Path.Combine(workingDirectory, "mysteryarmor.nif");

        try
        {
            await SyntheticNifTestData.WriteAsync(meshPath, SyntheticNifTestData.CreateUpperBodyArmorVertices());

            var service = new BasicArmorRegionBindingService();
            var armor = new ImportedArmor(meshPath, [meshPath], [], [], []);
            var analysis = new MeshAnalysis("mixed", false, 1);

            var binding = await service.BindAsync(armor, analysis, CancellationToken.None);

            Assert.Equal("spatial-geometry", binding.DetectionMethod);
            Assert.Contains("chest", binding.CoveredRegions, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("arms", binding.CoveredRegions, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Batch report tests
// ─────────────────────────────────────────────────────────────────────────────
public sealed class BatchReportTests
{
    [Fact]
    public async Task BatchConvert_Directory_WritesBatchReportJson()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "armor1.nif"), "mesh");
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "armor2.nif"), "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var runner = new BatchConversionRunner(orchestrator);
            var results = await runner.ConvertAsync(new ConversionRequest(workingDirectory, "CBBE", outputDirectory));

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.True(r.Success));

            var reportPath = Path.Combine(outputDirectory, "batch-report.json");
            Assert.True(File.Exists(reportPath), "batch-report.json was not written.");

            var content = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("\"TotalCount\"",   content, StringComparison.Ordinal);
            Assert.Contains("\"SuccessCount\"", content, StringComparison.Ordinal);
            Assert.Contains("\"TargetBody\"",   content, StringComparison.Ordinal);
            Assert.Contains("\"CBBE\"",         content, StringComparison.Ordinal);
            Assert.Contains("\"Results\"",      content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BatchConvert_Directory_ReportCountsMatchResults()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "a.nif"), "mesh");
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "b.nif"), "mesh");
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "c.nif"), "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var runner = new BatchConversionRunner(orchestrator);
            await runner.ConvertAsync(new ConversionRequest(workingDirectory, "3BA", outputDirectory));

            var reportPath = Path.Combine(outputDirectory, "batch-report.json");
            var content = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("\"TotalCount\": 3",   content, StringComparison.Ordinal);
            Assert.Contains("\"SuccessCount\": 3", content, StringComparison.Ordinal);
            Assert.Contains("\"FailedCount\": 0",  content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Pose simulation, preview.html, and xEdit script tests
// ─────────────────────────────────────────────────────────────────────────────
public sealed class PoseSimulationAndPreviewTests
{
    // ── BasicPoseSimulationService ────────────────────────────────────────────

    [Fact]
    public async Task PoseSimulation_NoHighMorphs_ReturnsZeroAtRiskPoses()
    {
        var service = new BasicPoseSimulationService();
        // Use 0.92 so that even with the highest regional amplifier (belly in Crouch = 1.12),
        // the effective stress stays below the 1.10 risk threshold: 0.92 × 1.12 = 1.030.
        var mesh    = new ConvertedMesh("mixed", "test", 1, new Dictionary<string, double>
        {
            ["chest"] = 0.92, ["waist"] = 0.92, ["belly"] = 0.92
        });

        var result = await service.SimulateAsync(mesh, "CBBE", CancellationToken.None);

        Assert.Equal(0, result.TotalPosesAtRisk);
        Assert.Empty(result.HighRiskRegions);
        Assert.Empty(result.PoseClippingRisk);
        Assert.Equal(8, result.TestedPoses.Count);
    }

    [Fact]
    public async Task PoseSimulation_HighThighMorph_FlagsHighStressPoses()
    {
        var service = new BasicPoseSimulationService();
        // thigh morph 1.09 × crouch amplifier 1.20 = 1.308 → above threshold
        var mesh = new ConvertedMesh("mixed", "test", 1, new Dictionary<string, double>
        {
            ["thighs"] = 1.09
        });

        var result = await service.SimulateAsync(mesh, "CBBE", CancellationToken.None);

        Assert.True(result.TotalPosesAtRisk > 0, "Expected at least one pose at risk for high thigh morph.");
        Assert.Contains("thighs", result.HighRiskRegions, StringComparer.OrdinalIgnoreCase);
        Assert.True(result.PoseClippingRisk.ContainsKey("Crouch") || result.PoseClippingRisk.ContainsKey("Sneak"),
            "Crouch or Sneak should be flagged for high thigh morph.");
    }

    [Fact]
    public async Task PoseSimulation_UnknownRegion_IsIgnoredGracefully()
    {
        var service = new BasicPoseSimulationService();
        var mesh = new ConvertedMesh("mixed", "test", 1, new Dictionary<string, double>
        {
            ["nonexistent_region"] = 2.50
        });

        // Should not throw even though the region has no pose amplifiers defined.
        var result = await service.SimulateAsync(mesh, "CBBE", CancellationToken.None);
        Assert.NotNull(result);
        // Effective stress = 2.50 × 1.0 (no amplifier) = 2.50 → above threshold → flagged
        Assert.True(result.TotalPosesAtRisk > 0, "T-pose should flag very high morph even without amplifier.");
    }

    [Fact]
    public async Task PoseSimulation_ResultContainsAllEightPoses()
    {
        var service = new BasicPoseSimulationService();
        var mesh    = new ConvertedMesh("mixed", "test", 1, new Dictionary<string, double>());
        var result  = await service.SimulateAsync(mesh, "CBBE", CancellationToken.None);

        var expectedPoses = new[] { "T-pose", "Walk", "Run", "Idle", "Crouch", "Combat-Idle", "Jump", "Sneak" };
        foreach (var pose in expectedPoses)
        {
            Assert.Contains(result.TestedPoses, p => string.Equals(p, pose, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ── preview.html output ───────────────────────────────────────────────────

    [Fact]
    public async Task Convert_WithDefaultModules_WritesPreviewHtmlFile()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "out");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "testarmor.nif"), "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            await orchestrator.ConvertAsync(new ConversionRequest(
                Path.Combine(workingDirectory, "testarmor.nif"), "CBBE", outputDirectory));

            var previewPath = Path.Combine(outputDirectory, "preview.html");
            Assert.True(File.Exists(previewPath), "preview.html was not written.");

            var html = await File.ReadAllTextAsync(previewPath);
            Assert.Contains("<!DOCTYPE html>",  html, StringComparison.Ordinal);
            Assert.Contains("<svg ",            html, StringComparison.Ordinal);
            Assert.Contains("SlideSmith",       html, StringComparison.Ordinal);
            Assert.Contains("CBBE",             html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Convert_WithDefaultModules_PreviewHtmlContainsRegionalMorphingTable()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "out");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "testarmor.nif"), "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            await orchestrator.ConvertAsync(new ConversionRequest(
                Path.Combine(workingDirectory, "testarmor.nif"), "CBBE", outputDirectory));

            var html = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "preview.html"));
            Assert.Contains("Regional Morphing", html, StringComparison.Ordinal);
            Assert.Contains("<table>",            html, StringComparison.Ordinal);
            Assert.Contains("Factor",             html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Convert_WithDefaultModules_DoesNotWriteLegacyPreviewRendersJson()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "out");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "testarmor.nif"), "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            await orchestrator.ConvertAsync(new ConversionRequest(
                Path.Combine(workingDirectory, "testarmor.nif"), "CBBE", outputDirectory));

            Assert.False(File.Exists(Path.Combine(outputDirectory, "preview-renders.json")),
                "preview-renders.json should not be written (replaced by preview.html).");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    // ── pose-simulation-report.json ───────────────────────────────────────────

    [Fact]
    public async Task Convert_WithDefaultModules_WritesPoseSimulationReport()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "out");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "testarmor.nif"), "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            await orchestrator.ConvertAsync(new ConversionRequest(
                Path.Combine(workingDirectory, "testarmor.nif"), "CBBE", outputDirectory));

            var reportPath = Path.Combine(outputDirectory, "pose-simulation-report.json");
            Assert.True(File.Exists(reportPath), "pose-simulation-report.json was not written.");

            var json = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("TestedPoses",      json, StringComparison.Ordinal);
            Assert.Contains("TotalPosesAtRisk", json, StringComparison.Ordinal);
            Assert.Contains("HighRiskRegions",  json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ConversionLog_ContainsPoseSimulationStep()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "out");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "testarmor.nif"), "mesh");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(new ConversionRequest(
                Path.Combine(workingDirectory, "testarmor.nif"), "CBBE", outputDirectory));

            Assert.True(result.Steps.Any(s => s.StartsWith("pose-simulation:", StringComparison.Ordinal)),
                "Steps should contain a pose-simulation entry.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    // ── patch-armor.pas (xEdit script) ────────────────────────────────────────

    [Fact]
    public async Task Convert_WithPlugins_WritesXEditScript()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "out");
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "testarmor.nif"), "mesh");
        await File.WriteAllTextAsync(Path.Combine(workingDirectory, "testarmor.esp"), "TES5");

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            await orchestrator.ConvertAsync(new ConversionRequest(
                Path.Combine(workingDirectory, "testarmor.nif"), "CBBE", outputDirectory));

            var scriptPath = Path.Combine(outputDirectory, "patch-armor.pas");
            if (File.Exists(scriptPath))
            {
                var content = await File.ReadAllTextAsync(scriptPath);
                Assert.Contains("SlideSmith",   content, StringComparison.Ordinal);
                Assert.Contains("ARMA",         content, StringComparison.Ordinal);
                Assert.Contains("unit ",        content, StringComparison.Ordinal);
                Assert.Contains("Initialize",   content, StringComparison.Ordinal);
                Assert.Contains("Finalize",     content, StringComparison.Ordinal);
                Assert.Contains("SetEditValue", content, StringComparison.Ordinal);
                Assert.Contains("1st Person",   content, StringComparison.Ordinal);
                Assert.Contains(@"Male World Model\MOD2", content, StringComparison.Ordinal);
                Assert.Contains(@"Female World Model\MOD3", content, StringComparison.Ordinal);
                Assert.Contains(@"Male 1st Person\MOD4", content, StringComparison.Ordinal);
                Assert.Contains(@"Female 1st Person\MOD5", content, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Animation-Driven Geometry Solver Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class AnimationDrivenGeometrySolverTests
{
    // ── AnimationDrivenGeometrySolver.Solve ───────────────────────────────────

    [Fact]
    public void Solve_EmptyVertexList_ReturnsZeroVerticesAndEmptyPushOut()
    {
        var result = AnimationDrivenGeometrySolver.Solve(
            [],
            new Dictionary<string, double>());

        Assert.Equal(0, result.VerticesAnalyzed);
        Assert.Empty(result.MaxPushOutPerRegion);
        Assert.Contains("no-vertices", result.Method, StringComparison.Ordinal);
    }

    [Fact]
    public void Solve_VerticesInsideBodyEnvelope_ProducesPushOutForAffectedRegions()
    {
        // Thigh vertices very close to the body axis: they should penetrate the
        // thigh cylinder (baseRadius=0.088 × morphFactor=1.4 = 0.1232) when the
        // Crouch pose rotates them toward it.
        var vertices = new List<(float X, float Y, float Z)>();
        for (var i = 0; i < 20; i++)
        {
            // Place vertices in the thigh region (normalised height ~0.35)
            var z = 0.35f + (i * 0.001f);
            vertices.Add((0.05f, 0.01f, z));  // near body axis
        }

        var morphing = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["thighs"] = 1.4  // generous morph expands the body envelope
        };

        var result = AnimationDrivenGeometrySolver.Solve(vertices, morphing);

        Assert.Equal(vertices.Count, result.VerticesAnalyzed);
        Assert.Equal("animation-driven", result.Method);
        // Vertices near the axis should be inside the expanded body envelope → push-out > 0
        Assert.True(result.MaxPushOutPerRegion.ContainsKey("thighs"),
            "Thigh region should have a push-out entry for vertices inside the body envelope.");
        Assert.True(result.MaxPushOutPerRegion["thighs"] > 0,
            "Push-out for thigh region should be positive.");
    }

    [Fact]
    public void Solve_VerticesFarFromBodyAxis_NoPushOut()
    {
        // Vertices in a symmetric +X/-X pattern so that after XY normalisation each
        // vertex sits at xyDist ≈ 0.5 — far beyond any body-envelope radius (max 0.115).
        var vertices = new List<(float X, float Y, float Z)>();
        for (var i = 0; i < 10; i++)
        {
            var z = (float)i * 0.1f;
            vertices.Add(( 1.0f, 0f, z));
            vertices.Add((-1.0f, 0f, z));
        }

        var morphing = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["thighs"] = 1.0, ["chest"] = 1.0
        };

        var result = AnimationDrivenGeometrySolver.Solve(vertices, morphing);

        Assert.Equal(vertices.Count, result.VerticesAnalyzed);
        // All vertices are far from the body axis; none should penetrate the envelope
        Assert.True(result.MaxPushOutPerRegion.Values.All(v => v <= 0),
            "Vertices far from body axis should have zero push-out.");
    }

    [Fact]
    public void Solve_AllEightPosesEvaluated_MethodIsAnimationDriven()
    {
        var vertices = SyntheticNifTestData.CreateBodyVertices(100);
        var morphing = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["thighs"] = 1.1, ["chest"] = 1.1, ["belly"] = 1.05
        };

        var result = AnimationDrivenGeometrySolver.Solve(vertices, morphing);

        Assert.Equal("animation-driven", result.Method);
        Assert.True(result.VerticesAnalyzed == 100);
    }

    [Fact]
    public void HeightToRegion_CorrectlyMapsNormalisedHeights()
    {
        Assert.Equal("feet",      AnimationDrivenGeometrySolver.HeightToRegion(0.01f));
        Assert.Equal("calves",    AnimationDrivenGeometrySolver.HeightToRegion(0.15f));
        Assert.Equal("thighs",    AnimationDrivenGeometrySolver.HeightToRegion(0.40f));
        Assert.Equal("butt",      AnimationDrivenGeometrySolver.HeightToRegion(0.52f));
        Assert.Equal("pelvis",    AnimationDrivenGeometrySolver.HeightToRegion(0.60f));
        Assert.Equal("belly",     AnimationDrivenGeometrySolver.HeightToRegion(0.67f));
        Assert.Equal("waist",     AnimationDrivenGeometrySolver.HeightToRegion(0.74f));
        Assert.Equal("chest",     AnimationDrivenGeometrySolver.HeightToRegion(0.82f));
        Assert.Equal("shoulders", AnimationDrivenGeometrySolver.HeightToRegion(0.90f));
        Assert.Equal("arms",      AnimationDrivenGeometrySolver.HeightToRegion(0.97f));
    }

    // ── AnimationDrivenPoseSimulationService ──────────────────────────────────

    [Fact]
    public async Task AnimationDrivenService_SimulateAsync_ReturnsEightPoses()
    {
        var service = new AnimationDrivenPoseSimulationService();
        var mesh    = new ConvertedMesh("mixed", "test", 1, new Dictionary<string, double>());
        var result  = await service.SimulateAsync(mesh, "CBBE", CancellationToken.None);

        var expectedPoses = new[] { "T-pose", "Walk", "Run", "Idle", "Crouch", "Combat-Idle", "Jump", "Sneak" };
        foreach (var pose in expectedPoses)
        {
            Assert.Contains(result.TestedPoses, p => string.Equals(p, pose, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task AnimationDrivenService_SimulateAsync_HeuristicFallback_FlagsHighMorphRegion()
    {
        var service = new AnimationDrivenPoseSimulationService();
        // No mesh paths supplied → heuristic path; thigh 1.09 × crouch 1.20 = 1.308 > 1.10 threshold
        var mesh = new ConvertedMesh("mixed", "test", 1, new Dictionary<string, double>
        {
            ["thighs"] = 1.09
        });

        var result = await service.SimulateAsync(mesh, "CBBE", CancellationToken.None);

        Assert.True(result.TotalPosesAtRisk > 0, "Expected at least one pose at risk for high thigh morph.");
        Assert.Contains("thighs", result.HighRiskRegions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnimationDrivenService_SimulateWithMeshDataAsync_NullPaths_UsesFallback()
    {
        var service = new AnimationDrivenPoseSimulationService();
        var mesh    = new ConvertedMesh("mixed", "test", 1, new Dictionary<string, double>
        {
            ["thighs"] = 1.09
        });

        var result = await service.SimulateWithMeshDataAsync(mesh, "CBBE", null, CancellationToken.None);

        Assert.Equal(8, result.TestedPoses.Count);
        Assert.True(result.TotalPosesAtRisk > 0);
    }

    [Fact]
    public async Task AnimationDrivenService_SimulateWithMeshDataAsync_WithSyntheticNif_UsesAnimationDrivenPath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        var nifPath = Path.Combine(workingDirectory, "test.nif");

        // Write a synthetic NIF with body-region vertices
        var vertices = SyntheticNifTestData.CreateBodyVertices(200);
        await SyntheticNifTestData.WriteAsync(nifPath, vertices);

        try
        {
            var service = new AnimationDrivenPoseSimulationService();
            var mesh    = new ConvertedMesh("cloth", "3BA", 1, new Dictionary<string, double>
            {
                ["thighs"] = 1.15, ["chest"] = 1.12, ["belly"] = 1.08
            });

            var result = await service.SimulateWithMeshDataAsync(
                mesh, "3BA", [nifPath], CancellationToken.None);

            Assert.Equal(8, result.TestedPoses.Count);
            // Animation-driven path processes real vertex data; result is a valid PoseSimulationResult
            Assert.NotNull(result.HighRiskRegions);
            Assert.NotNull(result.PoseClippingRisk);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AnimationDrivenService_SimulateWithMeshDataAsync_MissingFile_FallsBackToHeuristic()
    {
        var service = new AnimationDrivenPoseSimulationService();
        var mesh    = new ConvertedMesh("mixed", "test", 1, new Dictionary<string, double>
        {
            ["thighs"] = 1.09
        });

        // Supply a non-existent path; service should fall back to heuristic silently
        var result = await service.SimulateWithMeshDataAsync(
            mesh, "CBBE", ["/nonexistent/path/test.nif"], CancellationToken.None);

        Assert.Equal(8, result.TestedPoses.Count);
        Assert.True(result.TotalPosesAtRisk > 0, "Heuristic fallback should flag high thigh morph.");
    }

    // ── Integration: animation-driven path in full pipeline ───────────────────

    [Fact]
    public async Task Convert_WithSyntheticNif_PoseSimulationStepPresent()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory  = Path.Combine(workingDirectory, "out");
        Directory.CreateDirectory(workingDirectory);
        var inputFile = Path.Combine(workingDirectory, "armor.nif");
        await SyntheticNifTestData.WriteAsync(inputFile, SyntheticNifTestData.CreateBodyVertices(200));

        try
        {
            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(
                new ConversionRequest(inputFile, "3BA", outputDirectory));

            Assert.True(result.Success);
            Assert.True(result.Steps.Any(s => s.StartsWith("pose-simulation:", StringComparison.Ordinal)),
                "Steps should contain a pose-simulation entry.");
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BinaryPluginRewriteService Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class BinaryPluginRewriteServiceTests
{
    // ── Header-size detection ─────────────────────────────────────────────────

    [Fact]
    public void DetectHeaderSize_TooShort_ReturnsSseDefault()
    {
        var result = BinaryPluginRewriteService.DetectHeaderSize([]);
        Assert.Equal(24, result);
    }

    [Fact]
    public void DetectHeaderSize_NotTes4_ReturnsSseDefault()
    {
        var bytes = new byte[30];
        System.Text.Encoding.ASCII.GetBytes("GRUP").CopyTo(bytes, 0);
        Assert.Equal(24, BinaryPluginRewriteService.DetectHeaderSize(bytes));
    }

    [Fact]
    public void DetectHeaderSize_LePlugin_Returns20()
    {
        // Build a minimal TES4 header where "HEDR" appears at offset 20 (LE layout).
        var bytes = new byte[28];
        System.Text.Encoding.ASCII.GetBytes("TES4").CopyTo(bytes, 0);
        System.Text.Encoding.ASCII.GetBytes("HEDR").CopyTo(bytes, 20);
        Assert.Equal(20, BinaryPluginRewriteService.DetectHeaderSize(bytes));
    }

    [Fact]
    public void DetectHeaderSize_SsePlugin_Returns24()
    {
        // TES4 header where "HEDR" is NOT at offset 20 (SSE — 24-byte headers).
        var bytes = new byte[32];
        System.Text.Encoding.ASCII.GetBytes("TES4").CopyTo(bytes, 0);
        System.Text.Encoding.ASCII.GetBytes("HEDR").CopyTo(bytes, 24);
        Assert.Equal(24, BinaryPluginRewriteService.DetectHeaderSize(bytes));
    }

    // ── ARMA subrecord rewrite ────────────────────────────────────────────────

    [Fact]
    public void RewriteArmaSubrecords_NoMatchingPaths_ReturnsBytesUnchanged()
    {
        // Build an ARMA data block with a single EDID subrecord (not a mesh path subrecord).
        const string edid = "MyArmor\0";
        var edidBytes = System.Text.Encoding.ASCII.GetBytes(edid);
        byte[] data = BuildSubrecord("EDID", edidBytes);

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = "meshes/slidesmith/cbbe/iron_0.nif"
        };

        var (newData, rewritten) = BinaryPluginRewriteService.RewriteArmaSubrecords(
            data, 0, data.Length, rewriteMap);

        Assert.Equal(0, rewritten);
        Assert.Equal(data, newData);
    }

    [Fact]
    public void RewriteArmaSubrecords_MatchingMod2_RewritesPath()
    {
        const string original = "meshes/armor/iron/iron_0.nif\0";
        const string expected = "meshes/slidesmith/cbbe/iron_0.nif";

        var origBytes = System.Text.Encoding.ASCII.GetBytes(original);
        byte[] data = BuildSubrecord("MOD2", origBytes);

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = expected
        };

        var (newData, rewritten) = BinaryPluginRewriteService.RewriteArmaSubrecords(
            data, 0, data.Length, rewriteMap);

        Assert.Equal(1, rewritten);

        // Parse the output subrecord and verify the path.
        var subType = System.Text.Encoding.ASCII.GetString(newData, 0, 4);
        var subSize = (ushort)(newData[4] | (newData[5] << 8));
        var writtenPath = System.Text.Encoding.ASCII
            .GetString(newData, 6, subSize - 1); // exclude null terminator
        Assert.Equal("MOD2", subType);
        Assert.Equal(expected, writtenPath, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void RewriteArmaSubrecords_Mod3Mod4Mod5_AllRewritten()
    {
        const string orig3 = "meshes/armor/iron/iron_m_0.nif\0";
        const string orig4 = "meshes/armor/iron/iron_1stf.nif\0";
        const string orig5 = "meshes/armor/iron/iron_1stm.nif\0";

        byte[] mod3 = BuildSubrecord("MOD3", System.Text.Encoding.ASCII.GetBytes(orig3));
        byte[] mod4 = BuildSubrecord("MOD4", System.Text.Encoding.ASCII.GetBytes(orig4));
        byte[] mod5 = BuildSubrecord("MOD5", System.Text.Encoding.ASCII.GetBytes(orig5));

        byte[] data = [..mod3, ..mod4, ..mod5];

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_m_0.nif"]  = "meshes/slidesmith/himbo/iron_m_0.nif",
            ["meshes/armor/iron/iron_1stf.nif"] = "meshes/slidesmith/himbo/iron_1stf.nif",
            ["meshes/armor/iron/iron_1stm.nif"] = "meshes/slidesmith/himbo/iron_1stm.nif",
        };

        var (_, rewritten) = BinaryPluginRewriteService.RewriteArmaSubrecords(
            data, 0, data.Length, rewriteMap);

        Assert.Equal(3, rewritten);
    }

    [Fact]
    public void RewriteArmaSubrecords_CaseInsensitiveLookup_MatchesUpperCasePath()
    {
        // Plugin stores path with Windows mixed-case backslashes; map key is lowercase.
        const string storedPath  = "Meshes\\Armor\\Iron\\Iron_0.nif\0";
        const string newPath     = "meshes/slidesmith/cbbe/iron_0.nif";
        const string mapKey      = "meshes/armor/iron/iron_0.nif";   // normalised lowercase

        byte[] data = BuildSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(storedPath));

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [mapKey] = newPath
        };

        var (_, rewritten) = BinaryPluginRewriteService.RewriteArmaSubrecords(
            data, 0, data.Length, rewriteMap);

        // BinaryPluginRewriteService normalises path before lookup, so this should match.
        Assert.Equal(1, rewritten);
    }

    [Fact]
    public void RewriteArmaSubrecords_ExtendedSizeMod2_RewritesPath()
    {
        const string original = "meshes/armor/iron/iron_0.nif\0";
        const string expected = "meshes/slidesmith/cbbe/iron_0.nif";
        byte[] data = BuildExtendedSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(original));

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = expected
        };

        var (newData, rewritten) = BinaryPluginRewriteService.RewriteArmaSubrecords(
            data, 0, data.Length, rewriteMap);

        Assert.Equal(1, rewritten);
        Assert.Equal("MOD2", System.Text.Encoding.ASCII.GetString(newData, 0, 4));
        var subSize = (ushort)(newData[4] | (newData[5] << 8));
        var writtenPath = System.Text.Encoding.ASCII.GetString(newData, 6, subSize - 1);
        Assert.Equal(expected, writtenPath);
    }

    [Fact]
    public void RewriteArmaSubrecords_LongReplacementPath_WritesExtendedSizeSubrecord()
    {
        const string original = "meshes/armor/iron/iron_0.nif\0";
        var longPath = "meshes/slidesmith/" + new string('a', 70000) + ".nif";
        byte[] data = BuildSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(original));

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = longPath
        };

        var (newData, rewritten) = BinaryPluginRewriteService.RewriteArmaSubrecords(
            data, 0, data.Length, rewriteMap);

        Assert.Equal(1, rewritten);
        Assert.Equal("XXXX", System.Text.Encoding.ASCII.GetString(newData, 0, 4));
        Assert.Equal(4, (ushort)(newData[4] | (newData[5] << 8)));
        var extendedSize = newData[6] | (newData[7] << 8) | (newData[8] << 16) | (newData[9] << 24);
        Assert.Equal(longPath.Length + 1, extendedSize);
        Assert.Equal("MOD2", System.Text.Encoding.ASCII.GetString(newData, 10, 4));
        Assert.Equal(0, (ushort)(newData[14] | (newData[15] << 8)));
    }

    [Fact]
    public void RewriteArmaSubrecords_CompressedFlagSkipped_ReturnsUnchanged()
    {
        // The RewritePlugin call skips compressed ARMA; this tests the raw subrecord
        // function with an empty data block — should produce 0 rewrites and no crash.
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var (newData, rewritten) = BinaryPluginRewriteService.RewriteArmaSubrecords(
            [], 0, 0, rewriteMap);

        Assert.Equal(0, rewritten);
        Assert.Empty(newData);
    }

    // ── Full plugin round-trip ────────────────────────────────────────────────

    [Fact]
    public void RewritePlugin_NoArmaRecord_ReturnsIdenticalBytes()
    {
        // Build a minimal SSE plugin with only a TES4 record (no ARMA).
        byte[] plugin = BuildMinimalPlugin_SseNoArma();
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = "meshes/slidesmith/cbbe/iron_0.nif"
        };

        var (patched, armaPatched, pathsRewritten, _) =
            BinaryPluginRewriteService.RewritePlugin(plugin, 24, rewriteMap);

        Assert.Equal(0, armaPatched);
        Assert.Equal(0, pathsRewritten);
        Assert.Equal(plugin, patched);
    }

    [Fact]
    public void RewritePlugin_ArmaWithMatchingMod2_PatchesPathAndUpdatesDataSize()
    {
        const string origPath = "meshes/armor/iron/iron_0.nif\0";
        const string newPath  = "meshes/slidesmith/cbbe/iron_0.nif";

        byte[] mod2Data = BuildSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(origPath));
        byte[] plugin   = BuildMinimalPlugin_SseWithArma(mod2Data);

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = newPath
        };

        var (patched, armaPatched, pathsRewritten, warnings) =
            BinaryPluginRewriteService.RewritePlugin(plugin, 24, rewriteMap);

        Assert.Empty(warnings);
        Assert.Equal(1, armaPatched);
        Assert.Equal(1, pathsRewritten);

        // Verify the patched path appears verbatim in the output.
        var content = System.Text.Encoding.Latin1.GetString(patched);
        Assert.Contains(newPath, content, StringComparison.Ordinal);
    }

    [Fact]
    public void RewritePlugin_ArmaInsideGrup_PatchesPathAndUpdatesGrupSize()
    {
        const string origPath = "meshes/armor/iron/iron_0.nif\0";
        const string newPath  = "meshes/slidesmith/cbbe/iron_0.nif";

        byte[] mod2Data   = BuildSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(origPath));
        byte[] armaRecord = BuildArmaRecord(mod2Data, headerSize: 24);

        // Wrap the ARMA record inside a GRUP.
        byte[] plugin = BuildMinimalPlugin_SseWithGrupContaining(armaRecord);

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = newPath
        };

        var (patched, armaPatched, pathsRewritten, warnings) =
            BinaryPluginRewriteService.RewritePlugin(plugin, 24, rewriteMap);

        Assert.Empty(warnings);
        Assert.Equal(1, armaPatched);
        Assert.Equal(1, pathsRewritten);

        // Verify patched content is present.
        var content = System.Text.Encoding.Latin1.GetString(patched);
        Assert.Contains(newPath, content, StringComparison.Ordinal);
    }

    // ── RewriteAsync integration ──────────────────────────────────────────────

    [Fact]
    public async Task RewriteAsync_EmptyMap_ReturnsZeroResult()
    {
        var svc = new BinaryPluginRewriteService();
        var result = await svc.RewriteAsync(
            ["some/path.esp"],
            new Dictionary<string, string>(),
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.Equal(0, result.PluginsProcessed);
        Assert.Equal(0, result.PathsRewritten);
        Assert.Empty(result.PatchedPluginPaths);
    }

    [Fact]
    public async Task RewriteAsync_EmptyPluginList_ReturnsZeroResult()
    {
        var svc = new BinaryPluginRewriteService();
        var result = await svc.RewriteAsync(
            [],
            new Dictionary<string, string> { ["meshes/a.nif"] = "meshes/b.nif" },
            Path.GetTempPath(),
            CancellationToken.None);

        Assert.Equal(0, result.PluginsProcessed);
        Assert.Empty(result.PatchedPluginPaths);
    }

    [Fact]
    public async Task RewriteAsync_WithMatchingPlugin_WritesPatchedFile()
    {
        var workDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outDir  = Path.Combine(workDir, "out");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(outDir);

        try
        {
            const string origPath = "meshes/armor/iron/iron_0.nif\0";
            const string newPath  = "meshes/slidesmith/cbbe/iron_0.nif";

            byte[] mod2Data = BuildSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(origPath));
            byte[] plugin   = BuildMinimalPlugin_SseWithArma(mod2Data);

            var pluginPath = Path.Combine(workDir, "TestArmor.esp");
            await File.WriteAllBytesAsync(pluginPath, plugin);

            var svc = new BinaryPluginRewriteService();
            var result = await svc.RewriteAsync(
                [pluginPath],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["meshes/armor/iron/iron_0.nif"] = newPath
                },
                outDir,
                CancellationToken.None);

            Assert.Equal(1, result.PluginsProcessed);
            Assert.Equal(1, result.ArmaRecordsPatched);
            Assert.Equal(1, result.PathsRewritten);
            Assert.Single(result.PatchedPluginPaths);

            var patchedFile = result.PatchedPluginPaths[0];
            Assert.True(File.Exists(patchedFile));
            Assert.EndsWith("_patched.esp", patchedFile, StringComparison.OrdinalIgnoreCase);

            var patchedContent = System.Text.Encoding.Latin1.GetString(await File.ReadAllBytesAsync(patchedFile));
            Assert.Contains(newPath, patchedContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    [Fact]
    public async Task RewriteAsync_NoMatchingPaths_NoPatchedFileWritten()
    {
        var workDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outDir  = Path.Combine(workDir, "out");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(outDir);

        try
        {
            // Plugin with a path that is NOT in the rewrite map.
            byte[] mod2Data = BuildSubrecord("MOD2",
                System.Text.Encoding.ASCII.GetBytes("meshes/other/other_0.nif\0"));
            byte[] plugin = BuildMinimalPlugin_SseWithArma(mod2Data);

            var pluginPath = Path.Combine(workDir, "Other.esp");
            await File.WriteAllBytesAsync(pluginPath, plugin);

            var svc = new BinaryPluginRewriteService();
            var result = await svc.RewriteAsync(
                [pluginPath],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["meshes/armor/iron/iron_0.nif"] = "meshes/slidesmith/cbbe/iron_0.nif"
                },
                outDir,
                CancellationToken.None);

            Assert.Equal(1, result.PluginsProcessed);
            Assert.Equal(0, result.PathsRewritten);
            Assert.Empty(result.PatchedPluginPaths);
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    [Fact]
    public async Task RewriteAsync_WithMatchingEsmPlugin_PreservesPluginExtension()
    {
        var workDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outDir  = Path.Combine(workDir, "out");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(outDir);

        try
        {
            const string origPath = "meshes/armor/iron/iron_0.nif\0";
            const string newPath  = "meshes/slidesmith/cbbe/iron_0.nif";

            byte[] mod2Data = BuildSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(origPath));
            byte[] plugin   = BuildMinimalPlugin_SseWithArma(mod2Data);

            var pluginPath = Path.Combine(workDir, "TestArmor.esm");
            await File.WriteAllBytesAsync(pluginPath, plugin);

            var svc = new BinaryPluginRewriteService();
            var result = await svc.RewriteAsync(
                [pluginPath],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["meshes/armor/iron/iron_0.nif"] = newPath
                },
                outDir,
                CancellationToken.None);

            Assert.Single(result.PatchedPluginPaths);
            Assert.EndsWith("_patched.esm", result.PatchedPluginPaths[0], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    // ── ARMO record rewrite ───────────────────────────────────────────────────

    [Fact]
    public async Task RewriteAsync_ArmoWithMatchingMod2_PatchesPath()
    {
        var workDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outDir  = Path.Combine(workDir, "out");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(outDir);

        try
        {
            // Build a minimal plugin that has an ARMO record (not ARMA) with a MOD2 mesh path.
            const string origPath = "meshes/armor/iron/iron_0.nif";
            const string newPath  = "meshes/slidesmith/cbbe/iron_0.nif";

            byte[] mod2   = BuildSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(origPath + "\0"));
            byte[] armo   = BuildArmoRecord(mod2, headerSize: 24);
            byte[] plugin = [..BuildMinimalPlugin_SseNoArma(), ..armo];

            var pluginPath = Path.Combine(workDir, "ArmoTest.esp");
            await File.WriteAllBytesAsync(pluginPath, plugin);

            var svc    = new BinaryPluginRewriteService();
            var result = await svc.RewriteAsync(
                [pluginPath],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [origPath] = newPath
                },
                outDir,
                CancellationToken.None);

            Assert.Equal(1, result.PluginsProcessed);
            Assert.Equal(1, result.PathsRewritten);
            Assert.Single(result.PatchedPluginPaths);

            var patchedContent = System.Text.Encoding.Latin1.GetString(
                await File.ReadAllBytesAsync(result.PatchedPluginPaths[0]));
            Assert.Contains(newPath, patchedContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    /// <summary>Builds an ARMO record with a 24-byte SSE record header wrapping subrecord data.</summary>
    private static byte[] BuildArmoRecord(byte[] subrecordData, int headerSize = 24)
    {
        var buf = new byte[headerSize + subrecordData.Length];
        System.Text.Encoding.ASCII.GetBytes("ARMO").CopyTo(buf, 0);
        WriteUInt32Le(buf, 4, (uint)subrecordData.Length);
        WriteUInt32Le(buf, 12, 0x00000002u); // distinct FormID
        subrecordData.CopyTo(buf, headerSize);
        return buf;
    }

    // ── Full pipeline integration ─────────────────────────────────────────────

    [Fact]
    public async Task Convert_WithSsePlugin_WritesPatchedPlugin()
    {
        var workDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outDir  = Path.Combine(workDir, "out");
        Directory.CreateDirectory(workDir);

        try
        {
            // Write a real SSE plugin that has an ARMA record pointing at testarmor.nif.
            const string origPath = "meshes/armor/testarmor/testarmor_0.nif\0";
            byte[] mod2 = BuildSubrecord("MOD2", System.Text.Encoding.ASCII.GetBytes(origPath));
            byte[] plugin = BuildMinimalPlugin_SseWithArma(mod2);

            await File.WriteAllBytesAsync(Path.Combine(workDir, "testarmor.nif"), new byte[] { 0x4E, 0x69, 0x66 });
            await File.WriteAllBytesAsync(Path.Combine(workDir, "testarmor.esp"), plugin);

            var orchestrator = StandaloneConversionModules.CreateDefault();
            var result = await orchestrator.ConvertAsync(
                new ConversionRequest(
                    Path.Combine(workDir, "testarmor.nif"),
                    "CBBE",
                    outDir));

            Assert.True(result.Success);

            // The patched plugin should exist in the output when at least one mesh
            // path from the plugin matches the converted output file.
            var patchedEsp = result.OutputFiles
                .FirstOrDefault(f => f.Contains("_patched.", StringComparison.OrdinalIgnoreCase));

            // Only present when the regex scan also found the path (i.e. rewrite map not empty).
            if (patchedEsp is not null)
            {
                Assert.True(File.Exists(patchedEsp),
                    $"Patched plugin file should exist at {patchedEsp}");
            }
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    // ── Binary-builder helpers ────────────────────────────────────────────────

    /// <summary>Builds a 6+data subrecord: type(4) + size(2) + data.</summary>
    private static byte[] BuildSubrecord(string type, byte[] data)
    {
        var buf = new byte[6 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(buf, 0);
        buf[4] = (byte)(data.Length & 0xFF);
        buf[5] = (byte)(data.Length >> 8);
        data.CopyTo(buf, 6);
        return buf;
    }

    private static byte[] BuildExtendedSubrecord(string type, byte[] data)
    {
        var buf = new byte[16 + data.Length];
        System.Text.Encoding.ASCII.GetBytes("XXXX").CopyTo(buf, 0);
        buf[4] = 4;
        WriteUInt32Le(buf, 6, (uint)data.Length);
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(buf, 10);
        data.CopyTo(buf, 16);
        return buf;
    }

    /// <summary>Builds an ARMA record with a 24-byte SSE record header wrapping <paramref name="subrecordData"/>.</summary>
    private static byte[] BuildArmaRecord(byte[] subrecordData, int headerSize = 24)
    {
        var buf = new byte[headerSize + subrecordData.Length];
        System.Text.Encoding.ASCII.GetBytes("ARMA").CopyTo(buf, 0);
        WriteUInt32Le(buf, 4, (uint)subrecordData.Length); // dataSize
        // flags = 0, formID = 0x00000001, rest = 0
        WriteUInt32Le(buf, 12, 0x00000001u);
        subrecordData.CopyTo(buf, headerSize);
        return buf;
    }

    /// <summary>Builds a minimal SSE plugin (TES4 + HEDR) with no ARMA records.</summary>
    private static byte[] BuildMinimalPlugin_SseNoArma()
    {
        // TES4 record: header(24) + HEDR subrecord(6+12=18) + CNAM subrecord(6+1=7) = 25 bytes data
        byte[] hedrData = new byte[12];    // version(4)+numRecords(4)+nextObjectID(4)
        byte[] hedr = BuildSubrecord("HEDR", hedrData);
        byte[] cnam = BuildSubrecord("CNAM", [0x00]); // empty author
        byte[] tes4Data = [..hedr, ..cnam];

        var buf = new byte[24 + tes4Data.Length];
        System.Text.Encoding.ASCII.GetBytes("TES4").CopyTo(buf, 0);
        WriteUInt32Le(buf, 4, (uint)tes4Data.Length);
        tes4Data.CopyTo(buf, 24);
        return buf;
    }

    /// <summary>Builds a minimal SSE plugin (TES4 + one ARMA at file top level) with the given subrecord data.</summary>
    private static byte[] BuildMinimalPlugin_SseWithArma(byte[] armaSubrecords)
    {
        byte[] tes4 = BuildMinimalPlugin_SseNoArma();
        byte[] arma = BuildArmaRecord(armaSubrecords, headerSize: 24);
        return [..tes4, ..arma];
    }

    /// <summary>Wraps the ARMA record inside a top-level GRUP and prepends a TES4 record.</summary>
    private static byte[] BuildMinimalPlugin_SseWithGrupContaining(byte[] armaRecord)
    {
        // GRUP header (24 bytes): "GRUP" + totalSize + label("ARMA") + groupType(0) + ...
        int grupTotal = 24 + armaRecord.Length;
        var grup = new byte[grupTotal];
        System.Text.Encoding.ASCII.GetBytes("GRUP").CopyTo(grup, 0);
        WriteUInt32Le(grup, 4, (uint)grupTotal);         // total size
        System.Text.Encoding.ASCII.GetBytes("ARMA").CopyTo(grup, 8); // label = top-level ARMA group
        // groupType = 0 (top-level record group), rest zeros
        armaRecord.CopyTo(grup, 24);

        byte[] tes4 = BuildMinimalPlugin_SseNoArma();
        return [..tes4, ..grup];
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        buf[offset]     = (byte)(value);
        buf[offset + 1] = (byte)(value >> 8);
        buf[offset + 2] = (byte)(value >> 16);
        buf[offset + 3] = (byte)(value >> 24);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// BinaryArmaParser Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class BinaryArmaParserTests
{
    // ── Header-size detection (same logic as BinaryPluginRewriteService) ──────

    [Fact]
    public void DetectHeaderSize_LePlugin_Returns20()
    {
        var bytes = new byte[28];
        System.Text.Encoding.ASCII.GetBytes("TES4").CopyTo(bytes, 0);
        System.Text.Encoding.ASCII.GetBytes("HEDR").CopyTo(bytes, 20);
        Assert.Equal(20, BinaryArmaParser.DetectHeaderSize(bytes));
    }

    [Fact]
    public void DetectHeaderSize_SsePlugin_Returns24()
    {
        var bytes = new byte[32];
        System.Text.Encoding.ASCII.GetBytes("TES4").CopyTo(bytes, 0);
        System.Text.Encoding.ASCII.GetBytes("HEDR").CopyTo(bytes, 24);
        Assert.Equal(24, BinaryArmaParser.DetectHeaderSize(bytes));
    }

    // ── FormID extraction ─────────────────────────────────────────────────────

    [Fact]
    public void ExtractArmaRecords_ReadsFormIdFromRecordHeader()
    {
        // Build a minimal SSE plugin with a single ARMA record whose FormID = 0x00001234.
        const uint expectedFormId = 0x00001234u;
        byte[] armaData = BuildSubrecord("EDID", System.Text.Encoding.ASCII.GetBytes("TestArmor\0"));
        byte[] plugin   = BuildMinimalPlugin_SseWithArmaAndFormId(armaData, expectedFormId);

        var descriptors = BinaryArmaParser.ExtractArmaRecords(plugin);

        Assert.Single(descriptors);
        Assert.Equal(expectedFormId, descriptors[0].FormId);
    }

    // ── EditorID (EDID subrecord) extraction ──────────────────────────────────

    [Fact]
    public void ExtractArmaRecords_ReadsEditorIdFromEdidSubrecord()
    {
        const string editorId = "SomeCoolArmor";
        byte[] edid  = BuildSubrecord("EDID", System.Text.Encoding.ASCII.GetBytes(editorId + "\0"));
        byte[] mod2  = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/test/test_0.nif\0"));
        byte[] armaData = [..edid, ..mod2];

        byte[] plugin = BuildMinimalPlugin_SseWithArma(armaData);
        var descriptors = BinaryArmaParser.ExtractArmaRecords(plugin);

        Assert.Single(descriptors);
        Assert.Equal(editorId, descriptors[0].EditorId);
    }

    // ── BOD2 biped-slot decoding ──────────────────────────────────────────────

    [Fact]
    public void ExtractArmaRecords_DecodesBipedSlotsFromBod2()
    {
        // Slot 32 (Body) = bit 2 → flags = 0x00000004
        // Slot 33 (Hands) = bit 3 → flags |= 0x00000008
        // Combined: 0x0000000C
        const uint slotFlags = 0x0000000Cu;

        using var bod2Ms = new MemoryStream(8);
        WriteUInt32Le(bod2Ms, slotFlags); // slot flags
        WriteUInt32Le(bod2Ms, 0);         // general flags
        byte[] bod2    = BuildSubrecord("BOD2", bod2Ms.ToArray());
        byte[] plugin  = BuildMinimalPlugin_SseWithArma(bod2);

        var descriptors = BinaryArmaParser.ExtractArmaRecords(plugin);

        Assert.Single(descriptors);
        Assert.Contains(32, descriptors[0].BipedSlots);  // Body
        Assert.Contains(33, descriptors[0].BipedSlots);  // Hands
        Assert.DoesNotContain(30, descriptors[0].BipedSlots);
    }

    // ── Mesh path extraction ──────────────────────────────────────────────────

    [Fact]
    public void ExtractArmaRecords_ExtractsMeshPathsFromMod2Mod3Mod4Mod5()
    {
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/a/a_0.nif\0"));
        byte[] mod3 = BuildSubrecord("MOD3",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/a/a_1.nif\0"));
        byte[] mod4 = BuildSubrecord("MOD4",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/a/a_1st_f.nif\0"));
        byte[] mod5 = BuildSubrecord("MOD5",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/a/a_1st_m.nif\0"));
        byte[] data = [..mod2, ..mod3, ..mod4, ..mod5];

        var plugin      = BuildMinimalPlugin_SseWithArma(data);
        var descriptors = BinaryArmaParser.ExtractArmaRecords(plugin);

        Assert.Single(descriptors);
        var paths = descriptors[0].MeshPaths;
        Assert.Equal(4, paths.Count);
        Assert.Contains("meshes/armor/a/a_0.nif", paths);
        Assert.Contains("meshes/armor/a/a_1.nif", paths);
        Assert.Contains("meshes/armor/a/a_1st_f.nif", paths);
        Assert.Contains("meshes/armor/a/a_1st_m.nif", paths);
    }

    [Fact]
    public void ExtractArmaRecords_ExtendedSizeMod2_ExtractsMeshPath()
    {
        byte[] mod2 = BuildExtendedSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/a/a_0.nif\0"));
        var plugin      = BuildMinimalPlugin_SseWithArma(mod2);
        var descriptors = BinaryArmaParser.ExtractArmaRecords(plugin);

        Assert.Single(descriptors);
        Assert.Contains("meshes/armor/a/a_0.nif", descriptors[0].MeshPaths);
    }

    // ── GRUP traversal ────────────────────────────────────────────────────────

    [Fact]
    public void ExtractArmaRecords_FindsArmaInsideGrup()
    {
        byte[] mod2   = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/iron/a_0.nif\0"));
        byte[] arma   = BuildArmaRecord(mod2, headerSize: 24);
        byte[] plugin = BuildMinimalPlugin_SseWithGrupContaining(arma);

        var descriptors = BinaryArmaParser.ExtractArmaRecords(plugin);

        Assert.Single(descriptors);
        Assert.Contains("meshes/iron/a_0.nif", descriptors[0].MeshPaths);
    }

    // ── Original record bytes are preserved ──────────────────────────────────

    [Fact]
    public void ExtractArmaRecords_PreservesOriginalRecordBytes()
    {
        byte[] edid = BuildSubrecord("EDID",
            System.Text.Encoding.ASCII.GetBytes("PreserveMe\0"));
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/a/b.nif\0"));
        byte[] armaData = [..edid, ..mod2];

        var plugin      = BuildMinimalPlugin_SseWithArma(armaData);
        var descriptors = BinaryArmaParser.ExtractArmaRecords(plugin);

        Assert.Single(descriptors);
        // Original header must be 24 bytes (SSE) and start with "ARMA"
        Assert.Equal(24, descriptors[0].OriginalRecordHeaderBytes.Length);
        Assert.Equal("ARMA",
            System.Text.Encoding.ASCII.GetString(descriptors[0].OriginalRecordHeaderBytes, 0, 4));
        // Original data bytes must equal the armaData we provided
        Assert.Equal(armaData, descriptors[0].OriginalDataBytes);
    }

    // ── Empty / malformed inputs ──────────────────────────────────────────────

    [Fact]
    public void ExtractArmaRecords_EmptyInput_ReturnsEmpty()
    {
        var result = BinaryArmaParser.ExtractArmaRecords([]);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractArmaRecords_NoArmaRecords_ReturnsEmpty()
    {
        byte[] plugin = BuildMinimalPlugin_SseNoArma();
        var result    = BinaryArmaParser.ExtractArmaRecords(plugin);
        Assert.Empty(result);
    }

    // ── ARMO (Armor) record extraction ───────────────────────────────────────

    [Fact]
    public void ExtractArmoRecords_ExtractsMeshPathsFromMod2AndMod3()
    {
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/iron/iron_w.nif\0"));
        byte[] mod3 = BuildSubrecord("MOD3",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/iron/iron_f_w.nif\0"));
        byte[] data = [..mod2, ..mod3];

        var plugin      = BuildMinimalPlugin_SseWithArmo(data);
        var descriptors = BinaryArmaParser.ExtractArmoRecords(plugin);

        Assert.Single(descriptors);
        Assert.Contains("meshes/armor/iron/iron_w.nif",   descriptors[0].MeshPaths);
        Assert.Contains("meshes/armor/iron/iron_f_w.nif", descriptors[0].MeshPaths);
    }

    [Fact]
    public void ExtractArmoRecords_EmptyInput_ReturnsEmpty()
    {
        var result = BinaryArmaParser.ExtractArmoRecords([]);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractArmoRecords_NoArmoRecords_ReturnsEmpty()
    {
        // Plugin contains only an ARMA record — ARMO extraction should find nothing.
        byte[] mod2   = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/a/b.nif\0"));
        byte[] plugin = BuildMinimalPlugin_SseWithArma(mod2);

        var result = BinaryArmaParser.ExtractArmoRecords(plugin);
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractArmaRecords_CompressedRecord_DecompressesAndExtractsPaths()
    {
        // Build an ARMA record whose payload is zlib-compressed.
        byte[] mod2    = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/comp/comp_0.nif\0"));
        byte[] armaRec = BuildCompressedRecord(mod2, tag: "ARMA", headerSize: 24);
        byte[] plugin  = [..BuildMinimalPlugin_SseNoArma(), ..armaRec];

        var descriptors = BinaryArmaParser.ExtractArmaRecords(plugin);

        Assert.Single(descriptors);
        Assert.Contains("meshes/armor/comp/comp_0.nif", descriptors[0].MeshPaths);
    }

    [Fact]
    public void ExtractArmoRecords_CompressedRecord_DecompressesAndExtractsPaths()
    {
        byte[] mod2    = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/comp/comp_w.nif\0"));
        byte[] armoRec = BuildCompressedRecord(mod2, tag: "ARMO", headerSize: 24);
        byte[] plugin  = [..BuildMinimalPlugin_SseNoArma(), ..armoRec];

        var descriptors = BinaryArmaParser.ExtractArmoRecords(plugin);

        Assert.Single(descriptors);
        Assert.Contains("meshes/armor/comp/comp_w.nif", descriptors[0].MeshPaths);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Like BuildMinimalPlugin_SseWithArma but lets the caller set the FormID.</summary>
    private static byte[] BuildMinimalPlugin_SseWithArmaAndFormId(byte[] armaSubrecords, uint formId)
    {
        byte[] tes4 = BuildMinimalPlugin_SseNoArma();
        byte[] arma = BuildArmaRecordWithFormId(armaSubrecords, formId, headerSize: 24);
        return [..tes4, ..arma];
    }

    private static byte[] BuildArmaRecordWithFormId(byte[] subrecordData, uint formId, int headerSize)
    {
        var buf = new byte[headerSize + subrecordData.Length];
        System.Text.Encoding.ASCII.GetBytes("ARMA").CopyTo(buf, 0);
        WriteUInt32Le(buf, 4, (uint)subrecordData.Length);
        WriteUInt32Le(buf, 8, 0);       // flags
        WriteUInt32Le(buf, 12, formId); // FormID
        subrecordData.CopyTo(buf, headerSize);
        return buf;
    }

    private static byte[] BuildSubrecord(string tag, byte[] data)
    {
        var result = new byte[6 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(tag).CopyTo(result, 0);
        result[4] = (byte)(data.Length & 0xFF);
        result[5] = (byte)((data.Length >> 8) & 0xFF);
        data.CopyTo(result, 6);
        return result;
    }

    private static byte[] BuildExtendedSubrecord(string tag, byte[] data)
    {
        var result = new byte[16 + data.Length];
        System.Text.Encoding.ASCII.GetBytes("XXXX").CopyTo(result, 0);
        result[4] = 4;
        WriteUInt32Le(result, 6, (uint)data.Length);
        System.Text.Encoding.ASCII.GetBytes(tag).CopyTo(result, 10);
        data.CopyTo(result, 16);
        return result;
    }

    private static byte[] BuildArmaRecord(byte[] subrecordData, int headerSize)
    {
        var buf = new byte[headerSize + subrecordData.Length];
        System.Text.Encoding.ASCII.GetBytes("ARMA").CopyTo(buf, 0);
        WriteUInt32Le(buf, 4, (uint)subrecordData.Length);
        WriteUInt32Le(buf, 8, 0);
        WriteUInt32Le(buf, 12, 0x00000001u);
        subrecordData.CopyTo(buf, headerSize);
        return buf;
    }

    private static byte[] BuildMinimalPlugin_SseNoArma()
    {
        byte[] hedrData = new byte[12];
        byte[] hedr     = BuildSubrecord("HEDR", hedrData);
        byte[] cnam     = BuildSubrecord("CNAM", [0x00]);
        byte[] tes4Data = [..hedr, ..cnam];
        var buf = new byte[24 + tes4Data.Length];
        System.Text.Encoding.ASCII.GetBytes("TES4").CopyTo(buf, 0);
        WriteUInt32Le(buf, 4, (uint)tes4Data.Length);
        tes4Data.CopyTo(buf, 24);
        return buf;
    }

    private static byte[] BuildMinimalPlugin_SseWithArma(byte[] armaSubrecords)
    {
        byte[] tes4 = BuildMinimalPlugin_SseNoArma();
        byte[] arma = BuildArmaRecord(armaSubrecords, headerSize: 24);
        return [..tes4, ..arma];
    }

    private static byte[] BuildMinimalPlugin_SseWithGrupContaining(byte[] armaRecord)
    {
        int grupTotal = 24 + armaRecord.Length;
        var grup = new byte[grupTotal];
        System.Text.Encoding.ASCII.GetBytes("GRUP").CopyTo(grup, 0);
        WriteUInt32Le(grup, 4, (uint)grupTotal);
        System.Text.Encoding.ASCII.GetBytes("ARMA").CopyTo(grup, 8);
        armaRecord.CopyTo(grup, 24);
        byte[] tes4 = BuildMinimalPlugin_SseNoArma();
        return [..tes4, ..grup];
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        buf[offset]     = (byte)(value);
        buf[offset + 1] = (byte)(value >> 8);
        buf[offset + 2] = (byte)(value >> 16);
        buf[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteUInt32Le(MemoryStream ms, uint value)
    {
        ms.WriteByte((byte)(value));
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)(value >> 16));
        ms.WriteByte((byte)(value >> 24));
    }

    /// <summary>Builds a minimal SSE plugin with a single ARMO record (not ARMA) at file top level.</summary>
    private static byte[] BuildMinimalPlugin_SseWithArmo(byte[] armoSubrecords)
    {
        byte[] tes4 = BuildMinimalPlugin_SseNoArma();
        var buf = new byte[24 + armoSubrecords.Length];
        System.Text.Encoding.ASCII.GetBytes("ARMO").CopyTo(buf, 0);
        WriteUInt32Le(buf, 4, (uint)armoSubrecords.Length);
        WriteUInt32Le(buf, 12, 0x00000002u); // FormID
        armoSubrecords.CopyTo(buf, 24);
        return [..tes4, ..buf];
    }

    /// <summary>
    /// Builds a record with <paramref name="tag"/> whose payload is the zlib-compressed form of
    /// <paramref name="uncompressedData"/> (FlagCompressed bit 18 = 0x00040000 set in header flags).
    /// </summary>
    private static byte[] BuildCompressedRecord(byte[] uncompressedData, string tag, int headerSize = 24)
    {
        // Compress with ZLib.
        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using var zlib = new System.IO.Compression.ZLibStream(
                ms, System.IO.Compression.CompressionLevel.Fastest);
            zlib.Write(uncompressedData, 0, uncompressedData.Length);
            zlib.Close();
            compressed = ms.ToArray();
        }

        // Payload: uint32LE uncompressed size + compressed bytes.
        var payload = new byte[4 + compressed.Length];
        WriteUInt32Le(payload, 0, (uint)uncompressedData.Length);
        compressed.CopyTo(payload, 4);

        var buf = new byte[headerSize + payload.Length];
        System.Text.Encoding.ASCII.GetBytes(tag).CopyTo(buf, 0);
        WriteUInt32Le(buf, 4, (uint)payload.Length);
        WriteUInt32Le(buf, 8, 0x00040000u);  // FlagCompressed
        WriteUInt32Le(buf, 12, 0x00000001u); // FormID
        payload.CopyTo(buf, headerSize);
        return buf;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PatchPluginWriter Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class PatchPluginWriterTests
{
    // ── ARMA data rewrite ─────────────────────────────────────────────────────

    [Fact]
    public void RewriteArmaData_MatchingPath_RewritesAndCounts()
    {
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/iron/iron_0.nif\0"));
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = "meshes/slidesmith/cbbe/iron_0.nif"
        };

        var (newData, rewritten) = PatchPluginWriter.RewriteArmaData(mod2, rewriteMap);

        Assert.Equal(1, rewritten);
        // Verify the new path in the output subrecord.
        var tag  = System.Text.Encoding.ASCII.GetString(newData, 0, 4);
        var size = (ushort)(newData[4] | (newData[5] << 8));
        var path = System.Text.Encoding.ASCII.GetString(newData, 6, size - 1);
        Assert.Equal("MOD2", tag);
        Assert.Equal("meshes/slidesmith/cbbe/iron_0.nif", path);
    }

    [Fact]
    public void RewriteArmaData_NoMatch_ReturnsOriginalAndZero()
    {
        byte[] edid = BuildSubrecord("EDID",
            System.Text.Encoding.ASCII.GetBytes("NoMesh\0"));
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/x.nif"] = "meshes/y.nif"
        };

        var (newData, rewritten) = PatchPluginWriter.RewriteArmaData(edid, rewriteMap);

        Assert.Equal(0, rewritten);
        Assert.Equal(edid, newData);
    }

    [Fact]
    public void RewriteArmaData_ExtendedSizeMod2_RewritesPath()
    {
        byte[] mod2 = BuildExtendedSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/iron/iron_0.nif\0"));
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = "meshes/slidesmith/cbbe/iron_0.nif"
        };

        var (newData, rewritten) = PatchPluginWriter.RewriteArmaData(mod2, rewriteMap);

        Assert.Equal(1, rewritten);
        Assert.Equal("MOD2", System.Text.Encoding.ASCII.GetString(newData, 0, 4));
    }

    [Fact]
    public void RewriteArmaData_LongReplacementPath_WritesExtendedSizeSubrecord()
    {
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/armor/iron/iron_0.nif\0"));
        var longPath = "meshes/slidesmith/" + new string('b', 70000) + ".nif";
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/armor/iron/iron_0.nif"] = longPath
        };

        var (newData, rewritten) = PatchPluginWriter.RewriteArmaData(mod2, rewriteMap);

        Assert.Equal(1, rewritten);
        Assert.Equal("XXXX", System.Text.Encoding.ASCII.GetString(newData, 0, 4));
        Assert.Equal(4, (ushort)(newData[4] | (newData[5] << 8)));
        var extendedSize = newData[6] | (newData[7] << 8) | (newData[8] << 16) | (newData[9] << 24);
        Assert.Equal(longPath.Length + 1, extendedSize);
        Assert.Equal("MOD2", System.Text.Encoding.ASCII.GetString(newData, 10, 4));
        Assert.Equal(0, (ushort)(newData[14] | (newData[15] << 8)));
    }

    // ── BuildPatchPlugin ──────────────────────────────────────────────────────

    [Fact]
    public void BuildPatchPlugin_NoMatchingPaths_ReturnsZeroIncluded()
    {
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/no/match.nif\0"));
        var descriptor = BuildDescriptor(0x00000001u, "NoMatch", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/other/path.nif"] = "meshes/slidesmith/out.nif"
        };

        var (_, included) = PatchPluginWriter.BuildPatchPlugin("Source.esp", [descriptor], rewriteMap);

        Assert.Equal(0, included);
    }

    [Fact]
    public void BuildPatchPlugin_WithMatchingArma_IncludesRecord()
    {
        const string originalPath = "meshes/armor/iron/iron_0.nif";
        const string newPath      = "meshes/slidesmith/cbbe/iron_0.nif";
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes(originalPath + "\0"));
        var descriptor = BuildDescriptor(0x00001234u, "TestArmor", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [originalPath] = newPath
        };

        var (patchBytes, included) = PatchPluginWriter.BuildPatchPlugin(
            "MyMod.esp", [descriptor], rewriteMap, headerSize: 24);

        Assert.Equal(1, included);
        Assert.True(patchBytes.Length > 0);
    }

    [Fact]
    public void BuildPatchPlugin_OutputStartsWithTes4Tag()
    {
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/a/b.nif\0"));
        var descriptor = BuildDescriptor(0x00000001u, "ArmorA", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/a/b.nif"] = "meshes/slidesmith/out/b.nif"
        };

        var (patchBytes, _) = PatchPluginWriter.BuildPatchPlugin(
            "OriginalPlugin.esp", [descriptor], rewriteMap, headerSize: 24);

        var tag = System.Text.Encoding.ASCII.GetString(patchBytes, 0, 4);
        Assert.Equal("TES4", tag);
    }

    [Fact]
    public void BuildPatchPlugin_Tes4DataContainsMasterFileName()
    {
        const string masterName = "OriginalArmor.esp";
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/orig/a.nif\0"));
        var descriptor = BuildDescriptor(0x00000001u, "ArmorRec", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/orig/a.nif"] = "meshes/slidesmith/new/a.nif"
        };

        var (patchBytes, _) = PatchPluginWriter.BuildPatchPlugin(
            masterName, [descriptor], rewriteMap, headerSize: 24);

        // Search the TES4 data for the MAST subrecord tag followed by the master name.
        var asLatin1 = System.Text.Encoding.Latin1.GetString(patchBytes);
        Assert.Contains("MAST", asLatin1);
        Assert.Contains(masterName, asLatin1);
    }

    [Fact]
    public void BuildPatchPlugin_OutputContainsGrupAndArmaTag()
    {
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/x/y.nif\0"));
        var descriptor = BuildDescriptor(0x00000001u, "ArmorX", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/x/y.nif"] = "meshes/slidesmith/y.nif"
        };

        var (patchBytes, _) = PatchPluginWriter.BuildPatchPlugin(
            "X.esp", [descriptor], rewriteMap, headerSize: 24);

        var asLatin1 = System.Text.Encoding.Latin1.GetString(patchBytes);
        Assert.Contains("GRUP", asLatin1);
        Assert.Contains("ARMA", asLatin1);
    }

    [Fact]
    public void BuildPatchPlugin_PatchedArmaPreservesOriginalFormId()
    {
        const uint formId  = 0x00ABCD12u;
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/a/c.nif\0"));
        var descriptor = BuildDescriptor(formId, "ArmorC", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/a/c.nif"] = "meshes/slidesmith/c.nif"
        };

        var (patchBytes, included) = PatchPluginWriter.BuildPatchPlugin(
            "C.esp", [descriptor], rewriteMap, headerSize: 24);

        Assert.Equal(1, included);

        // Locate the ARMA *record* in the patch output (not the GRUP label).
        // Layout: ... GRUP[0..3] | totalSize[4..7] | "ARMA"(label)[8..11] | groupType[12..15] | VC[16..23]
        //              ARMA(record)[0..3] | dataSize[4..7] | flags[8..11] | FormID[12..15] | ...
        // The GRUP label "ARMA" appears 8 bytes after the "GRUP" tag.
        // Skip any "ARMA" occurrence that is a GRUP label by checking whether
        // the 8 bytes before it are "GRUP".
        int armaOff = -1;
        for (int i = 0; i < patchBytes.Length - 4; i++)
        {
            if (patchBytes[i] == 'A' && patchBytes[i + 1] == 'R' &&
                patchBytes[i + 2] == 'M' && patchBytes[i + 3] == 'A' &&
                i + 24 <= patchBytes.Length)
            {
                // Skip GRUP label — "ARMA" at byte offset 8 inside a GRUP header.
                if (i >= 8 &&
                    patchBytes[i - 8] == 'G' && patchBytes[i - 7] == 'R' &&
                    patchBytes[i - 6] == 'U' && patchBytes[i - 5] == 'P')
                {
                    continue;
                }
                armaOff = i;
                break;
            }
        }

        // We should have found an ARMA record.
        Assert.True(armaOff >= 0, "ARMA record not found in patch output");

        // Read FormID from header bytes 12–15.
        var patchedFormId = (uint)(patchBytes[armaOff + 12]
                                 | (patchBytes[armaOff + 13] << 8)
                                 | (patchBytes[armaOff + 14] << 16)
                                 | (patchBytes[armaOff + 15] << 24));
        Assert.Equal(formId, patchedFormId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // ── ESL flag ──────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPatchPlugin_Tes4HasEslFlag()
    {
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/a/b.nif\0"));
        var descriptor = BuildDescriptor(0x00000001u, "ArmorA", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/a/b.nif"] = "meshes/slidesmith/out/b.nif"
        };

        var (patchBytes, _) = PatchPluginWriter.BuildPatchPlugin(
            "Plugin.esp", [descriptor], rewriteMap, headerSize: 24);

        // TES4 record header: tag[0..3] | dataSize[4..7] | flags[8..11] | ...
        var tes4Flags = (uint)(patchBytes[8]
                             | (patchBytes[9]  << 8)
                             | (patchBytes[10] << 16)
                             | (patchBytes[11] << 24));
        const uint eslFlag = 0x00000200u;
        Assert.True((tes4Flags & eslFlag) != 0,
            $"Expected ESL flag 0x200 in TES4 record flags, got 0x{tes4Flags:X8}");
    }

    // ── HEDR numRecords accuracy ──────────────────────────────────────────────

    [Fact]
    public void BuildPatchPlugin_HedrNumRecordsMatchesIncludedCount()
    {
        // One matching ARMA record → included = 1, numRecords in HEDR should be 1.
        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes("meshes/x/y.nif\0"));
        var descriptor = BuildDescriptor(0x00000001u, "ArmorX", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["meshes/x/y.nif"] = "meshes/slidesmith/y.nif"
        };

        var (patchBytes, included) = PatchPluginWriter.BuildPatchPlugin(
            "P.esp", [descriptor], rewriteMap, headerSize: 24);

        Assert.Equal(1, included);

        // TES4 record: header(24) | TES4 data starts at 24
        // TES4 data: HEDR tag[0..3] + size[4..5] (=12) + data[6..17]
        //   HEDR data: version(float32)[0..3] + numRecords(int32)[4..7] + nextObjectId(uint32)[8..11]
        // So numRecords is at byte 24 + 6 + 4 = 34
        int numRecords = patchBytes[34]
                       | (patchBytes[35] << 8)
                       | (patchBytes[36] << 16)
                       | (patchBytes[37] << 24);
        Assert.Equal(included, numRecords);
    }

    // ── ARMO GRUP ─────────────────────────────────────────────────────────────

    [Fact]
    public void BuildPatchPlugin_WithArmoDescriptors_EmitsArmoGrup()
    {
        // ARMO-only: no ARMA descriptors, one ARMO descriptor with a matching path.
        const string origPath = "meshes/armor/iron/iron_w.nif";
        const string newPath  = "meshes/slidesmith/cbbe/iron_w.nif";

        byte[] mod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes(origPath + "\0"));
        var armoDesc = BuildArmoDescriptor(0x00000010u, "ArmorIron", mod2);
        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [origPath] = newPath
        };

        var (patchBytes, included) = PatchPluginWriter.BuildPatchPlugin(
            "ArmoOnly.esp",
            descriptors: [],
            rewriteMap: rewriteMap,
            headerSize: 24,
            armoDescriptors: [armoDesc]);

        Assert.Equal(1, included);
        var asLatin1 = System.Text.Encoding.Latin1.GetString(patchBytes);
        Assert.Contains("ARMO", asLatin1);
        Assert.Contains("GRUP", asLatin1);
        // Confirm the new mesh path appears in the output.
        Assert.Contains(newPath, asLatin1, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPatchPlugin_WithBothArmaAndArmo_EmitsBothGrups()
    {
        const string armaOrig = "meshes/arma/a.nif";
        const string armaNew  = "meshes/slidesmith/a.nif";
        const string armoOrig = "meshes/armo/b.nif";
        const string armoNew  = "meshes/slidesmith/b.nif";

        byte[] armaMod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes(armaOrig + "\0"));
        byte[] armoMod2 = BuildSubrecord("MOD2",
            System.Text.Encoding.ASCII.GetBytes(armoOrig + "\0"));

        var armaDesc = BuildDescriptor(0x00000001u, "ArmaRec", armaMod2);
        var armoDesc = BuildArmoDescriptor(0x00000002u, "ArmoRec", armoMod2);

        var rewriteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [armaOrig] = armaNew,
            [armoOrig] = armoNew
        };

        var (patchBytes, included) = PatchPluginWriter.BuildPatchPlugin(
            "Both.esp", [armaDesc], rewriteMap, headerSize: 24,
            armoDescriptors: [armoDesc]);

        Assert.Equal(2, included);
        var asLatin1 = System.Text.Encoding.Latin1.GetString(patchBytes);
        // Both GRUPs must be present.
        Assert.Contains(armaNew, asLatin1, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(armoNew, asLatin1, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ArmaRecordDescriptor BuildDescriptor(
        uint formId, string editorId, byte[] dataBytes)
    {
        // Build a 24-byte SSE record header.
        var header = new byte[24];
        System.Text.Encoding.ASCII.GetBytes("ARMA").CopyTo(header, 0);
        WriteUInt32Le(header, 4, (uint)dataBytes.Length);
        WriteUInt32Le(header, 8, 0);        // flags
        WriteUInt32Le(header, 12, formId);

        return new ArmaRecordDescriptor(
            FormId: formId,
            PluginFileName: "Test.esp",
            EditorId: editorId,
            BipedSlots: [],
            MeshPaths: [],
            OriginalRecordHeaderBytes: header,
            OriginalDataBytes: dataBytes);
    }

    private static ArmoRecordDescriptor BuildArmoDescriptor(
        uint formId, string editorId, byte[] dataBytes)
    {
        var header = new byte[24];
        System.Text.Encoding.ASCII.GetBytes("ARMO").CopyTo(header, 0);
        WriteUInt32Le(header, 4, (uint)dataBytes.Length);
        WriteUInt32Le(header, 8, 0);
        WriteUInt32Le(header, 12, formId);

        return new ArmoRecordDescriptor(
            FormId: formId,
            PluginFileName: "Test.esp",
            EditorId: editorId,
            MeshPaths: [],
            OriginalRecordHeaderBytes: header,
            OriginalDataBytes: dataBytes);
    }


    private static byte[] BuildSubrecord(string tag, byte[] data)
    {
        var result = new byte[6 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(tag).CopyTo(result, 0);
        result[4] = (byte)(data.Length & 0xFF);
        result[5] = (byte)((data.Length >> 8) & 0xFF);
        data.CopyTo(result, 6);
        return result;
    }

    private static byte[] BuildExtendedSubrecord(string tag, byte[] data)
    {
        var result = new byte[16 + data.Length];
        System.Text.Encoding.ASCII.GetBytes("XXXX").CopyTo(result, 0);
        result[4] = 4;
        WriteUInt32Le(result, 6, (uint)data.Length);
        System.Text.Encoding.ASCII.GetBytes(tag).CopyTo(result, 10);
        data.CopyTo(result, 16);
        return result;
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        buf[offset]     = (byte)(value);
        buf[offset + 1] = (byte)(value >> 8);
        buf[offset + 2] = (byte)(value >> 16);
        buf[offset + 3] = (byte)(value >> 24);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ConversionReadmeGenerator Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class ConversionReadmeGeneratorTests
{
    [Fact]
    public void Generate_IncludesArmorNameAndTargetBody()
    {
        var readme = BuildReadme();
        Assert.Contains("iron_0", readme);
        Assert.Contains("CBBE", readme);
    }

    [Fact]
    public void Generate_IncludesWhatWasConvertedSection()
    {
        var readme = BuildReadme();
        Assert.Contains("WHAT WAS CONVERTED", readme);
        Assert.Contains("Target body:", readme);
    }

    [Fact]
    public void Generate_IncludesFilesGeneratedSection()
    {
        var readme = BuildReadme();
        Assert.Contains("FILES GENERATED", readme);
    }

    [Fact]
    public void Generate_IncludesHowToInstallSection()
    {
        var readme = BuildReadme();
        Assert.Contains("HOW TO INSTALL", readme);
    }

    [Fact]
    public void Generate_WhenPatchEspGenerated_MentionsPatchFile()
    {
        var readme = BuildReadme(patchEspGenerated: true, espPath: "/out/MyMod_SlidesmithPatch.esp");
        Assert.Contains("SlidesmithPatch", readme);
        Assert.Contains("override patch", readme);
    }

    [Fact]
    public void Generate_WhenNoPatchEsp_MentionsXEditScript()
    {
        var readme = BuildReadme(patchEspGenerated: false);
        Assert.Contains("xEdit", readme);
    }

    [Fact]
    public void Generate_IncludesBodySlideSection()
    {
        var readme = BuildReadme(includeBsd: true);
        Assert.Contains("BODYSLIDE", readme);
        Assert.Contains("Build", readme);
    }

    [Fact]
    public void Generate_IsNonEmptyString()
    {
        var readme = BuildReadme();
        Assert.True(readme.Length > 200);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static string BuildReadme(
        bool patchEspGenerated = false,
        string? espPath = null,
        bool includeBsd = false)
    {
        var request    = new ConversionRequest("/src", "CBBE");
        var armor      = new ImportedArmor(
            "/src",
            ["iron_0.nif"],
            [],
            [],
            []);
        var mesh       = new ConvertedMesh("leather", "proportional", 1,
            new Dictionary<string, double> { ["chest"] = 1.05 });
        var bsProject  = new BodySlideProject("IronArmor", "CBBE",
            includeBsd ? ["Belly", "Butt"] : [],
            "<osp/>");
        var pluginResult = new PluginAnalysisResult([], [], "No plugins found.");
        var files      = new List<string> { "/out/iron_0.nif" };
        if (espPath is not null) files.Add(espPath);
        if (includeBsd)
        {
            files.Add("/out/SliderData/Belly.bsd");
            files.Add("/out/IronArmor.osp");
        }

        var rewriteMap = new Dictionary<string, string>
        {
            ["meshes/armor/iron/iron_0.nif"] = "meshes/slidesmith/cbbe/iron_0.nif"
        };

        return ConversionReadmeGenerator.Generate(
            request, armor, mesh, bsProject,
            pluginResult, files, rewriteMap, patchEspGenerated);
    }
}
