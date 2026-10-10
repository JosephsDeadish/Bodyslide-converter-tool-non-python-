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
        var result = SkyrimSseNifShapeReader.Read(CreateNif([new TestShape("Body", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])], streamVersion: 83));

        Assert.False(result.Supported);
        Assert.Equal("unsupported-nif-profile", result.Diagnostic);
        Assert.Empty(result.Shapes);
    }

    [Fact]
    public void RejectsSkinnedShapesRatherThanTreatingTheirInlinePayloadAsComplete()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("SkinnedBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)], SkinInstance: 1)]));

        Assert.False(result.Supported);
        Assert.Equal("unsupported-skinned-shape:SkinnedBody", result.Diagnostic);
    }

    [Fact]
    public void RejectsUnsupportedDynamicShapeBlocks()
    {
        var result = SkyrimSseNifShapeReader.Read(CreateNif(
            [new TestShape("DynamicBody", [(0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f)])],
            blockType: "BSDynamicTriShape"));

        Assert.False(result.Supported);
        Assert.Equal("unsupported-geometry-block:BSDynamicTriShape", result.Diagnostic);
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
        string blockType = "BSTriShape")
    {
        var blockBytes = shapes
            .Select((shape, index) => CreateShapeBlock(shape with { NameIndex = index }))
            .ToArray();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("Gamebryo File Format, Version 20.2.0.7\n"));
        writer.Write(0x14020007u);
        writer.Write((byte)1);
        writer.Write(12u);
        writer.Write((uint)shapes.Count);
        writer.Write(streamVersion);
        writer.Write(1u);
        WriteSizedString(writer, blockType);
        foreach (var _ in shapes) writer.Write((ushort)0);
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

    private static byte[] CreateShapeBlock(TestShape shape)
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
        var descriptor = (ulong)(uint)(shape.VertexStride / 4) | (1UL << 44);
        writer.Write(descriptor);
        var triangles = shape.Triangles ?? [shape.Triangle ?? (0, 1, 2)];
        writer.Write((ushort)triangles.Count);
        writer.Write((ushort)shape.Vertices.Count);
        writer.Write((uint)(shape.Vertices.Count * shape.VertexStride + triangles.Count * 3 * sizeof(ushort)));
        for (var vertexIndex = 0; vertexIndex < shape.Vertices.Count; vertexIndex++)
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
        foreach (var (a, b, c) in triangles)
        {
            writer.Write((ushort)a);
            writer.Write((ushort)b);
            writer.Write((ushort)c);
        }
        return stream.ToArray();
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
        IReadOnlyList<(int A, int B, int C)>? Triangles = null)
    {
        public int NameIndex { get; init; }
    }
}
