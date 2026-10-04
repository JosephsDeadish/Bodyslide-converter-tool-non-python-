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
}
