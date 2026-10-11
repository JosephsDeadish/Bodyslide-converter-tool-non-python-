using System.Text;
using System.Text.Json;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class SkyrimSseNifShapeReaderTests
{
    internal static byte[] CreateNifForShapeTargets(params string[] shapeNames) =>
        CreateNif(shapeNames
            .Select(static name => new TestShape(name, [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)]))
            .ToArray());

    internal static byte[] CreateDynamicNifForShapeTargets(params string[] shapeNames) =>
        CreateNif(shapeNames
            .Select(static name => new TestShape(
                name,
                [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                VertexStride: 0,
                DynamicVertices: [(0f, 0f, 0f, 0.25f), (1f, 0f, 0f, 0.5f), (0f, 1f, 0f, 0.75f)]))
            .ToArray(),
            blockType: "BSDynamicTriShape",
            dynamicShapeLayout: true);

    [Fact]
    public void ReadsSeparateShapeNamesVerticesAndTriangleIndexes()
    {
        var payload = CreateNif(
            [
                new TestShape("BodyPartA", [(1f, 2f, 3f), (4f, 5f, 6f), (7f, 8f, 9f)]),
                new TestShape("BodyPartB", [(-1f, -2f, -3f), (-4f, -5f, -6f), (-7f, -8f, -9f)])
            ]);

        var result = SkyrimSseNifShapeReader.Read(payload);

        Assert.True(result.Supported, result.Diagnostic);
        Assert.Equal("supported-unskinned-bstrishape", result.Diagnostic);
        Assert.Equal(2, result.Shapes.Count);
        Assert.Equal("BodyPartA", result.Shapes[0].Name);
        Assert.Equal(new MeshVertex(1f, 2f, 3f), result.Shapes[0].Vertices[0]);
        Assert.Equal(new MeshVertex(7f, 8f, 9f), result.Shapes[0].Vertices[2]);
        Assert.Equal(new ushort[] { 0, 1, 2 }, result.Shapes[0].TriangleIndices);
        Assert.True(result.Shapes[0].VertexDataOffset > 0);
        Assert.Equal("BodyPartB", result.Shapes[1].Name);
        Assert.Equal(new MeshVertex(-1f, -2f, -3f), result.Shapes[1].Vertices[0]);
        Assert.Equal(16, result.Shapes[1].VertexStride);
        Assert.Equal(3, result.Shapes[0].ParticleVertices!.Count);
        Assert.Equal(3, result.Shapes[0].ParticleNormals!.Count);
        Assert.Equal(new ushort[] { 0, 1, 2 }, result.Shapes[0].ParticleTriangleIndices);
    }

    [Fact]
    public void GeometryInspectionUsesParsedShapesForSupportedSseLayout()
    {
        var nif = CreateNif(
        [
            new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 0f, 1f)]),
            new TestShape("Detail", [(2f, 0f, 0f), (3f, 0f, 0f), (2f, 0f, 1f)])
        ]);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".nif");
        try
        {
            File.WriteAllBytes(path, nif);
            var report = NifGeometrySignatureReader.Inspect(path);

            Assert.Equal("supported", report.Status);
            Assert.Equal("parsed-sse-bstrishape", report.ParseMode);
            Assert.Equal(6, report.VertexCount);
            Assert.Equal(
                new[]
                {
                    new MeshVertex(0f, 0f, 0f), new MeshVertex(1f, 0f, 0f), new MeshVertex(0f, 0f, 1f),
                    new MeshVertex(2f, 0f, 0f), new MeshVertex(3f, 0f, 0f), new MeshVertex(2f, 0f, 1f)
                },
                NifGeometrySignatureReader.TryReadFullVertices(nif));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExactShapeCorrespondenceRequiresMatchingOrderedGeometryAndTopology()
    {
        var source = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])])).Shapes);
        var target = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])])).Shapes);
        var identicalTarget = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])])).Shapes);

        Assert.True(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(source, target, out var evidence));
        Assert.True(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(source, identicalTarget, out var identicalEvidence));
        Assert.StartsWith("exact-ordered-shape-geometry-and-topology-sha256:", evidence, StringComparison.Ordinal);
        Assert.Equal(evidence, identicalEvidence);
        Assert.Equal(
            SkyrimSseNifShapeCorrespondence.ComputeVertexOrderFingerprint(source),
            SkyrimSseNifShapeCorrespondence.ComputeVertexOrderFingerprint(target));
    }

    [Fact]
    public void ExactShapeCorrespondenceRejectsShapeNameVertexOrderAndTopologyMismatches()
    {
        var source = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])])).Shapes);
        var differentName = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("BodyDetail", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])])).Shapes);
        var differentNameCase = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])])).Shapes);
        var reorderedVertices = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("Body", [(1f, 0f, 0f), (0f, 0f, 0f), (0f, 1f, 0f)])])).Shapes);
        var differentTopology = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], Triangle: (0, 2, 1))])).Shapes);
        var negativeZero = Assert.Single(SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("Body", [(-0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])])).Shapes);

        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(source, differentName, out _));
        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(source, differentNameCase, out _));
        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(source, reorderedVertices, out _));
        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(source, differentTopology, out _));
        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(source, negativeZero, out _));
        Assert.NotEqual(
            SkyrimSseNifShapeCorrespondence.ComputeVertexOrderFingerprint(source),
            SkyrimSseNifShapeCorrespondence.ComputeVertexOrderFingerprint(reorderedVertices));
        Assert.NotEqual(
            SkyrimSseNifShapeCorrespondence.ComputeVertexOrderFingerprint(source),
            SkyrimSseNifShapeCorrespondence.ComputeVertexOrderFingerprint(negativeZero));
    }

    [Fact]
    public void ReadsAndTransformsSubIndexTriShapeWhilePreservingOpaqueExtensionBytes()
    {
        var opaqueSegmentPayload = new byte[] { 0x5A, 0x19, 0xA3, 0x7C, 0x42, 0xD1 };
        var source = CreateNif(
        [
            new TestShape(
                "SegmentedBody",
                [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)],
                TrailingData: opaqueSegmentPayload)
        ],
        blockType: "BSSubIndexTriShape");
        var readResult = SkyrimSseNifShapeReader.Read(source);

        Assert.True(readResult.Supported, readResult.Diagnostic);
        Assert.Equal("supported-bssubindextrishape-position-index-only-segments-opaque", readResult.Diagnostic);
        var shape = Assert.Single(readResult.Shapes);
        Assert.Equal("BSSubIndexTriShape", shape.GeometryBlockType);
        var inspectionPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".nif");
        try
        {
            File.WriteAllBytes(inspectionPath, source);
            var inspection = NifGeometrySignatureReader.Inspect(inspectionPath);
            Assert.Equal("supported", inspection.Status);
            Assert.Equal("parsed-sse-bssubindextrishape-position-index-only-segments-opaque", inspection.ParseMode);
            Assert.Equal("automated-file-inspection-only", inspection.CompatibilityEvidenceScope);
            Assert.Equal("untested", inspection.ExternalCompatibilityStatus);
        }
        finally
        {
            File.Delete(inspectionPath);
        }

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));

        var transformedShape = Assert.Single(SkyrimSseNifShapeReader.Read(transformed).Shapes);
        Assert.NotEqual(shape.Vertices, transformedShape.Vertices);
        Assert.Equal(shape.TriangleIndices, transformedShape.TriangleIndices);
        var opaqueOffset = shape.VertexDataOffset + shape.Vertices.Count * shape.VertexStride +
                           shape.TriangleIndices.Count * sizeof(ushort) +
                           sizeof(uint) +
                           (shape.ParticleVertices!.Count + shape.ParticleNormals!.Count) * 3 * sizeof(ushort) +
                           shape.ParticleTriangleIndices!.Count * sizeof(ushort);
        Assert.Equal(opaqueSegmentPayload, source.AsSpan(opaqueOffset, opaqueSegmentPayload.Length).ToArray());
        Assert.Equal(opaqueSegmentPayload, transformed.AsSpan(opaqueOffset, opaqueSegmentPayload.Length).ToArray());
        AssertOnlyShapePositionsChanged(source, transformed, [shape]);
        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(
            shape,
            SkyrimSseNifShapeReader.Read(CreateNif(
                [new TestShape("SegmentedBody", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)])])).Shapes[0],
            out _));
    }

    [Fact]
    public void WriterUsesParsedVertexStreamForSupportedSingleShapeNif()
    {
        var source = CreateNif(
            [new TestShape("Body", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)])]);
        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));

        var sourceShape = Assert.Single(SkyrimSseNifShapeReader.Read(source).Shapes);
        var transformedShape = Assert.Single(SkyrimSseNifShapeReader.Read(transformed).Shapes);
        Assert.NotEqual(sourceShape.Vertices, transformedShape.Vertices);
        Assert.Equal(sourceShape.TriangleIndices, transformedShape.TriangleIndices);
        Assert.Equal(sourceShape.Name, transformedShape.Name);
    }

    [Fact]
    public void WriterTransformsEveryParsedShapeWithoutChangingShapeTopology()
    {
        var source = CreateNif(
        [
            new TestShape("Body", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)]),
            new TestShape("BodyDetail", [(0f, 2f, 1f), (24f, 2f, 9f), (0f, 18f, 18f)])
        ]);
        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));

        var sourceShapes = SkyrimSseNifShapeReader.Read(source).Shapes;
        var transformedShapes = SkyrimSseNifShapeReader.Read(transformed).Shapes;
        Assert.Equal(sourceShapes.Select(static shape => shape.Name), transformedShapes.Select(static shape => shape.Name));
        Assert.Equal(sourceShapes.Select(static shape => shape.TriangleIndices), transformedShapes.Select(static shape => shape.TriangleIndices));
        for (var shapeIndex = 0; shapeIndex < sourceShapes.Count; shapeIndex++)
        {
            Assert.NotEqual(sourceShapes[shapeIndex].Vertices, transformedShapes[shapeIndex].Vertices);
        }
        AssertOnlyShapePositionsChanged(source, transformed, sourceShapes);
    }

    [Theory]
    [InlineData("BHUNP", "3BA")]
    [InlineData("3BA", "BHUNP")]
    public async Task ConvertAsync_SupportedShapeAwareArmorIsSavedAsReadableNif(string sourceBody, string targetBody)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input", "meshes", "armor");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(inputDirectory);
        var inputPath = Path.Combine(inputDirectory, "shape-aware-armor.nif");
        var sourceBytes = CreateNif(
        [
            new TestShape("RoundTripBody", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)]),
            new TestShape("RoundTripDetail", [(0f, 2f, 1f), (24f, 2f, 9f), (0f, 18f, 18f)])
        ]);
        await File.WriteAllBytesAsync(inputPath, sourceBytes);

        try
        {
            var result = await StandaloneConversionModules.CreateDefault().ConvertAsync(
                new ConversionRequest(inputPath, targetBody, outputDirectory, SourceBodyOverride: sourceBody));

            Assert.True(result.Success);
            using var quality = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "conversion-quality.json")));
            Assert.Equal(sourceBody, quality.RootElement.GetProperty("DetectedSourceBody").GetString());
            Assert.Equal(targetBody, quality.RootElement.GetProperty("TargetBody").GetString());
            var sourceShapes = SkyrimSseNifShapeReader.Read(sourceBytes).Shapes;
            var outputMeshesDirectory = Path.Combine(outputDirectory, "meshes");
            var savedArmorNifs = Directory.GetFiles(outputMeshesDirectory, "*.nif", SearchOption.AllDirectories);
            Assert.NotEmpty(savedArmorNifs);
            var readableOutputs = savedArmorNifs
                .Select(path => SkyrimSseNifShapeReader.Read(File.ReadAllBytes(path)))
                .Where(static readResult => readResult.Supported)
                .ToArray();
            var convertedOutputs = readableOutputs
                .Where(readResult =>
                    readResult.Shapes.Select(static shape => shape.Name)
                        .SequenceEqual(sourceShapes.Select(static shape => shape.Name)) &&
                    readResult.Shapes.Count == sourceShapes.Count &&
                    sourceShapes.Zip(readResult.Shapes)
                        .All(static pair => pair.First.TriangleIndices.SequenceEqual(pair.Second.TriangleIndices)))
                .ToArray();
            Assert.NotEmpty(convertedOutputs);
            Assert.Contains(convertedOutputs.SelectMany(static output => output.Shapes), convertedShape =>
                sourceShapes.Any(sourceShape =>
                    sourceShape.Name == convertedShape.Name &&
                    !sourceShape.Vertices.SequenceEqual(convertedShape.Vertices)));

            foreach (var savedNif in savedArmorNifs)
            {
                var readResult = SkyrimSseNifShapeReader.Read(await File.ReadAllBytesAsync(savedNif));
                Assert.True(readResult.Supported, readResult.Diagnostic);
                Assert.Equal(sourceShapes.Select(static shape => shape.Name), readResult.Shapes.Select(static shape => shape.Name));
                Assert.Equal(sourceShapes.Select(static shape => shape.TriangleIndices), readResult.Shapes.Select(static shape => shape.TriangleIndices));
                Assert.All(readResult.Shapes.SelectMany(static shape => shape.Vertices),
                    static vertex => Assert.True(float.IsFinite(vertex.X) && float.IsFinite(vertex.Y) && float.IsFinite(vertex.Z)));
            }
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriterPreservesRecognizedNifWhenItsLayoutIsUnsupported()
    {
        var source = CreateNif(
            [new TestShape("Body", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)])],
            streamVersion: 83);

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));

        Assert.Same(source, transformed);
    }

    [Fact]
    public void WriterPreservesExtendedVertexAttributesAndTopology()
    {
        var source = CreateNif(
        [
            new TestShape(
                "Body",
                [
                    (0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f),
                    (60f, 0f, 5f), (90f, 0f, 15f), (60f, 20f, 25f)
                ],
                VertexStride: 24,
                Triangles: [(0, 1, 2), (3, 4, 5)])
        ]);

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));

        var sourceShape = Assert.Single(SkyrimSseNifShapeReader.Read(source).Shapes);
        var transformedShape = Assert.Single(SkyrimSseNifShapeReader.Read(transformed).Shapes);
        Assert.Equal(24, sourceShape.VertexStride);
        Assert.Equal(sourceShape.TriangleIndices, transformedShape.TriangleIndices);
        Assert.NotEqual(sourceShape.Vertices, transformedShape.Vertices);
        var sourceTopology = LocalExportService.BuildParsedShapeTopologySummary([sourceShape]);
        Assert.NotNull(sourceTopology);
        Assert.NotEqual(sourceTopology!.ComponentIds[0], sourceTopology.ComponentIds[3]);

        for (var vertexIndex = 0; vertexIndex < sourceShape.Vertices.Count; vertexIndex++)
        {
            var sourceAttributeOffset = sourceShape.VertexDataOffset + (vertexIndex * sourceShape.VertexStride) + 12;
            var transformedAttributeOffset = transformedShape.VertexDataOffset + (vertexIndex * transformedShape.VertexStride) + 12;
            Assert.Equal(
                source.AsSpan(sourceAttributeOffset, 12).ToArray(),
                transformed.AsSpan(transformedAttributeOffset, 12).ToArray());
        }
    }

    [Fact]
    public void ParsedShapeTopologyKeepsComponentsAndBoundaryEdgesShapeLocal()
    {
        var shapes = SkyrimSseNifShapeReader.Read(CreateNif(
        [
            new TestShape("Body", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)]),
            new TestShape("Accessory", [(60f, 0f, 0f), (90f, 0f, 10f), (60f, 20f, 20f)])
        ])).Shapes;

        var topology = LocalExportService.BuildParsedShapeTopologySummary(shapes);

        Assert.NotNull(topology);
        Assert.Equal(6, topology!.VertexCount);
        Assert.Equal(topology.ComponentIds[0], topology.ComponentIds[1]);
        Assert.Equal(topology.ComponentIds[1], topology.ComponentIds[2]);
        Assert.Equal(topology.ComponentIds[3], topology.ComponentIds[4]);
        Assert.Equal(topology.ComponentIds[4], topology.ComponentIds[5]);
        Assert.NotEqual(topology.ComponentIds[0], topology.ComponentIds[3]);
        Assert.All(topology.BoundaryVertexFlags, Assert.True);
    }

    [Fact]
    public void RejectsNonSseStreamProfile()
    {
        var nif = CreateNif([new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])], streamVersion: 83);
        var result = SkyrimSseNifShapeReader.Read(nif);

        Assert.False(result.Supported);
        Assert.Equal("unsupported-nif-profile", result.Diagnostic);
        Assert.Empty(result.Shapes);
        Assert.True(SkyrimSseNifShapeReader.HasRecognizedNifHeader(nif));
        Assert.Null(NifGeometrySignatureReader.TryReadFullVertices(nif));

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            nif,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.2d },
            BasicCageGenerationService.CreatePresetCage("mixed"));
        Assert.Same(nif, transformed);
    }

    [Fact]
    public void PreservesRecognizedLegacyNetImmerseInputWithoutHeuristicRewrite()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("NetImmerse File Format, Version 10.0.1.0\n"u8);
            writer.Write(0x0A000100u);
            writer.Write((byte)1);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
        }
        var nif = stream.ToArray();

        Assert.True(SkyrimSseNifShapeReader.HasRecognizedNifHeader(nif));
        Assert.False(SkyrimSseNifShapeReader.Read(nif).Supported);

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            nif,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.2d },
            BasicCageGenerationService.CreatePresetCage("mixed"));
        Assert.Same(nif, transformed);
    }

    [Fact]
    public void ReadsSkinnedShapePositionsAndTopologyWithoutClaimingSkinPayloadSupport()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)]));

        Assert.True(result.Supported, result.Diagnostic);
        Assert.Equal("supported-skinned-bstrishape-position-index-only", result.Diagnostic);
        var shape = Assert.Single(result.Shapes);
        Assert.True(shape.IsSkinned);
        Assert.Equal(1, shape.SkinInstanceBlockIndex);
        Assert.Equal("NiSkinInstance", shape.SkinInstanceType);
        Assert.NotEqual(0UL, shape.VertexDescriptor);
        Assert.Equal(new MeshVertex(1f, 0f, 0f), shape.Vertices[1]);
        Assert.Equal(new ushort[] { 0, 1, 2 }, shape.TriangleIndices);
    }

    [Fact]
    public void ReadsBethesdaDismemberSkinInstanceReferenceWithoutClaimingPartitionSupport()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("DismemberBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)],
            skinBlockType: "BSDismemberSkinInstance"));

        Assert.True(result.Supported, result.Diagnostic);
        var shape = Assert.Single(result.Shapes);
        Assert.Equal(1, shape.SkinInstanceBlockIndex);
        Assert.Equal("BSDismemberSkinInstance", shape.SkinInstanceType);
        Assert.Equal(2, shape.SkinDataBlockIndex);
        Assert.Equal(3, shape.SkinPartitionBlockIndex);
        Assert.Equal(4, shape.SkeletonRootBlockIndex);
        Assert.Equal(new[] { 5 }, shape.SkinBoneBlockIndices);
        Assert.Equal(1, shape.SkinPartitionCount);
        Assert.Equal([new SkyrimSseNifBodyPartition(257, 32)], shape.BodyPartitions);
        Assert.Contains("position-index-only", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsMultipleDismemberPartitionEntriesAndExposesTheirBodyParts()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [
                new TestShape(
                    "PartitionedBody",
                    [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                    SkinInstance: 1,
                    BodyPartitions: [(257, 32), (1, 33)])
            ],
            skinBlockType: "BSDismemberSkinInstance"));

        Assert.True(result.Supported, result.Diagnostic);
        var shape = Assert.Single(result.Shapes);
        Assert.Equal(2, shape.SkinPartitionCount);
        Assert.Equal(
            [new SkyrimSseNifBodyPartition(257, 32), new SkyrimSseNifBodyPartition(1, 33)],
            shape.BodyPartitions);
    }

    [Fact]
    public void RejectsDismemberPartitionCountThatDoesNotMatchSkinPartitionBlock()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [
                new TestShape(
                    "PartitionedBody",
                    [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                    SkinInstance: 1,
                    BodyPartitions: [(257, 32), (1, 33)])
            ],
            skinBlockType: "BSDismemberSkinInstance",
            skinPartitionCountOverride: 1));

        Assert.False(result.Supported);
        Assert.Equal("dismember-partition-count-mismatch:PartitionedBody", result.Diagnostic);
        Assert.Empty(result.Shapes);
    }

    [Theory]
    [InlineData(0, null, null, null, "skin-reference-invalid:SkinnedBody:NiSkinData")]
    [InlineData(null, 0, null, null, "skin-reference-invalid:SkinnedBody:NiSkinPartition")]
    [InlineData(null, null, 0, null, "skin-reference-invalid:SkinnedBody:skeleton-root")]
    [InlineData(null, null, null, 0, "skin-reference-invalid:SkinnedBody:skin-bone")]
    public void RejectsSkinInstanceReferencesThatDoNotResolveToSchemaTypes(
        int? skinDataReference,
        int? skinPartitionReference,
        int? skeletonRootReference,
        int? boneReference,
        string diagnostic)
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)],
            skinDataReferenceOverride: skinDataReference,
            skinPartitionReferenceOverride: skinPartitionReference,
            skeletonRootReferenceOverride: skeletonRootReference,
            boneReferenceOverride: boneReference));

        Assert.False(result.Supported);
        Assert.Equal(diagnostic, result.Diagnostic);
    }

    [Fact]
    public void RejectsUnmodeledSkinInstancePayloadBytes()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)],
            trailingSkinInstanceByte: true));

        Assert.False(result.Supported);
        Assert.Equal("skin-instance-unmodeled-trailing-data:SkinnedBody", result.Diagnostic);
    }

    [Fact]
    public void RejectsNonFiniteSkinDataWeights()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)],
            skinDataWeightOverride: float.NaN));

        Assert.False(result.Supported);
        Assert.Equal("skin-data-vertex-weight-invalid:SkinnedBody", result.Diagnostic);
    }

    [Fact]
    public void RejectsSkinPartitionVertexMapOutsideShapeVertexRange()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)],
            skinPartitionVertexMapOverride: 3));

        Assert.False(result.Supported);
        Assert.Equal("skin-partition-vertex-map-out-of-range:SkinnedBody", result.Diagnostic);
    }

    [Fact]
    public void RejectsMissingVertexMapWhenPartitionDoesNotCoverWholeShape()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)],
            skinPartitionVertexMapFlagOverride: 0,
            skinPartitionVertexCountOverride: 2));

        Assert.False(result.Supported);
        Assert.Equal("skin-partition-vertex-map-missing:SkinnedBody", result.Diagnostic);
    }

    [Fact]
    public void RejectsNonFiniteSkinPartitionWeights()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)],
            skinPartitionWeightOverride: float.NaN));

        Assert.False(result.Supported);
        Assert.Equal("skin-partition-weight-invalid:SkinnedBody", result.Diagnostic);
    }

    [Fact]
    public void ExtractPartitionSlotsUsesParsedDismemberMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".nif");
        try
        {
            File.WriteAllBytes(path, CreateNif(
                [
                    new TestShape(
                        "PartitionedBody",
                        [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                        SkinInstance: 1,
                        BodyPartitions: [(257, 32), (1, 33)])
                ],
                skinBlockType: "BSDismemberSkinInstance"));

            Assert.Equal([32, 33], NifGeometrySignatureReader.ExtractPartitionSlots(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SyntheticPermittedSkinFixtureRoundTripsWithPositionOnlyTransform()
    {
        var source = CreateNif(
        [
            new TestShape(
                "RoundTripSkinnedBody",
                [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)],
                SkinInstance: 1,
                BodyPartitions: [(257, 32), (1, 33)])
        ],
        skinBlockType: "BSDismemberSkinInstance");
        var initial = SkyrimSseNifShapeReader.Read(source);
        Assert.True(initial.Supported, initial.Diagnostic);

        var output = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));
        var roundTripped = SkyrimSseNifShapeReader.Read(output);

        Assert.NotSame(source, output);
        Assert.True(roundTripped.Supported, roundTripped.Diagnostic);
        Assert.Equal(initial.Shapes[0].BodyPartitions, roundTripped.Shapes[0].BodyPartitions);
        Assert.Equal(initial.Shapes[0].SkinBoneBlockIndices, roundTripped.Shapes[0].SkinBoneBlockIndices);
        Assert.NotEqual(initial.Shapes[0].Vertices, roundTripped.Shapes[0].Vertices);
        AssertOnlyShapePositionsChanged(source, output, initial.Shapes);
    }

    [Theory]
    [InlineData(17, "NiSkinInstance", "unsupported-skin-instance-reference:SkinnedBody")]
    [InlineData(1, "NiNode", "unsupported-skin-instance-reference:SkinnedBody")]
    public void RejectsInvalidOrUnsupportedSkinInstanceReferences(int skinInstance, string skinBlockType, string diagnostic)
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: skinInstance)],
            skinBlockType: skinBlockType));

        Assert.False(result.Supported);
        Assert.Equal(diagnostic, result.Diagnostic);
    }

    [Theory]
    [InlineData("NiSkinInstance")]
    [InlineData("BSDismemberSkinInstance")]
    public void WriterTransformsValidatedSkinnedShapeAndPreservesAllNonPositionData(string skinInstanceType)
    {
        var source = CreateNif(
            [
                new TestShape(
                    "SkinnedBody",
                    [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)],
                    SkinInstance: 1,
                    BodyPartitions: [(257, 32), (1, 33)])
            ],
            skinBlockType: skinInstanceType);
        var initial = SkyrimSseNifShapeReader.Read(source);
        Assert.True(initial.Supported, initial.Diagnostic);
        var shape = Assert.Single(initial.Shapes);

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));
        Assert.NotSame(source, transformed);
        var transformedShape = Assert.Single(SkyrimSseNifShapeReader.Read(transformed).Shapes);
        Assert.NotEqual(shape.Vertices, transformedShape.Vertices);
        Assert.Equal(shape.TriangleIndices, transformedShape.TriangleIndices);
        Assert.Equal(shape.ParticleVertices, transformedShape.ParticleVertices);
        Assert.Equal(shape.ParticleNormals, transformedShape.ParticleNormals);
        Assert.Equal(shape.ParticleTriangleIndices, transformedShape.ParticleTriangleIndices);
        Assert.Equal(shape.BodyPartitions, transformedShape.BodyPartitions);
        Assert.Equal(shape.SkinBoneBlockIndices, transformedShape.SkinBoneBlockIndices);
        AssertOnlyShapePositionsChanged(source, transformed, [shape]);
    }

    [Fact]
    public async Task SkinnedConversionReportsRuntimeValidationRequirement()
    {
        var source = CreateNif(
            [
                new TestShape(
                    "SkinnedBody",
                    [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)],
                    SkinInstance: 1,
                    BodyPartitions: [(257, 32), (1, 33)])
            ],
            skinBlockType: "BSDismemberSkinInstance");
        var sourceShape = Assert.Single(SkyrimSseNifShapeReader.Read(source).Shapes);
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(workingDirectory, "SkinnedBody.nif");
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(workingDirectory);
        try
        {
            File.WriteAllBytes(path, source);
            var support = NifGeometrySignatureReader.Inspect(path);

            Assert.Equal("degraded", support.Status);
            Assert.Equal("parsed-sse-bstrishape-skinned-position-index-only", support.ParseMode);
            Assert.Contains("skin-data-validated-and-preserved-unmodified", support.Messages);
            Assert.Contains("live-skinned-deformation-unverified", support.Messages);
            Assert.Contains("non-position-nif-data-preserved-unmodified", support.Messages);
            Assert.Contains("manual-review-recommended", support.Messages);
        }
        finally
        {
            File.Delete(path);
        }

        try
        {
            File.WriteAllBytes(path, source);
            var result = await StandaloneConversionModules.CreateDefault().ConvertAsync(
                new ConversionRequest(path, "3BA", outputDirectory, SourceBodyOverride: "CBBE"));

            Assert.True(result.Success);
            var qualityJson = File.ReadAllText(Path.Combine(outputDirectory, "conversion-quality.json"));
            Assert.Contains("\"Code\": \"skinned-nif-runtime-unverified\"", qualityJson, StringComparison.Ordinal);
            Assert.Contains("Skin weights, bones, partitions, and other non-position data are never rewritten", qualityJson, StringComparison.Ordinal);
            Assert.Contains(
                "validate the result in Outfit Studio and Skyrim",
                ConversionValidationGuidance.GetIssueFollowUp("skinned-nif-runtime-unverified", "3BA")!,
                StringComparison.Ordinal);
            var outputBytes = Directory.GetFiles(outputDirectory, "*.nif", SearchOption.AllDirectories)
                .Select(File.ReadAllBytes)
                .FirstOrDefault(bytes => !bytes.SequenceEqual(source));
            Assert.NotNull(outputBytes);
            AssertOnlyShapePositionsChanged(source, outputBytes!, [sourceShape]);
        }
        finally
        {
            if (Directory.Exists(workingDirectory)) Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriterTransformsSkinnedAndUnskinnedShapesWithoutChangingNonPositionBytes()
    {
        var source = CreateNif(
        [
            new TestShape("UnskinnedBody", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)]),
            new TestShape(
                "SkinnedTrim",
                [(0f, 1f, 0f), (20f, 1f, 8f), (0f, 16f, 16f)],
                SkinInstance: 2,
                BodyPartitions: [(257, 32)])
        ]);

        var sourceShapes = SkyrimSseNifShapeReader.Read(source).Shapes;
        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));

        Assert.NotSame(source, transformed);
        var transformedShapes = SkyrimSseNifShapeReader.Read(transformed).Shapes;
        Assert.Equal(sourceShapes.Select(static shape => shape.Name), transformedShapes.Select(static shape => shape.Name));
        Assert.Equal(sourceShapes.Select(static shape => shape.TriangleIndices), transformedShapes.Select(static shape => shape.TriangleIndices));
        for (var shapeIndex = 0; shapeIndex < sourceShapes.Count; shapeIndex++)
        {
            Assert.NotEqual(sourceShapes[shapeIndex].Vertices, transformedShapes[shapeIndex].Vertices);
        }
        Assert.Equal(sourceShapes[1].BodyPartitions, transformedShapes[1].BodyPartitions);
        AssertOnlyShapePositionsChanged(source, transformed, sourceShapes);
    }

    [Fact]
    public void WriterLeavesSkinnedShapeUnchangedWhenSkinLayoutIsUnsupported()
    {
        var source = CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)], SkinInstance: 1)],
            skinBlockType: "UnsupportedSkinInstance");
        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"] = 1.2d },
            BasicCageGenerationService.CreatePresetCage("mixed"));
        Assert.Same(source, transformed);
    }

    [Fact]
    public void ReadsAndTransformsUnskinnedDynamicPositionStreamWhilePreservingFourthComponent()
    {
        var nif = CreateNif(
            [
                new TestShape(
                    "DynamicBody",
                    [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)],
                    VertexStride: 0,
                    DynamicVertices: [(0f, 0f, 0f, 0.25f), (30f, 0f, 10f, 0.5f), (0f, 20f, 20f, 0.75f)])
            ],
            blockType: "BSDynamicTriShape",
            dynamicShapeLayout: true);
        var result = SkyrimSseNifShapeReader.Read(nif);

        Assert.True(result.Supported, result.Diagnostic);
        Assert.Equal("supported-bsdynamictrishape-dynamic-position-stream", result.Diagnostic);
        Assert.True(SkyrimSseNifShapeReader.HasSupportedProfile(nif));
        var shape = Assert.Single(result.Shapes);
        Assert.Equal("BSDynamicTriShape", shape.GeometryBlockType);
        Assert.Equal(16, shape.VertexStride);
        Assert.Equal(new MeshVertex(30f, 0f, 10f), shape.Vertices[1]);
        Assert.Equal(3, shape.ParticleVertices!.Count);
        Assert.Equal(new ushort[] { 0, 1, 2 }, shape.ParticleTriangleIndices);

        var inspectionPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".nif");
        try
        {
            File.WriteAllBytes(inspectionPath, nif);
            Assert.Equal("parsed-sse-bsdynamictrishape-dynamic-position-stream", NifGeometrySignatureReader.Inspect(inspectionPath).ParseMode);
        }
        finally
        {
            File.Delete(inspectionPath);
        }

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            nif,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));
        var transformedShape = Assert.Single(SkyrimSseNifShapeReader.Read(transformed).Shapes);
        Assert.NotEqual(shape.Vertices, transformedShape.Vertices);
        Assert.Equal(shape.TriangleIndices, transformedShape.TriangleIndices);
        AssertOnlyShapePositionsChanged(nif, transformed, [shape]);
        Assert.Equal(
            Enumerable.Range(0, shape.Vertices.Count).Select(index =>
                BitConverter.ToSingle(nif, shape.VertexDataOffset + (index * shape.VertexStride) + 12)),
            Enumerable.Range(0, transformedShape.Vertices.Count).Select(index =>
                BitConverter.ToSingle(transformed, transformedShape.VertexDataOffset + (index * transformedShape.VertexStride) + 12)));
    }

    [Fact]
    public void RejectsMalformedDynamicVertexPayloadSize()
    {
        var source = CreateNif(
            [
                new TestShape(
                    "DynamicBody",
                    [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                    VertexStride: 0,
                    DynamicVertices: [(0f, 0f, 0f, 0f), (1f, 0f, 0f, 0f), (0f, 1f, 0f, 0f)])
            ],
            blockType: "BSDynamicTriShape",
            dynamicShapeLayout: true,
            dynamicDataSizeOverride: 47);

        var result = SkyrimSseNifShapeReader.Read(source);
        Assert.False(result.Supported);
        Assert.Equal("bstrishape-dynamic-data-size-mismatch:DynamicBody", result.Diagnostic);
        Assert.Null(NifGeometrySignatureReader.TryReadFullVertices(source));
    }

    [Fact]
    public void RejectsTruncatedDynamicVertexPayload()
    {
        var source = CreateNif(
            [
                new TestShape(
                    "DynamicBody",
                    [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                    VertexStride: 0,
                    DynamicVertices: [(0f, 0f, 0f, 0f), (1f, 0f, 0f, 0f)])
            ],
            blockType: "BSDynamicTriShape",
            dynamicShapeLayout: true,
            dynamicDataSizeOverride: 48);

        var result = SkyrimSseNifShapeReader.Read(source);
        Assert.False(result.Supported);
        Assert.Equal("bstrishape-dynamic-data-truncated:DynamicBody", result.Diagnostic);
    }

    [Fact]
    public void RejectsIncorrectParticleDataSizeFromSchemaDefinedLayout()
    {
        var source = CreateNif(
            [
                new TestShape(
                    "DynamicBody",
                    [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                    VertexStride: 0,
                    DynamicVertices: [(0f, 0f, 0f, 0f), (1f, 0f, 0f, 0f), (0f, 1f, 0f, 0f)])
            ],
            blockType: "BSDynamicTriShape",
            dynamicShapeLayout: true,
            particleDataSizeOverride: 1);

        var result = SkyrimSseNifShapeReader.Read(source);
        Assert.False(result.Supported);
        Assert.Equal("bstrishape-particle-data-size-mismatch:DynamicBody", result.Diagnostic);
    }

    [Fact]
    public void RejectsOutOfRangeParticleTriangleIndexes()
    {
        var source = CreateNif(
            [new TestShape(
                "Body",
                [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                ParticleTriangles: [(0, 1, 3)])]);

        var result = SkyrimSseNifShapeReader.Read(source);

        Assert.False(result.Supported);
        Assert.Equal("bstrishape-particle-triangle-index-out-of-range:Body", result.Diagnostic);
    }

    [Fact]
    public void RejectsNonFiniteHalfPrecisionParticleData()
    {
        var source = CreateNif(
            [new TestShape(
                "Body",
                [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                ParticleVertexHalfBits: [(0x7e00, 0, 0), (0, 0, 0), (0, 0, 0)])]);

        var result = SkyrimSseNifShapeReader.Read(source);

        Assert.False(result.Supported);
        Assert.Equal("bstrishape-nonfinite-particle-vertex:Body", result.Diagnostic);
    }

    [Fact]
    public void LeavesSkinnedDynamicShapeUnchanged()
    {
        var source = CreateNif(
            [
                new TestShape(
                    "DynamicSkinnedBody",
                    [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)],
                    SkinInstance: 1,
                    VertexStride: 0,
                    DynamicVertices: [(0f, 0f, 0f, 0.25f), (30f, 0f, 10f, 0.5f), (0f, 20f, 20f, 0.75f)])
            ],
            blockType: "BSDynamicTriShape",
            dynamicShapeLayout: true,
            particleDataSizeOverride: 0);

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));

        Assert.Same(source, transformed);
    }

    [Theory]
    [InlineData("NiTriShape")]
    [InlineData("NiTriShapeData")]
    [InlineData("NiTriStrips")]
    [InlineData("NiTriStripsData")]
    [InlineData("BSGeometry")]
    [InlineData("NiMesh")]
    public void RejectsUnimplementedGeometryFamiliesInsteadOfParsingAsBSTriShape(string blockType)
    {
        var nif = CreateNif(
            [new TestShape("AlternateBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])],
            blockType: blockType);

        var result = SkyrimSseNifShapeReader.Read(nif);

        Assert.False(result.Supported);
        Assert.Equal($"unsupported-geometry-block:{blockType}", result.Diagnostic);
        Assert.Empty(result.Shapes);
    }

    [Theory]
    [InlineData("NiTriShape")]
    [InlineData("NiTriShapeData")]
    [InlineData("NiTriStrips")]
    [InlineData("NiTriStripsData")]
    [InlineData("BSGeometry")]
    [InlineData("NiMesh")]
    public void WriterDoesNotHeuristicallyModifyUnimplementedGeometryFamilies(string blockType)
    {
        var source = CreateNif(
            [new TestShape("AlternateBody", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)])],
            blockType: blockType);

        var transformed = LocalExportService.TryApplyNifVertexTransform(
            source,
            null,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"] = 1.32d,
                ["breasts"] = 1.28d,
                ["waist"] = 0.84d,
                ["belly"] = 1.18d,
                ["thighs"] = 1.22d
            },
            BasicCageGenerationService.CreatePresetCage("mixed"));

        Assert.Same(source, transformed);
    }

    [Fact]
    public void RejectsTriangleIndexesOutsideTheirShapeVertexRange()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("BrokenShape", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], Triangle: (0, 1, 3))]));

        Assert.False(result.Supported);
        Assert.Equal("bstrishape-triangle-index-out-of-range:BrokenShape", result.Diagnostic);
    }

    [Fact]
    public void RejectsUnmodeledTrailingBytesInStandardShapeBlock()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], TrailingData: [0xA5])]));

        Assert.False(result.Supported);
        Assert.Equal("bstrishape-unmodeled-trailing-data:Body", result.Diagnostic);
    }

    [Fact]
    public void RejectsUnmodeledTrailingBytesAfterDynamicPositionStream()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [
                new TestShape(
                    "DynamicBody",
                    [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)],
                    VertexStride: 0,
                    DynamicVertices: [(0f, 0f, 0f, 0f), (1f, 0f, 0f, 0f), (0f, 1f, 0f, 0f)],
                    TrailingData: [0xA5])
            ],
            blockType: "BSDynamicTriShape",
            dynamicShapeLayout: true));

        Assert.False(result.Supported);
        Assert.Equal("bstrishape-unmodeled-trailing-data:DynamicBody", result.Diagnostic);
    }

    private static byte[] CreateNif(
        IReadOnlyList<TestShape> shapes,
        uint streamVersion = 100,
        string blockType = "BSTriShape",
        string skinBlockType = "NiSkinInstance",
        bool dynamicShapeLayout = false,
        uint? dynamicDataSizeOverride = null,
        uint? particleDataSizeOverride = null,
        uint? skinPartitionCountOverride = null,
        int? skinDataReferenceOverride = null,
        int? skinPartitionReferenceOverride = null,
        int? skeletonRootReferenceOverride = null,
        int? boneReferenceOverride = null,
        bool trailingSkinInstanceByte = false,
        int? skinPartitionVertexMapOverride = null,
        byte? skinPartitionVertexMapFlagOverride = null,
        ushort? skinPartitionVertexCountOverride = null,
        float? skinDataWeightOverride = null,
        float? skinPartitionWeightOverride = null)
    {
        var skinInstanceBlockIndex = shapes.Count;
        var skinDataBlockIndex = skinInstanceBlockIndex + 1;
        var skinPartitionBlockIndex = skinInstanceBlockIndex + 2;
        var skeletonRootBlockIndex = skinInstanceBlockIndex + 3;
        var skinBoneBlockIndex = skinInstanceBlockIndex + 4;
        var shapeBlockBytes = shapes
            .Select((shape, index) => CreateShapeBlock(
                shape with { NameIndex = index },
                dynamicShapeLayout,
                dynamicDataSizeOverride,
                particleDataSizeOverride))
            .ToArray();
        var hasSkinInstance = shapes.Any(static shape => shape.SkinInstance >= 0);
        var bodyPartitions = shapes.FirstOrDefault(static shape => shape.SkinInstance >= 0)?.BodyPartitions
            ?? [(257, 32)];
        var skinPartitionCount = skinPartitionCountOverride ?? (uint)bodyPartitions.Count;
        var skinBlocks = hasSkinInstance
            ? new[]
            {
                CreateSkinInstanceBlock(
                    skinBlockType,
                    skinDataBlockIndex,
                    skinPartitionBlockIndex,
                    skeletonRootBlockIndex,
                    skinBoneBlockIndex,
                    skinBlockType == "BSDismemberSkinInstance" ? bodyPartitions : [],
                    skinDataReferenceOverride,
                    skinPartitionReferenceOverride,
                    skeletonRootReferenceOverride,
                    boneReferenceOverride,
                    trailingSkinInstanceByte),
                CreateNiSkinDataBlock(skinDataWeightOverride),
                CreateNiSkinPartitionBlock(
                    skinPartitionCount,
                    shapes.First(static shape => shape.SkinInstance >= 0),
                    skinPartitionVertexMapOverride,
                    skinPartitionVertexMapFlagOverride,
                    skinPartitionVertexCountOverride,
                    skinPartitionWeightOverride),
                Array.Empty<byte>(),
                Array.Empty<byte>()
            }
            : [];
        var blockBytes = shapeBlockBytes.Concat(skinBlocks).ToArray();
        var blockTypes = hasSkinInstance
            ? new[] { blockType, skinBlockType, "NiSkinData", "NiSkinPartition", "NiNode" }
            : [blockType];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        writer.Write(0x14020007u);
        writer.Write((byte)1);
        writer.Write(12u);
        writer.Write((uint)blockBytes.Length);
        writer.Write(streamVersion);
        writer.Write((uint)blockTypes.Length);
        foreach (var type in blockTypes) WriteSizedString(writer, type);
        for (var i = 0; i < shapes.Count; i++) writer.Write((ushort)0);
        if (hasSkinInstance)
        {
            writer.Write((ushort)1);
            writer.Write((ushort)2);
            writer.Write((ushort)3);
            writer.Write((ushort)4);
            writer.Write((ushort)4);
        }
        foreach (var block in blockBytes) writer.Write((uint)block.Length);
        writer.Write((uint)shapes.Count);
        writer.Write((uint)shapes.Max(shape => Encoding.UTF8.GetByteCount(shape.Name)));
        foreach (var shape in shapes) WriteSizedString(writer, shape.Name);
        writer.Write(0u);
        foreach (var block in blockBytes) writer.Write(block);
        writer.Write(1u);
        writer.Write(0u);
        return stream.ToArray();
    }

    private static byte[] CreateSkinInstanceBlock(
        string skinBlockType,
        int skinDataBlockIndex,
        int skinPartitionBlockIndex,
        int skeletonRootBlockIndex,
        int skinBoneBlockIndex,
        IReadOnlyList<(ushort Flags, ushort BodyPart)> bodyPartitions,
        int? skinDataReferenceOverride,
        int? skinPartitionReferenceOverride,
        int? skeletonRootReferenceOverride,
        int? boneReferenceOverride,
        bool trailingByte)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(skinDataReferenceOverride ?? skinDataBlockIndex);
        writer.Write(skinPartitionReferenceOverride ?? skinPartitionBlockIndex);
        writer.Write(skeletonRootReferenceOverride ?? skeletonRootBlockIndex);
        writer.Write(1u);
        writer.Write(boneReferenceOverride ?? skinBoneBlockIndex);
        if (skinBlockType == "BSDismemberSkinInstance")
        {
            writer.Write((uint)bodyPartitions.Count);
            foreach (var (flags, bodyPart) in bodyPartitions)
            {
                writer.Write(flags);
                writer.Write(bodyPart);
            }
        }
        if (trailingByte) writer.Write((byte)0xA5);
        return stream.ToArray();
    }

    private static byte[] CreateNiSkinDataBlock(float? skinDataWeightOverride)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        for (var i = 0; i < 3 + 9 + 1; i++) writer.Write(0f);
        writer.Write(1u);
        writer.Write((byte)1);
        for (var i = 0; i < 3 + 9 + 1 + 3 + 1; i++) writer.Write(0f);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write(skinDataWeightOverride ?? 1f);
        return stream.ToArray();
    }

    private static byte[] CreateNiSkinPartitionBlock(
        uint partitionCount,
        TestShape shape,
        int? vertexMapOverride,
        byte? vertexMapFlagOverride,
        ushort? vertexCountOverride,
        float? skinPartitionWeightOverride)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(partitionCount);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0UL);
        for (var i = 0; i < checked((int)partitionCount); i++)
        {
            var triangles = shape.Triangles ?? [shape.Triangle ?? (0, 1, 2)];
            var partitionVertexCount = vertexCountOverride ?? (ushort)shape.Vertices.Count;
            writer.Write(partitionVertexCount);
            writer.Write((ushort)triangles.Count);
            writer.Write((ushort)1);
            writer.Write((ushort)0);
            writer.Write((ushort)4);
            writer.Write((ushort)0);
            writer.Write(vertexMapFlagOverride ?? (byte)1);
            for (var vertexIndex = 0; vertexMapFlagOverride != 0 && vertexIndex < partitionVertexCount; vertexIndex++)
            {
                writer.Write((ushort)(vertexIndex == 0 ? vertexMapOverride ?? vertexIndex : vertexIndex));
            }
            writer.Write((byte)1);
            for (var vertexIndex = 0; vertexIndex < partitionVertexCount; vertexIndex++)
            {
                writer.Write(vertexIndex == 0 ? skinPartitionWeightOverride ?? 1f : 1f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(0f);
            }
            foreach (var (a, b, c) in triangles)
            {
                writer.Write((ushort)a);
                writer.Write((ushort)b);
                writer.Write((ushort)c);
            }
            writer.Write((byte)1);
            for (var vertexIndex = 0; vertexIndex < partitionVertexCount * 4; vertexIndex++)
            {
                writer.Write((byte)0);
            }
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write(0UL);
            foreach (var (a, b, c) in triangles)
            {
                writer.Write((ushort)a);
                writer.Write((ushort)b);
                writer.Write((ushort)c);
            }
        }
        return stream.ToArray();
    }

    private static byte[] CreateShapeBlock(
        TestShape shape,
        bool dynamicShapeLayout,
        uint? dynamicDataSizeOverride,
        uint? particleDataSizeOverride)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((uint)shape.NameIndex);
        writer.Write(0u);
        writer.Write(-1);
        writer.Write(0u);
        for (var i = 0; i < 3 + 9; i++) writer.Write(0f);
        writer.Write(1f);
        writer.Write(-1);
        for (var i = 0; i < 4; i++) writer.Write(0f);
        writer.Write(shape.SkinInstance);
        writer.Write(-1);
        writer.Write(-1);
        var descriptor = (ulong)(uint)(shape.VertexStride / 4) | (dynamicShapeLayout ? 0UL : (1UL << 44));
        writer.Write(descriptor);
        var triangles = shape.Triangles ?? [shape.Triangle ?? (0, 1, 2)];
        writer.Write((ushort)triangles.Count);
        writer.Write((ushort)shape.Vertices.Count);
        writer.Write((uint)(shape.Vertices.Count * shape.VertexStride + triangles.Count * 3 * sizeof(ushort)));
        for (var vertexIndex = 0; vertexIndex < shape.Vertices.Count; vertexIndex++)
        {
            if (dynamicShapeLayout)
            {
                for (var attributeByte = 0; attributeByte < shape.VertexStride; attributeByte++)
                {
                    writer.Write((byte)(0xA0 + vertexIndex + attributeByte));
                }
            }
            else
            {
                var (x, y, z) = shape.Vertices[vertexIndex];
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                for (var attributeByte = 12; attributeByte < shape.VertexStride; attributeByte++)
                {
                    writer.Write((byte)(0xA0 + vertexIndex + attributeByte));
                }
            }
        }
        foreach (var (a, b, c) in triangles)
        {
            writer.Write((ushort)a);
            writer.Write((ushort)b);
            writer.Write((ushort)c);
        }
        var expectedParticleDataSize = checked((uint)shape.Vertices.Count * 6u + (uint)triangles.Count * 3u);
        writer.Write(particleDataSizeOverride ?? expectedParticleDataSize);
        WriteParticleVectors(writer, shape.ParticleVertexHalfBits, shape.Vertices.Count);
        WriteParticleVectors(writer, shape.ParticleNormalHalfBits, shape.Vertices.Count);
        foreach (var (a, b, c) in shape.ParticleTriangles ?? triangles)
        {
            writer.Write((ushort)a);
            writer.Write((ushort)b);
            writer.Write((ushort)c);
        }
        if (dynamicShapeLayout)
        {
            var dynamicVertices = shape.DynamicVertices ?? [];
            writer.Write(dynamicDataSizeOverride ?? checked((uint)(dynamicVertices.Count * 4 * sizeof(float))));
            foreach (var (x, y, z, w) in dynamicVertices)
            {
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                writer.Write(w);
            }
        }
        if (shape.TrailingData is not null) writer.Write(shape.TrailingData);
        return stream.ToArray();
    }

    private static void WriteParticleVectors(
        BinaryWriter writer,
        IReadOnlyList<(ushort X, ushort Y, ushort Z)>? vectors,
        int expectedCount)
    {
        for (var i = 0; i < expectedCount; i++)
        {
            var (x, y, z) = vectors is not null && i < vectors.Count ? vectors[i] : default;
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
        }
    }

    private static void AssertOnlyShapePositionsChanged(
        byte[] source,
        byte[] transformed,
        IReadOnlyList<SkyrimSseNifShape> shapes)
    {
        Assert.Equal(source.Length, transformed.Length);
        var positionBytes = new bool[source.Length];
        foreach (var shape in shapes)
        {
            for (var vertexIndex = 0; vertexIndex < shape.Vertices.Count; vertexIndex++)
            {
                var offset = shape.VertexDataOffset + (vertexIndex * shape.VertexStride);
                for (var byteIndex = 0; byteIndex < 3 * sizeof(float); byteIndex++)
                {
                    positionBytes[offset + byteIndex] = true;
                }
            }
        }

        for (var byteIndex = 0; byteIndex < source.Length; byteIndex++)
        {
            if (!positionBytes[byteIndex])
            {
                Assert.Equal(source[byteIndex], transformed[byteIndex]);
            }
        }
    }

    private static void WriteSizedString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((uint)bytes.Length);
        writer.Write(bytes);
    }

    private sealed record TestShape(
        string Name,
        IReadOnlyList<(float X, float Y, float Z)> Vertices,
        int SkinInstance = -1,
        (int, int, int)? Triangle = null,
        int VertexStride = 16,
        IReadOnlyList<(int A, int B, int C)>? Triangles = null,
        byte[]? TrailingData = null,
        IReadOnlyList<(float X, float Y, float Z, float W)>? DynamicVertices = null,
        IReadOnlyList<(int A, int B, int C)>? ParticleTriangles = null,
        IReadOnlyList<(ushort X, ushort Y, ushort Z)>? ParticleVertexHalfBits = null,
        IReadOnlyList<(ushort X, ushort Y, ushort Z)>? ParticleNormalHalfBits = null,
        IReadOnlyList<(ushort Flags, ushort BodyPart)>? BodyPartitions = null)
    {
        public int NameIndex { get; init; }
    }
}
