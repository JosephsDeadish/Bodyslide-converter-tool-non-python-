using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

[Collection("NonParallel")]
public sealed class ConversionPerformanceRegressionTests
{
    [Fact]
    public void PayloadReuseSummary_DoesNotAllocateRetargetedVertexArrays()
    {
        var sliders = Enumerable.Range(0, 20).Select(index => $"Slider{index}").ToArray();
        var payloads = sliders.ToDictionary(slider => slider, slider =>
            new SourceMorphPayloadVariants(
                new SourceMorphPayload(
                    slider, false, "bsd", 2, [(1f, 0f, 0f), (2f, 0f, 0f)],
                    ShapeIdentityStatus: "verified", VertexOrderStatus: "verified", RetargetMapVerified: true),
                new SourceMorphPayload(
                    slider, true, "bsd", 2, [(2f, 0f, 0f), (3f, 0f, 0f)],
                    ShapeIdentityStatus: "verified", VertexOrderStatus: "verified", RetargetMapVerified: true)));
        var method = GetMethod("BuildPayloadReuseSummary");
        method.Invoke(null, [sliders, payloads, 100_000, null]);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var summary = (MorphPayloadReuseSummary)method.Invoke(null, [sliders, payloads, 100_000, null])!;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(40, summary.RequestedVariantCount);
        Assert.Equal(40, summary.RetargetedVariantCount);
        Assert.Equal(0, summary.FallbackVariantCount);
        Assert.True(allocated < 1_000_000, $"Metadata-only summary allocated {allocated} bytes.");
    }

    [Theory]
    [InlineData("unresolved", "verified")]
    [InlineData("verified", "unverified")]
    [InlineData("unresolved", "unverified")]
    public void PayloadReuseSummary_RejectsExactVertexCountWithoutShapeAndOrderProvenance(
        string shapeIdentityStatus,
        string vertexOrderStatus)
    {
        var payloads = new Dictionary<string, SourceMorphPayloadVariants>
        {
            ["Belly"] = new(
                new SourceMorphPayload(
                    "Belly", false, "bsd", 2, [(0.1f, 0f, 0f), (0.2f, 0f, 0f)], "Belly.bsd",
                    shapeIdentityStatus, vertexOrderStatus),
                new SourceMorphPayload(
                    "Belly", true, "bsd", 2, [(0.2f, 0f, 0f), (0.3f, 0f, 0f)], "Belly_1.bsd",
                    shapeIdentityStatus, vertexOrderStatus))
        };

        var summary = (MorphPayloadReuseSummary)GetMethod("BuildPayloadReuseSummary")
            .Invoke(null, [new[] { "Belly" }, payloads, 2, null])!;

        Assert.Equal(0, summary.ReusedVariantCount);
        Assert.Equal(0, summary.RetargetedVariantCount);
        Assert.Equal(2, summary.FallbackVariantCount);
        Assert.Equal(new[] { "Belly", "Belly_1" }, summary.FallbackVariants);
    }

    [Fact]
    public void PayloadReuseSummary_RejectsTopologyRetargetWithoutVerifiedRetargetMap()
    {
        var payloads = new Dictionary<string, SourceMorphPayloadVariants>
        {
            ["Belly"] = new(
                new SourceMorphPayload(
                    "Belly", false, "bsd", 2, [(0.1f, 0f, 0f), (0.2f, 0f, 0f)],
                    ShapeIdentityStatus: "verified", VertexOrderStatus: "verified"),
                null)
        };

        var summary = (MorphPayloadReuseSummary)GetMethod("BuildPayloadReuseSummary")
            .Invoke(null, [new[] { "Belly" }, payloads, 5, null])!;

        Assert.Equal(0, summary.ReusedVariantCount);
        Assert.Equal(0, summary.RetargetedVariantCount);
        Assert.Equal(2, summary.FallbackVariantCount);
    }

    [Theory]
    [InlineData("height")]
    [InlineData("glow")]
    [InlineData("roughness")]
    public void DerivedDds_EmitsValidSingleLevelHeader(string kind)
    {
        var source = LocalExportService.BuildSolidColorDds(32, 64, 192, 255, 8, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(8, 4), 0x2100F);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(28, 4), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(108, 4), 0x401008);

