using System.Text;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class MorphPayloadSafetyTests
{
    [Fact]
    public void FaceGenTriRejectsOversizedDeltaCountWithoutArithmeticException()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("FRTRI003"u8);
        writer.Write(1u);
        writer.Write(1u);
        writer.Write((ushort)1);
        writer.Write((byte)'A');
        writer.Write(int.MaxValue);
        Assert.False(TriMorphReader.TryRead(stream.ToArray(), out _));
    }

    [Theory]
    [InlineData(250_000)]
    [InlineData(int.MaxValue)]
    public void OsdRejectsOutOfRangeVertexIndexes(int index)
    {
        Assert.False(OsdMorphReader.TryRead(Osd(index, 1f), out _));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void ReadersRejectNonFiniteDeltas(float value)
    {
        Assert.False(OsdMorphReader.TryRead(Osd(0, value), out _));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("BSD\0"u8);
        writer.Write((ushort)1);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((byte)'A');
        writer.Write(1u);
        writer.Write(value);
        writer.Write(0f);
        writer.Write(0f);
        Assert.False(BsdMorphReader.TryRead(stream.ToArray(), out _));
    }

    [Fact]
    public void SmallSparseBodyTriCannotRequestUnboundedDenseExpansion()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("PIRT"u8);
        writer.Write((ushort)1);
        writer.Write((byte)1);
        writer.Write((byte)'S');
        writer.Write((ushort)129);
        for (var i = 0; i < 129; i++)
        {
            writer.Write((byte)1);
            writer.Write((byte)'A');
            writer.Write(1f);
            writer.Write((ushort)1);
            writer.Write(ushort.MaxValue);
            writer.Write((short)1);
            writer.Write((short)0);
            writer.Write((short)0);
        }
        Assert.False(TriMorphReader.TryRead(stream.ToArray(), out _));
    }

    [Fact]
    public void BodyTriRejectsTrailingBytes()
    {
        Assert.False(TriMorphReader.TryRead("PIRT\0\0extra"u8, out _));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BodyTriAcceptsValidatedOptionalUvSection(bool hasPositionMorph, bool hasUvMorph)
    {
        var bytes = BodyTriWithUv(hasPositionMorph, hasUvMorph);
        Assert.True(TriMorphReader.TryRead(bytes, out var payload));
        Assert.Equal(hasPositionMorph ? 1 : 0, payload!.Shapes.Count);
        if (hasPositionMorph)
        {
            var shape = Assert.Single(payload.Shapes);
            Assert.Equal("S", shape.Name);
            Assert.Equal((1f, 0f, 0f), Assert.Single(shape.Morphs).Deltas[0]);
        }
        Assert.False(TriMorphReader.TryRead(bytes[..^1], out _));
        Assert.False(TriMorphReader.TryRead(bytes.Concat(new byte[] { 1 }).ToArray(), out _));
    }

    [Fact]
    public void BodyTriPreservesMorphsAndVertexCountsForEachShape()
    {
        Assert.True(TriMorphReader.TryRead(
            BodyTriWithShapes(("ShapeA", "Waist", (ushort)0, (short)2), ("ShapeB", "Chest", (ushort)2, (short)3)),
            out var payload));

        var shapes = payload!.Shapes;
        Assert.Equal(2, shapes.Count);
        Assert.Equal(("ShapeA", 1, "Waist", (2f, 0f, 0f)),
            (shapes[0].Name, shapes[0].VertexCount, shapes[0].Morphs[0].Name, shapes[0].Morphs[0].Deltas[0]));
        Assert.Equal(("ShapeB", 3, "Chest", (0f, 0f, 0f)),
            (shapes[1].Name, shapes[1].VertexCount, shapes[1].Morphs[0].Name, shapes[1].Morphs[0].Deltas[0]));
        Assert.Equal((3f, 0f, 0f), shapes[1].Morphs[0].Deltas[2]);
    }

    [Fact]
    public void BodyTriRejectsDuplicateShapeNamesThatWouldMakeAssociationAmbiguous()
    {
        Assert.False(TriMorphReader.TryRead(
            BodyTriWithShapes(("Duplicate", "Waist", (ushort)0, (short)1), ("Duplicate", "Chest", (ushort)1, (short)1)),
            out _));
    }

    [Fact]
    public void ValidFiniteOsdRemainsReadable()
    {
        Assert.True(OsdMorphReader.TryRead(Osd(5, 0.25f), out var payload));
        Assert.Equal(6, payload!.InferredVertexCount);
        Assert.Equal((5, 0.25f, 0f, 0f), Assert.Single(Assert.Single(payload.Morphs).SparseDeltas));
    }

    [Fact]
    public void Int32OsdIndexesAreNotMisreadAsPaddedInt16Payloads()
    {
        Assert.True(OsdMorphReader.TryRead(Osd(65_536, 0.5f), out var payload));
        Assert.Equal(65_537, payload!.InferredVertexCount);
        Assert.Equal((65_536, 0.5f, 0f, 0f), Assert.Single(Assert.Single(payload.Morphs).SparseDeltas));
    }

    [Fact]
    public void OsdWriterRejectsVertexCountsOutsideItsSixteenBitIndexRange()
    {
        Assert.True(LocalExportService.IsOsdVertexCountRepresentable(ushort.MaxValue));
        Assert.False(LocalExportService.IsOsdVertexCountRepresentable((int)ushort.MaxValue + 1));
        Assert.False(LocalExportService.IsOsdVertexCountRepresentable(0));
    }

    [Fact]
    public void OsdWriterRejectsSliderNamesThatCannotFitItsLengthField()
    {
        var longName = new string('A', byte.MaxValue + 1);

        Assert.True(LocalExportService.IsOsdSliderNameRepresentable("Waist"));
        Assert.False(LocalExportService.IsOsdSliderNameRepresentable(longName));
        Assert.False(LocalExportService.IsOsdSliderNameRepresentable(string.Empty));
        Assert.True(LocalExportService.IsOsdSparseRecordCountRepresentable(ushort.MaxValue));
        Assert.False(LocalExportService.IsOsdSparseRecordCountRepresentable((int)ushort.MaxValue + 1));
    }

    [Theory]
    [InlineData("bsd")]
    [InlineData("tri")]
    [InlineData("osd")]
    public void OversizedFilesAreRejectedBeforeReadingTheirContents(string format)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "." + format);
        try
        {
            using (var stream = File.Create(path))
            {
                stream.SetLength((long)MorphPayloadLimits.MaximumFileBytes + 1);
            }
            var accepted = format switch
            {
                "bsd" => BsdMorphReader.TryRead(path, out _),
                "tri" => TriMorphReader.TryRead(path, out _),
                _ => OsdMorphReader.TryRead(path, out _)
            };
            Assert.False(accepted);
        }
        finally { File.Delete(path); }
    }

    private static byte[] Osd(int index, float value)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write("OSD\0"u8);
        writer.Write(1);
        writer.Write(1);
        writer.Write((byte)1);
        writer.Write((byte)'A');
        writer.Write((ushort)1);
        writer.Write(index);
        writer.Write(value);
        writer.Write(0f);
        writer.Write(0f);
        return stream.ToArray();
    }

    private static byte[] BodyTriWithUv(bool positions, bool uvs)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("PIRT"u8);
        foreach (var (included, uv) in new[] { (positions, false), (uvs, true) })
        {
            writer.Write((ushort)(included ? 1 : 0));
            if (!included) continue;
            writer.Write((byte)1);
            writer.Write((byte)'S');
            writer.Write((ushort)1);
            writer.Write((byte)1);
            writer.Write((byte)'A');
            writer.Write(1f);
            writer.Write((ushort)1);
            writer.Write((ushort)0);
            writer.Write((short)1);
            writer.Write((short)0);
            if (!uv) writer.Write((short)0);
        }
        return stream.ToArray();
    }

    private static byte[] BodyTriWithShapes(params (string ShapeName, string MorphName, ushort Index, short X)[] shapes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write("PIRT"u8);
        writer.Write((ushort)shapes.Length);
        foreach (var (shapeName, morphName, index, x) in shapes)
        {
            var encodedShapeName = Encoding.UTF8.GetBytes(shapeName);
            var encodedMorphName = Encoding.UTF8.GetBytes(morphName);
            writer.Write((byte)encodedShapeName.Length);
            writer.Write(encodedShapeName);
            writer.Write((ushort)1);
            writer.Write((byte)encodedMorphName.Length);
            writer.Write(encodedMorphName);
            writer.Write(1f);
            writer.Write((ushort)1);
            writer.Write(index);
            writer.Write(x);
            writer.Write((short)0);
            writer.Write((short)0);
        }
        return stream.ToArray();
    }
}
