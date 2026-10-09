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
        var descriptor = 4UL | (1UL << 44);
        writer.Write(descriptor);
        writer.Write((ushort)1);
        writer.Write((ushort)shape.Vertices.Count);
        writer.Write((uint)(shape.Vertices.Count * 16 + (3 * sizeof(ushort))));
        foreach (var (x, y, z) in shape.Vertices)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
            writer.Write(0f);
        }
        var triangle = shape.Triangle ?? (0, 1, 2);
        writer.Write((ushort)triangle.Item1);
        writer.Write((ushort)triangle.Item2);
        writer.Write((ushort)triangle.Item3);
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
        (int, int, int)? Triangle = null)
    {
        public int NameIndex { get; init; }
    }
}
