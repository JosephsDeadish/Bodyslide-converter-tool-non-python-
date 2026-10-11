using System.Text;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class SkyrimSseNifShapeReaderTests
{
    internal static byte[] CreateNifForShapeTargets(params string[] shapeNames) =>
        CreateNif(shapeNames
            .Select(static name => new TestShape(name, [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)]))
            .ToArray());

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
                           shape.TriangleIndices.Count * sizeof(ushort);
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

    [Fact]
    public async Task ConvertAsync_SupportedShapeAwareArmorIsSavedAsReadableNif()
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
                new ConversionRequest(inputPath, "CBBE", outputDirectory));

            Assert.True(result.Success);
            var sourceShapes = SkyrimSseNifShapeReader.Read(sourceBytes).Shapes;
            var outputMeshesDirectory = Path.Combine(outputDirectory, "meshes");
            var savedArmorNifs = Directory.GetFiles(outputMeshesDirectory, "*.nif", SearchOption.AllDirectories);
            Assert.NotEmpty(savedArmorNifs);
            Assert.Contains(savedArmorNifs, path =>
                !File.ReadAllBytes(path).AsSpan().SequenceEqual(sourceBytes));

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
    public void ReadsSkinnedShapePositionsAndTopologyWithoutClaimingSkinPayloadSupport()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)]));

        Assert.True(result.Supported, result.Diagnostic);
        Assert.Equal("supported-skinned-bstrishape-position-index-only", result.Diagnostic);
        var shape = Assert.Single(result.Shapes);
        Assert.True(shape.IsSkinned);
        Assert.Equal(new MeshVertex(1f, 0f, 0f), shape.Vertices[1]);
        Assert.Equal(new ushort[] { 0, 1, 2 }, shape.TriangleIndices);
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

    [Fact]
    public void WriterLeavesSkinnedShapeUnchangedUntilSkinSemanticsAreValidated()
    {
        var source = CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)], SkinInstance: 1)]);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".nif");
        try
        {
            File.WriteAllBytes(path, source);
            var support = NifGeometrySignatureReader.Inspect(path);

            Assert.Equal("degraded", support.Status);
            Assert.Equal("parsed-sse-bstrishape-skinned-position-index-only", support.ParseMode);
            Assert.Contains("skin-weight-and-partition-semantics-unverified", support.Messages);
            Assert.Contains("non-position-nif-data-preserved-unmodified", support.Messages);
            Assert.Contains("manual-review-recommended", support.Messages);
        }
        finally
        {
            File.Delete(path);
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
        Assert.Same(source, transformed);
    }

    [Fact]
    public void WriterLeavesMixedSkinnedAndUnskinnedShapesUnchangedAsOneFile()
    {
        var source = CreateNif(
        [
            new TestShape("UnskinnedBody", [(0f, 0f, 0f), (30f, 0f, 10f), (0f, 20f, 20f)]),
            new TestShape("SkinnedTrim", [(0f, 1f, 0f), (20f, 1f, 8f), (0f, 16f, 16f)], SkinInstance: 1)
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
            dynamicShapeLayout: true,
            particleDataSizeOverride: 0);
        var result = SkyrimSseNifShapeReader.Read(nif);

        Assert.True(result.Supported, result.Diagnostic);
        Assert.Equal("supported-bsdynamictrishape-dynamic-position-stream", result.Diagnostic);
        Assert.True(SkyrimSseNifShapeReader.HasSupportedProfile(nif));
        var shape = Assert.Single(result.Shapes);
        Assert.Equal("BSDynamicTriShape", shape.GeometryBlockType);
        Assert.Equal(16, shape.VertexStride);
        Assert.Equal(new MeshVertex(30f, 0f, 10f), shape.Vertices[1]);

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
            dynamicDataSizeOverride: 47,
            particleDataSizeOverride: 0);

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
            dynamicDataSizeOverride: 48,
            particleDataSizeOverride: 0);

        var result = SkyrimSseNifShapeReader.Read(source);
        Assert.False(result.Supported);
        Assert.Equal("bstrishape-dynamic-data-truncated:DynamicBody", result.Diagnostic);
    }

    [Fact]
    public void RejectsDynamicShapeParticlePayloadUntilItsLayoutIsSupported()
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
        Assert.Equal("unsupported-bsdynamictrishape-particle-data:DynamicBody", result.Diagnostic);
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

    private static byte[] CreateNif(
        IReadOnlyList<TestShape> shapes,
        uint streamVersion = 100,
        string blockType = "BSTriShape",
        string skinBlockType = "NiSkinInstance",
        bool dynamicShapeLayout = false,
        uint? dynamicDataSizeOverride = null,
        uint? particleDataSizeOverride = null)
    {
        var shapeBlockBytes = shapes
            .Select((shape, index) => CreateShapeBlock(
                shape with { NameIndex = index },
                dynamicShapeLayout,
                dynamicDataSizeOverride,
                particleDataSizeOverride))
            .ToArray();
        var hasSkinInstance = shapes.Any(static shape => shape.SkinInstance >= 0);
        var blockBytes = hasSkinInstance
            ? shapeBlockBytes.Append(Array.Empty<byte>()).ToArray()
            : shapeBlockBytes;
        var blockTypes = hasSkinInstance
            ? new[] { blockType, skinBlockType }
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
        if (hasSkinInstance) writer.Write((ushort)1);
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
        if (dynamicShapeLayout)
        {
            var dynamicVertices = shape.DynamicVertices ?? [];
            writer.Write(particleDataSizeOverride ?? 0);
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
        IReadOnlyList<(float X, float Y, float Z, float W)>? DynamicVertices = null)
    {
        public int NameIndex { get; init; }
    }
}