        Assert.True(DeriveTexture(kind, source, out var result));
        Assert.Equal(128 + 8 * 4 * 4, result.Length);
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(16, 4)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(12, 4)));
        Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(20, 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(28, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(8, 4)) & 0x20000);
        Assert.Equal(0x1000u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(108, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(112, 4)));
    }

    [Theory]
    [InlineData(88, 24u)]
    [InlineData(92, 0xFFu)]
    [InlineData(112, 0x200u)]
    public void DerivedDds_RejectsUnsupportedPixelOrSurfaceLayouts(int offset, uint value)
    {
        var source = LocalExportService.BuildSolidColorDds(32, 64, 192, 255);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(offset, 4), value);
        foreach (var kind in new[] { "height", "glow", "roughness" })
        {
            Assert.False(DeriveTexture(kind, source, out var result));
            Assert.Empty(result);
        }
    }

    [Theory]
    [InlineData("height")]
    [InlineData("glow")]
    [InlineData("roughness")]
    public void DerivedDds_HonorsCancellation(string kind)
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var source = LocalExportService.BuildSolidColorDds(32, 64, 192, 255);
        Assert.ThrowsAny<OperationCanceledException>(() => DeriveTexture(kind, source, out _, cancelled.Token));
    }

    [Fact]
    public async Task DerivableDdsRead_RejectsOversizedSourceBeforeAllocatingItsPixels()
    {
        var root = CreateRoot();
        try
        {
            var path = Path.Combine(root, "oversized.dds");
            var header = LocalExportService.BuildSolidColorDds(0, 0, 0, 255)[..128];
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), 8192);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), 8192);
            Assert.False(DdsTextureDerivation.CanDerive(header, out _, out _));
            File.WriteAllBytes(path, header);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write))
                stream.SetLength(128L + 8192L * 8192 * 4);
            var result = await (Task<byte[]?>)GetMethod("ReadDerivableDdsAsync")
                .Invoke(null, [path, CancellationToken.None])!;
            Assert.Null(result);
        }
        finally { Directory.Delete(root, true); }
    }

    private static bool DeriveTexture(string kind, byte[] source, out byte[] result, CancellationToken cancellationToken = default)
    {
        return kind switch
        {
            "height" => DdsTextureDerivation.TryDeriveHeightFromNormal(source, out result, cancellationToken),
            "glow" => DdsTextureDerivation.TryDeriveGlowFromDiffuse(source, out result, cancellationToken: cancellationToken),
            _ => DdsTextureDerivation.TryDeriveRoughnessFromSpecular(source, out result, cancellationToken)
        };
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PluginVerification_ChecksDiskEvenWhenMeshIsRecordedAsStaged(bool fileExists, bool recordedAsStaged)
    {
        var root = CreateRoot();
        try
        {
            const string rewritten = "meshes/slidesmith/cbbe/armor.nif";
            var destination = Path.Combine(root, "meshes", "slidesmith", "cbbe", "armor.nif");
            if (fileExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, new byte[64]);
            }
            var plan = new PluginRewritePlan(
                new Dictionary<string, string> { ["meshes/source.nif"] = rewritten },
                new Dictionary<string, string>(), [], [], 1);
            var staged = new HashSet<string>();
            if (recordedAsStaged) staged.Add(destination);
            var result = (PluginRewriteVerificationReport)GetMethod("BuildPluginRewriteVerificationReport")
                .Invoke(null, [plan, new PluginAnalysisResult([], [], ""), root, staged,
                    Array.Empty<string>(), new Dictionary<string, IReadOnlyList<string>>(),
                    Array.Empty<string>(), null])!;

            Assert.NotNull(result.MissingStagedMeshes);
            if (fileExists)
                Assert.Empty(result.MissingStagedMeshes);
            else
                Assert.Equal(rewritten, Assert.Single(result.MissingStagedMeshes));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void FixtureWriters_ReturnCanonicalPathsForNestedAssets()
    {
        var root = CreateRoot();
        try
        {
            Assert.Equal(Path.Combine(root, "plugins", "Armor.esp"),
                WritePlugin(root, "plugins/Armor.esp"));
            Assert.Equal(Path.Combine(root, "textures", "armor.dds"),
                WriteTexture(root, "textures/armor.dds"));
            Assert.Equal(Path.Combine(root, "meshes", "armor.nif"),
                WriteTextureNif(root, "meshes/armor.nif", ["textures/armor.dds"]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MorphDiagnostics_DescribePayloadsWithoutDuplicatingVertexArrays()
    {
        var root = CreateRoot();
        try
        {
            var nif = Path.Combine(root, "armor.nif");
            await File.WriteAllTextAsync(nif, "mesh");
            var deltas = Enumerable.Repeat((1f, 2f, 3f), 100_000).ToArray();
            var payload = new SourceMorphPayload("TestSlider", false, "bsd", deltas.Length, deltas, "TestSlider.bsd");
            var morphs = new MorphSet("low", "high", true, SliderCount: 1,
                ReusableSourceMorphPayloads: new Dictionary<string, SourceMorphPayloadVariants>
                {
                    ["TestSlider"] = new(payload)
                });
            var output = await ExportAsync(root, new ImportedArmor(root, [nif], [], [], []),
                new PluginAnalysisResult([], [], ""), new TextureSummary(0, [], [], []), morphs: morphs);

            foreach (var name in new[] { "morphs.json", "conversion-manifest.json" })
            {
                var path = Path.Combine(output, name);
                Assert.True(new FileInfo(path).Length < 30_000, name);
                using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                var summary = name == "morphs.json" ? json.RootElement : json.RootElement.GetProperty("Morphs");
                Assert.Equal(1, summary.GetProperty("SliderCount").GetInt32());
                var low = summary.GetProperty("SourceMorphPayloadCandidates").GetProperty("TestSlider").GetProperty("LowWeight");
                Assert.Equal(deltas.Length, low.GetProperty("DeltaCount").GetInt32());
                Assert.False(low.TryGetProperty("Deltas", out _));
                Assert.Equal("TestSlider.bsd", low.GetProperty("SourceAssetName").GetString());
                Assert.Equal("unresolved", low.GetProperty("ShapeIdentityStatus").GetString());
                Assert.Equal("unverified", low.GetProperty("VertexOrderStatus").GetString());
                Assert.Equal("blocked-shape-or-order-unverified", low.GetProperty("ReuseEligibility").GetString());
                Assert.Contains("must both be verified", summary.GetProperty("SourceMorphReusePolicy").GetString());
            }

            Assert.Same(deltas, payload.Deltas);
            Assert.Equal((1f, 2f, 3f), payload.Deltas[0]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExportIncludesLateQualityReportInFomodAndAcceptsGeneratedDependencyMap()
    {
        var root = CreateRoot();
        try
        {
            var nif = Path.Combine(root, "armor.nif");
            await File.WriteAllTextAsync(nif, "mesh");
            var output = await ExportAsync(root, new ImportedArmor(root, [nif], [], [], []),
                new PluginAnalysisResult([], [], ""), new TextureSummary(0, [], [], []));
            var config = System.Xml.Linq.XDocument.Load(Path.Combine(output, "fomod", "ModuleConfig.xml"));
            Assert.Contains(config.Descendants("file"), entry =>
                entry.Attribute("source")?.Value == "conversion-quality.json" &&
                entry.Attribute("destination")?.Value == "conversion-quality.json");
            var quality = await File.ReadAllTextAsync(Path.Combine(output, "conversion-quality.json"));
            Assert.DoesNotContain("fomod-missing-root-support-entry", quality);
            Assert.DoesNotContain("Report must contain a JSON object", quality);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void PluginVerification_AcceptsRepeatedNormalizedSourceReports()
    {
        var plan = new PluginRewritePlan(new Dictionary<string, string>(),
            new Dictionary<string, string> { ["meshes/armor.nif"] = "meshes/Armor.nif" }, [], [], 1);
        var reports = new[]
        {
            new NifSupportReport("meshes/Armor.nif", "supported", "test", 3, []),
            new NifSupportReport("meshes\\armor.nif", "unsupported", "missing-header", null, [])
        };

        var analysis = new PluginAnalysisResult(["Armor.esp"],
            [new PluginArmorAddon("Armor.esp [ESP]", ["meshes/armor.nif"], FormId: 1, OwningPluginFileName: "Armor.esp")],
            "", ArmorRecords:
            [new PluginArmorRecord("Armor.esp [ESP]", [], FormId: 2, LinkedArmorAddonFormIds: [1], OwningPluginFileName: "Armor.esp")]);
        var result = (PluginRewriteVerificationReport)GetMethod("BuildPluginRewriteVerificationReport").Invoke(null,
            [plan, analysis, Path.GetTempPath(),
             new HashSet<string>(), Array.Empty<string>(),
             new Dictionary<string, IReadOnlyList<string>>(), Array.Empty<string>(), reports])!;

        Assert.Contains("missing-header", Assert.Single(result.UnsupportedLinkedArmorAddonMeshes!));
    }

    [Fact]
    public async Task Analysis_ExcludesConflictingPluginIdentitiesAndDeduplicatesIdenticalPaths()
    {
        var root = CreateRoot();
        try
        {
            var first = WritePlugin(root, "variant-a/Armor.esp");
            var second = WritePlugin(root, "variant-b/ARMOR.ESP");
            var unique = WritePlugin(root, "Unique.esp");
            var armor = new ImportedArmor(root, [], [], [], [])
            {
                BatchPluginFiles = [first, first, second, unique, unique]
            };

            var result = await new BasicPluginAnalysisService().AnalyzeAsync(armor, "CBBE", CancellationToken.None);

            Assert.Equal("Armor.esp", Assert.Single(result.AmbiguousPlugins!), ignoreCase: true);
            Assert.Equal(2, result.ScannedPlugins.Count);
            Assert.StartsWith("Unique.esp", Assert.Single(result.ArmorAddons).RecordType);
            Assert.Contains(first, result.PatchGuidance);
            Assert.Contains(second, result.PatchGuidance);
            Assert.Contains("Select one compatible plugin variant", result.PatchGuidance);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Export_RejectsSameNameAlternativesEvenWithExternallySuppliedAnalysis()
    {
        var root = CreateRoot();
        try
        {
            var first = WritePlugin(root, "variant-a/Armor.esp");
            var second = WritePlugin(root, "variant-b/ARMOR.ESP");
            var unique = WritePlugin(root, "Unique.esp");
            var nif = Path.Combine(root, "armor_0.nif");
            File.WriteAllText(nif, "mesh");
            var armor = new ImportedArmor(root, [nif], [], [], [])
            {
                BatchPluginFiles = [first, first, second, unique, unique]
            };
            var analysis = new PluginAnalysisResult(
                ["Armor.esp [ESP]", "Unique.esp [ESP]"],
                [new PluginArmorAddon("Armor.esp [ESP]", ["meshes/armor_0.nif"]),
                 new PluginArmorAddon("Unique.esp [ESP]", ["meshes/armor_0.nif"])], "");

            var output = await ExportAsync(root, armor, analysis, new TextureSummary(0, [], [], []));

            Assert.DoesNotContain(Directory.EnumerateFiles(output, "*.esp"), path =>
                Path.GetFileName(path).StartsWith("Armor", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(output, "Unique.esp")));
            Assert.True(File.Exists(Path.Combine(output, "Unique_patched.esp")));
            var report = await File.ReadAllTextAsync(Path.Combine(output, "plugin-patches.json"));
            Assert.Contains("different source paths", report);
            Assert.Contains("Select one compatible", report);
            Assert.Contains(first.Replace("\\", "\\\\"), report);
        }
        finally
        {
            Directory.Delete(root, true);
            if (Directory.Exists(Path.Combine(root, "output") + ".reports"))
                Directory.Delete(Path.Combine(root, "output") + ".reports", true);
        }
    }

    [Theory]
    [InlineData(4096)]
    [InlineData(8192)]
    public async Task NeutralFallbacks_AreBoundedRegardlessOfDiffuseResolution(int resolution)
    {
        var root = CreateRoot();
        try
        {
            var diffuse = Path.Combine(root, "armor.dds");
            var header = LocalExportService.BuildSolidColorDds(0, 0, 0, 255)[..128];
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12, 4), resolution);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16, 4), resolution);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(84, 4), 0x31545844); // DXT1
            File.WriteAllBytes(diffuse, header);
            using (var stream = new FileStream(diffuse, FileMode.Open, FileAccess.Write))
                stream.SetLength(128L + (long)(resolution / 4) * (resolution / 4) * 8);
            var armor = new ImportedArmor(root, [], [diffuse, diffuse], [], []);
            var summary = MissingTextureSummary();
            var output = Path.Combine(root, "textures");
            Directory.CreateDirectory(output);

            var normalTask = (Task<IReadOnlyList<string>>)GetMethod("GenerateMissingNormalMapStubsAsync")
                .Invoke(null, [armor, summary, output, CancellationToken.None])!;
            Assert.Single(await normalTask);
            var auxTask = (Task)GetMethod("GenerateMissingAuxTextureStubsAsync")
                .Invoke(null, [armor, summary, output, CancellationToken.None])!;
            await auxTask;

            var maps = Directory.GetFiles(output, "*.dds");
            Assert.Equal(6, maps.Length);
            foreach (var map in maps)
            {
                var bytes = await File.ReadAllBytesAsync(map);
                Assert.Equal(192, bytes.Length);
                Assert.True(DdsTextureDerivation.TryReadDimensions(bytes, out var width, out var height));
                Assert.Equal(4, width);
                Assert.Equal(4, height);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BatchExport_UsesSupportSnapshotWithoutRescanningButStillCopiesStandaloneAssets()
    {
        var root = CreateRoot();
        try
        {
            var nif = Path.Combine(root, "armor.nif");
            File.WriteAllText(nif, "mesh");
            var material = Path.Combine(root, "armor.bgsm");
            var unexpected = Path.Combine(root, "added-after-snapshot.bgsm");
            File.WriteAllText(material, "material");
            using var context = new BatchPluginExportContext
            {
                SupportAssets = new LocalExportService.SupportAssetDiscoveryResult([], [material])
            };
            File.WriteAllText(unexpected, "not in batch snapshot");
            var armor = new ImportedArmor(root, [nif], [], [], []);

            var output = await ExportAsync(root, armor, new PluginAnalysisResult([], [], ""),
                new TextureSummary(0, [], [], []), context);

            Assert.Equal("material", await File.ReadAllTextAsync(Path.Combine(output, "armor.bgsm")));
            Assert.False(File.Exists(Path.Combine(output, "added-after-snapshot.bgsm")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("meshes", false)]
    [InlineData("meshes/armor", false)]
    [InlineData("meshes", true)]
    [InlineData("meshes/armor", true)]
    public async Task BatchFromMeshSubfolder_PreservesSiblingMaterialPaths(string selectedFolder, bool relativeInput)
    {
        var root = CreateRoot();
        var originalDirectory = Environment.CurrentDirectory;
        try
        {
            var input = Path.Combine(root, "mod");
            var meshDirectory = Path.Combine(input, "meshes", "armor");
            var materialDirectory = Path.Combine(input, "materials", "armor");
            Directory.CreateDirectory(meshDirectory);
            Directory.CreateDirectory(materialDirectory);
            await File.WriteAllBytesAsync(Path.Combine(meshDirectory, "armor_0.nif"), new byte[64]);
            await File.WriteAllTextAsync(Path.Combine(materialDirectory, "armor.bgsm"), "material");
            await File.WriteAllTextAsync(Path.Combine(materialDirectory, "armor.bgem"), "effect");
            var output = Path.Combine(root, "output");
            if (relativeInput) Environment.CurrentDirectory = input;

            var results = await new BatchConversionRunner(StandaloneConversionModules.CreateDefault())
                .ConvertAsync(new ConversionRequest(relativeInput ? selectedFolder : Path.Combine(input, selectedFolder), "CBBE", output));

            var result = Assert.Single(results);
            Assert.True(result.Success);
            Assert.Equal("material", await File.ReadAllTextAsync(
                Path.Combine(result.OutputDirectory, "materials", "armor", "armor.bgsm")));
            Assert.Equal("effect", await File.ReadAllTextAsync(
                Path.Combine(result.OutputDirectory, "materials", "armor", "armor.bgem")));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SingleMeshImportsAndExports_KeepOwnSharedAndCompanionTexturesWithoutPackWideCopies()
    {
        var root = CreateRoot();
        try
        {
            var aLow = WriteTextureNif(root, "meshes/a_0.nif", ["textures/a/main.dds", "textures/shared/detail.dds"]);
            WriteTextureNif(root, "meshes/a_1.nif", ["textures/a/high.dds"]);
            var b = WriteTextureNif(root, "meshes/b.nif", ["textures/b/main.dds", "textures/shared/detail.dds"]);
            var aDiffuse = WriteTexture(root, "textures/a/main.dds");
            var aNormal = WriteTexture(root, "textures/a/main_n.dds");
            var aHigh = WriteTexture(root, "textures/a/high.dds");
            var bDiffuse = WriteTexture(root, "textures/b/main.dds");
            var shared = WriteTexture(root, "textures/shared/detail.dds");
            var unrelated = WriteTexture(root, "textures/unrelated/large.dds");
            using var context = new BatchPluginExportContext();
            var importer = new LocalArmorImportService();

            var aArmor = await importer.ImportAsync(aLow, CancellationToken.None, null, context);
            var bArmor = await importer.ImportAsync(b, CancellationToken.None, null, context);

            Assert.Equal(2, aArmor.MeshFiles.Count);
            Assert.Equal(new[] { aHigh, aDiffuse, aNormal, shared }.OrderBy(path => path), aArmor.TextureFiles.OrderBy(path => path));
            Assert.Equal(new[] { bDiffuse, shared }.OrderBy(path => path), bArmor.TextureFiles.OrderBy(path => path));
            Assert.DoesNotContain(unrelated, aArmor.TextureFiles);
            Assert.DoesNotContain(unrelated, bArmor.TextureFiles);

            var aOutput = await ExportAsync(Path.Combine(root, "item-a"), aArmor,
                new PluginAnalysisResult([], [], ""), new TextureSummary(0, [], [], []), context);
            var bOutput = await ExportAsync(Path.Combine(root, "item-b"), bArmor,
                new PluginAnalysisResult([], [], ""), new TextureSummary(0, [], [], []), context);
            Assert.True(File.Exists(Path.Combine(aOutput, "textures/a/main.dds")));
            Assert.True(File.Exists(Path.Combine(aOutput, "textures/a/main_n.dds")));
            Assert.True(File.Exists(Path.Combine(aOutput, "textures/a/high.dds")));
            Assert.True(File.Exists(Path.Combine(aOutput, "textures/shared/detail.dds")));
            Assert.False(File.Exists(Path.Combine(aOutput, "textures/b/main.dds")));
            Assert.True(File.Exists(Path.Combine(bOutput, "textures/b/main.dds")));
            Assert.True(File.Exists(Path.Combine(bOutput, "textures/shared/detail.dds")));
            Assert.False(File.Exists(Path.Combine(bOutput, "textures/a/main.dds")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ReadableBodySlideProjectDoesNotForceLargeUnrelatedTexturesIntoEveryExport()
    {
        var root = CreateRoot();
        try
        {
            var nif = WriteTextureNif(root, "meshes/armor.nif", ["textures/armor/main.dds"]);
            WriteTextureNif(root, "CalienteTools/BodySlide/ShapeData/SourceAssets/source.nif",
                ["textures/shared/body.dds"]);
            var own = WriteTexture(root, "textures/armor/main.dds");
            var companion = WriteTexture(root, "textures/armor/main_layer_extra_n.dds");
            var shared = WriteTexture(root, "textures/shared/body.dds");
            var unrelated = WriteTexture(root, "textures/unrelated/large.dds");
            using (var file = File.OpenWrite(unrelated)) file.SetLength(8 * 1024 * 1024);
            var project = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets", "armor.osp");
            Directory.CreateDirectory(Path.GetDirectoryName(project)!);
            File.WriteAllText(project, """
                <SliderSetInfo><SliderSet name="ArmorProject">
                  <OutputPath>meshes</OutputPath><OutputFile>armor</OutputFile>
                  <DataFolder>SourceAssets</DataFolder><InputFile>source.nif</InputFile>
                </SliderSet></SliderSetInfo>
                """);
            File.WriteAllText(Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "SourceAssets", "morphs.osd"), "morph data");
            using var context = new BatchPluginExportContext();
            var armor = await new LocalArmorImportService().ImportAsync(nif, CancellationToken.None, null, context);

            Assert.Equal(new[] { own, companion, shared }.OrderBy(path => path),
                armor.TextureFiles.OrderBy(path => path));
            var output = await ExportAsync(Path.Combine(root, "converted"), armor,
                new PluginAnalysisResult([], [], ""), new TextureSummary(0, [], [], []), context);
            Assert.False(File.Exists(Path.Combine(output, "textures", "unrelated", "large.dds")));
            var exportedTextures = Directory.GetFiles(Path.Combine(output, "textures"), "*", SearchOption.AllDirectories);
            Assert.Equal(3, exportedTextures.Length);
            Assert.Equal(armor.TextureFiles.Sum(path => new FileInfo(path).Length),
                exportedTextures.Sum(path => new FileInfo(path).Length));
            Assert.True(exportedTextures.Sum(path => new FileInfo(path).Length) < 1024);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("unsupported")]
    [InlineData("multiple-folders")]
    [InlineData("datafolder-shadow")]
    public async Task UnprovenBodySlideInputsRetainConservativeTextureCoverage(string uncertainty)
    {
        var root = CreateRoot();
        try
        {
            var nif = WriteTextureNif(root, "meshes/armor.nif", ["textures/a.dds"]);
            var first = WriteTexture(root, "textures/a.dds");
            var second = WriteTexture(root, "textures/b.dds");
            var linkedMesh = WriteTextureNif(root, "source.nif", ["textures/a.dds"]);
            var project = Path.Combine(root, "armor.osp");
            File.WriteAllText(project, uncertainty == "multiple-folders"
                ? "<SliderSetInfo><SliderSet><DataFolder>A</DataFolder><InputFile>source.nif</InputFile></SliderSet><SliderSet><DataFolder>B</DataFolder><InputFile>source.nif</InputFile></SliderSet></SliderSetInfo>"
                : "<SliderSetInfo><SliderSet><InputFile>source.nif</InputFile></SliderSet></SliderSetInfo>");
            if (uncertainty == "datafolder-shadow")
            {
                File.Delete(project);
                project = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets", "armor.osp");
                Directory.CreateDirectory(Path.GetDirectoryName(project)!);
                File.WriteAllText(project, "<SliderSetInfo><SliderSet><DataFolder>MissingAssets</DataFolder><InputFile>source.nif</InputFile></SliderSet></SliderSetInfo>");
                WriteTextureNif(root, "CalienteTools/BodySlide/SliderSets/source.nif", ["textures/a.dds"]);
            }
            if (uncertainty == "missing") File.Delete(linkedMesh);
            if (uncertainty == "malformed") File.WriteAllText(project, "<SliderSetInfo>");
            if (uncertainty == "unsupported") File.WriteAllText(linkedMesh, "unsupported NIF");

            var armor = await new LocalArmorImportService().ImportAsync(nif, CancellationToken.None);
            Assert.Contains(first, armor.TextureFiles);
            Assert.Contains(second, armor.TextureFiles);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TextureCompanionMatchingPreservesUnderscorePrefixesWithoutCrossingDirectories()
    {
        var root = CreateRoot();
        try
        {
            var nif = WriteTextureNif(root, "meshes/armor.nif", ["textures/armor_set/main_layer_d.dds"]);
            var own = WriteTexture(root, "textures/armor_set/main_layer_d.dds");
            var companion = WriteTexture(root, "textures/armor_set/main_layer_extra_n.dds");
            var shorter = WriteTexture(root, "textures/armor_set/main_n.dds");
            var otherDirectory = WriteTexture(root, "textures/armor_set_extra/main_layer_n.dds");
            var armor = await new LocalArmorImportService().ImportAsync(nif, CancellationToken.None);

            Assert.Equal(new[] { own, companion }.OrderBy(path => path), armor.TextureFiles.OrderBy(path => path));
            Assert.DoesNotContain(shorter, armor.TextureFiles);
            Assert.DoesNotContain(otherDirectory, armor.TextureFiles);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("effect-shader")]
    [InlineData("unresolved")]
    [InlineData("material")]
    [InlineData("script")]
    [InlineData("bodyslide")]
    [InlineData("plugin-txst")]
    [InlineData("unsupported-version")]
    [InlineData("truncated")]
    [InlineData("unsupported-reference")]
    public async Task TextureNarrowing_ConservativelyRetainsAllSupportForUnprovenDependencies(string uncertainty)
    {
        var root = CreateRoot();
        try
        {
            var references = uncertainty == "unresolved"
                ? new[] { "textures/not-in-pack.dds" } : new[] { "textures/a.dds" };
            var nif = WriteTextureNif(root, "meshes/armor.nif", references,
                uncertainty == "effect-shader" ? "BSEffectShaderProperty" : "BSShaderTextureSet",
                uncertainty == "unsupported-version" ? 130u : 83u);
            var first = WriteTexture(root, "textures/a.dds");
            var second = WriteTexture(root, "textures/b.dds");
            switch (uncertainty)
            {
                case "malformed": File.WriteAllText(nif, "unsupported mesh"); break;
                case "material": File.WriteAllText(Path.Combine(root, "armor.bgsm"), "material"); break;
                case "script": File.WriteAllText(Path.Combine(root, "armor.pex"), "script"); break;
                case "bodyslide": File.WriteAllText(Path.Combine(root, "armor.osp"), "<SliderSetInfo />"); break;
                case "plugin-txst":
                    File.WriteAllBytes(Path.Combine(root, "Armor.esp"), "TES4 TXST"u8.ToArray());
                    break;
                case "truncated": File.WriteAllBytes(nif, File.ReadAllBytes(nif)[..^8]); break;
                case "unsupported-reference":
                    File.WriteAllText(Path.Combine(root, "meshes", "body_reference.nif"), "unknown body");
                    break;
            }

            var armor = await new LocalArmorImportService().ImportAsync(nif, CancellationToken.None);

            Assert.Contains(first, armor.TextureFiles);
            Assert.Contains(second, armor.TextureFiles);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BatchImport_ReusesFrozenSupportScanWhileStandaloneImportsRemainFresh()
    {
        var root = CreateRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "meshes"));
            var firstMesh = Path.Combine(root, "meshes", "a.nif");
            var secondMesh = Path.Combine(root, "meshes", "b.nif");
            File.WriteAllText(firstMesh, "unknown mesh");
            File.WriteAllText(secondMesh, "unknown mesh");
            var original = WriteTexture(root, "textures/original.dds");
            using var context = new BatchPluginExportContext();
            var importer = new LocalArmorImportService();
            var first = await importer.ImportAsync(firstMesh, CancellationToken.None, null, context);
            var added = WriteTexture(root, "textures/added-after-snapshot.dds");

            var second = await importer.ImportAsync(secondMesh, CancellationToken.None, null, context);
            var fresh = await importer.ImportAsync(secondMesh, CancellationToken.None);

            Assert.Equal(original, Assert.Single(first.TextureFiles));
            Assert.Equal(original, Assert.Single(second.TextureFiles));
            Assert.Contains(added, fresh.TextureFiles);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
            {
                _ = importer.ImportAsync(secondMesh, cancellation.Token, null, context);
            });
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DerivedMaps_PreserveDetailButCompactUniformDerivedMaps()
    {
        var root = CreateRoot();
        try
        {
            var diffuse = Path.Combine(root, "armor.dds");
            var bytes = LocalExportService.BuildSolidColorDds(0, 0, 0, 255, 16, 16);
            bytes[128] = bytes[129] = bytes[130] = 255;
            File.WriteAllBytes(diffuse, bytes);
            File.WriteAllBytes(Path.Combine(root, "armor_s.dds"),
                LocalExportService.BuildSpecularMapDds(16, 16));
            var armor = new ImportedArmor(root, [], [diffuse], [], []);
            var output = Path.Combine(root, "textures");
            Directory.CreateDirectory(output);
            await (Task)GetMethod("GenerateMissingAuxTextureStubsAsync")
                .Invoke(null, [armor, MissingTextureSummary(), output, CancellationToken.None])!;

            var glow = await File.ReadAllBytesAsync(Path.Combine(output, "armor_g.dds"));
            Assert.Equal(bytes.Length, glow.Length);
            Assert.Equal(255, glow[128]);
            Assert.Equal(0, glow[132]);
            var roughness = await File.ReadAllBytesAsync(Path.Combine(output, "armor_r.dds"));
            Assert.Equal(192, roughness.Length);
            Assert.Equal(127, roughness[128]);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TimingReport_IncludesExportQueueWaitAndMatchesZipAndDiagnostics()
    {
        var root = CreateRoot();
        try
        {
            var nif = Path.Combine(root, "armor.nif");
            File.WriteAllText(nif, "mesh");
            var plugin = WritePlugin(root, "Armor.esp");
            using var batchContext = new BatchPluginExportContext { PluginFiles = [plugin] };
            var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var gateHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var gateTask = batchContext.ExecuteAsync(async () =>
            {
                gateHeld.SetResult();
                await releaseGate.Task;
                return true;
            }, CancellationToken.None);
            await gateHeld.Task;
            var reachedExport = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var progress = new InlineProgress(update =>
            {
                if (update.Stage == "Exporting outputs") reachedExport.TrySetResult();
            });
            Directory.CreateDirectory(Path.Combine(root, "output"));
            File.WriteAllText(Path.Combine(root, "output", "conversion-timings.log"), "stale");
            var request = new ConversionRequest(nif, "CBBE", Path.Combine(root, "output"), OutputZip: true)
            {
                BatchPluginExportContext = batchContext
            };
            var conversion = StandaloneConversionModules.CreateDefault().ConvertAsync(request, progress: progress);
            await reachedExport.Task.WaitAsync(TimeSpan.FromSeconds(60));
            await Task.Delay(150);
            releaseGate.SetResult();
            await gateTask;
            var result = await conversion;

            var json = await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory, "conversion-timings.json"));
            using var timing = JsonDocument.Parse(json);
            var exportMs = timing.RootElement.GetProperty("StageMilliseconds").GetProperty("export").GetInt64();
            Assert.True(exportMs >= 100);
            Assert.Equal(exportMs, timing.RootElement.GetProperty("PhaseMilliseconds").GetProperty("export").GetInt64());
            Assert.True(timing.RootElement.GetProperty("TotalMilliseconds").GetInt64() >= exportMs);
            Assert.Contains(result.Steps, step => step == $"stage-ms:export={exportMs}");
            Assert.False(Directory.Exists(result.OutputDirectory + ".reports"));
            using var profile = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory, "conversion-pipeline-profile.json")));
            Assert.Equal(timing.RootElement.GetProperty("TotalMilliseconds").GetInt64(),
                profile.RootElement.GetProperty("TotalDurationMs").GetInt64());
            Assert.Contains($"stage-ms:export={exportMs}", await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory, "conversion.log")));
            Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory, "conversion-timings.log")));
            using var zip = ZipFile.OpenRead(result.OutputDirectory + ".zip");
            Assert.Null(zip.GetEntry("conversion-timings.json"));
            Assert.Null(zip.GetEntry("output-size-inventory.json"));
            using var profileReader = new StreamReader(zip.GetEntry("conversion-pipeline-profile.json")!.Open());
            Assert.Equal(await File.ReadAllTextAsync(Path.Combine(result.OutputDirectory, "conversion-pipeline-profile.json")),
                await profileReader.ReadToEndAsync());
            Assert.Single(zip.Entries, entry => entry.FullName == "conversion-timings.log");
            using var reader = new StreamReader(zip.GetEntry("conversion-timings.log")!.Open());
            Assert.Equal(json, await reader.ReadToEndAsync());
            using var logReader = new StreamReader(zip.GetEntry("conversion.log")!.Open());
            Assert.Contains($"stage-ms:export={exportMs}", await logReader.ReadToEndAsync());
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class InlineProgress(Action<ConversionStageProgressUpdate> action) : IProgress<ConversionStageProgressUpdate>
    {
        public void Report(ConversionStageProgressUpdate value) => action(value);
    }

    private static MethodInfo GetMethod(string name) =>
        typeof(LocalExportService).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string CreateRoot()
    {
        var root = Path.GetFullPath(Path.Combine(".test-work", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        return root;
    }

    private static TextureSummary MissingTextureSummary() => new(
        1, ["armor.dds"], [], ["armor.dds"],
        MissingSpecular: ["armor.dds"], MissingParallax: ["armor.dds"], MissingGlow: ["armor.dds"],
        MissingRoughness: ["armor.dds"], MissingSubsurface: ["armor.dds"]);

    private static string WritePlugin(string root, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var meshPath = Encoding.ASCII.GetBytes("meshes/armor_0.nif\0");
        var bytes = new byte[24 + 24 + 6 + meshPath.Length];
        Encoding.ASCII.GetBytes("TES4").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("ARMA").CopyTo(bytes, 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28, 4), 6 + meshPath.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36, 4), 0x00001234);
        Encoding.ASCII.GetBytes("MOD2").CopyTo(bytes, 48);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52, 2), (ushort)meshPath.Length);
        meshPath.CopyTo(bytes, 54);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string WriteTexture(string root, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, LocalExportService.BuildSolidColorDds(0, 0, 0, 255));
        return path;
    }

    private static string WriteTextureNif(
        string root, string relativePath, IReadOnlyList<string> textures,
        string blockType = "BSShaderTextureSet", uint bethesdaVersion = 83)
    {
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((uint)textures.Count);
            foreach (var texture in textures)
            {
                var bytes = Encoding.UTF8.GetBytes(texture);
                writer.Write((uint)bytes.Length);
                writer.Write(bytes);
            }
        }
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        using var header = new BinaryWriter(stream, Encoding.UTF8);
        header.Write(Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        header.Write(0x14020007u);
        header.Write((byte)1);
        header.Write(12u);
        header.Write(1u);
        header.Write(bethesdaVersion);
        header.Write(new byte[3]);
        header.Write((ushort)1);
        var type = Encoding.ASCII.GetBytes(blockType);
        header.Write((uint)type.Length);
        header.Write(type);
        header.Write((ushort)0);
        header.Write((uint)payload.Length);
        header.Write(0u);
        header.Write(0u);
        header.Write(0u);
        header.Write(payload.ToArray());
        header.Write(0u);
        return path;
    }

    [Fact]
    public async Task ExportPreservesDiscoveredBodySlideOsdAndSliderGroupDependencies()
    {
        var root = CreateRoot();
        try
        {
            var nif = WriteTextureNif(root, "meshes/armor.nif", []);
            var project = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets", "source.osp");
            var payload = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "Source", "morphs.osd");
            var group = Path.Combine(root, "CalienteTools", "BodySlide", "SliderGroups", "source.xml");
            foreach (var path in new[] { project, payload, group }) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(project, "<SliderSetInfo><SliderSet name=\"Source\"><DataFolder>Source</DataFolder><Slider name=\"Fit\"><DataFile>morphs.osd</DataFile></Slider></SliderSet></SliderSetInfo>");
            await File.WriteAllBytesAsync(payload, "OSD\0source-payload"u8.ToArray());
            await File.WriteAllTextAsync(group, "<SliderGroups><Group name=\"Source\"><Member name=\"Source\"/></Group></SliderGroups>");
            var armor = await new LocalArmorImportService().ImportAsync(nif, CancellationToken.None);
            foreach (var source in new[] { project, payload, group }) Assert.Contains(source, armor.BodyReferenceFiles);
            var output = await ExportAsync(Path.Combine(root, "converted"), armor,
                new PluginAnalysisResult([], [], ""), new TextureSummary(0, [], [], []));
            foreach (var source in new[] { project, payload, group })
            {
                var destination = Path.Combine(output, Path.GetRelativePath(root, source));
                Assert.True(File.Exists(destination), Path.GetRelativePath(root, source));
                Assert.Equal(await File.ReadAllBytesAsync(source), await File.ReadAllBytesAsync(destination));
            }
        }
        finally { Directory.Delete(root, true); }
    }

    private static async Task<string> ExportAsync(
        string root, ImportedArmor armor, PluginAnalysisResult pluginAnalysis, TextureSummary textures,
        BatchPluginExportContext? context = null, MorphSet? morphs = null)
    {
        var output = Path.Combine(root, "output");
        var result = await new LocalExportService().ExportAsync(
            new ConversionRequest(root, "CBBE", output, GenerateBodySlideFiles: false) { BatchPluginExportContext = context },
            armor, new MeshAnalysis("plate", false, 1),
            new ConvertedMesh("plate", "direct-copy", 1, new Dictionary<string, double>()),
            morphs ?? new MorphSet("low", "high", true), new PhysicsConfig("none"),
            new ClippingReport(false, [], []), new CorrectionResult(false, "not-required"),
            new BodySlideProject("Armor", "CBBE", [], "<BodySlideProject/>"),
            pluginAnalysis, textures, new PoseSimulationResult([], new Dictionary<string, IReadOnlyList<string>>(), [], 0),
            [], new BodyDetectionReport("CBBE", 1, ["test"]),
            new SkeletonMappingResult("XPMSSE", "CBBE", [], []), null,
            new VoxelCollisionResult(false, [], new Dictionary<string, double>(), 16), CancellationToken.None);
        return result.OutputDirectory;
    }
}
