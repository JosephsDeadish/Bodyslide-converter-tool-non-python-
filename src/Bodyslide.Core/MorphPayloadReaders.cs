using System.Buffers.Binary;
using System.Text;

namespace Bodyslide.Core;

internal sealed record BsdMorphPayload(string SliderName, bool IsHighWeight, int VertexCount, IReadOnlyList<(float X, float Y, float Z)> Deltas);
internal sealed record TriMorphEntry(string Name, IReadOnlyList<(float X, float Y, float Z)> Deltas);
internal sealed record TriMorphPayload(int VertexCount, IReadOnlyList<TriMorphEntry> Morphs);
internal readonly record struct MorphDeltaStats(int TotalCount, int MeaningfulCount, float TotalMagnitude, float MaxMagnitude)
{
    public float MeaningfulRatio => TotalCount <= 0 ? 0f : MeaningfulCount / (float)TotalCount;
}

internal static class MorphPayloadAnalysis
{
    private const float MeaningfulDeltaThreshold = 0.0001f;

    public static bool HasMeaningfulDeltas(IReadOnlyList<(float X, float Y, float Z)> deltas)
    {
        foreach (var (x, y, z) in deltas)
        {
            if (MathF.Abs(x) > MeaningfulDeltaThreshold ||
                MathF.Abs(y) > MeaningfulDeltaThreshold ||
                MathF.Abs(z) > MeaningfulDeltaThreshold)
            {
                return true;
            }
        }

        return false;
    }

    public static MorphDeltaStats Analyze(IReadOnlyList<(float X, float Y, float Z)> deltas)
    {
        var meaningfulCount = 0;
        var totalMagnitude = 0f;
        var maxMagnitude = 0f;

        foreach (var (x, y, z) in deltas)
        {
            var magnitude = MathF.Sqrt((x * x) + (y * y) + (z * z));
            if (magnitude <= MeaningfulDeltaThreshold)
            {
                continue;
            }

            meaningfulCount++;
            totalMagnitude += magnitude;
            maxMagnitude = Math.Max(maxMagnitude, magnitude);
        }

        return new MorphDeltaStats(deltas.Count, meaningfulCount, totalMagnitude, maxMagnitude);
    }
}

internal static class BsdMorphReader
{
    private static ReadOnlySpan<byte> Magic => "BSD\0"u8;

    public static bool TryRead(string filePath, out BsdMorphPayload? payload)
    {
        payload = null;
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            return TryRead(File.ReadAllBytes(filePath), out payload);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryRead(ReadOnlySpan<byte> bytes, out BsdMorphPayload? payload)
    {
        payload = null;
        if (bytes.Length < 13 || !bytes[..4].SequenceEqual(Magic))
        {
            return false;
        }

        var offset = 4;
        var version = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]); offset += 2;
        if (version == 0)
        {
            return false;
        }

        var isHighWeight = bytes[offset++] != 0;
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]); offset += 2;
        if (offset + nameLength + 4 > bytes.Length)
        {
            return false;
        }

        var sliderName = Encoding.UTF8.GetString(bytes[offset..(offset + nameLength)]); offset += nameLength;
        var vertexCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]); offset += 4;
        if (vertexCount <= 0 || vertexCount > 250_000)
        {
            return false;
        }

        var expectedBytes = checked(vertexCount * 12);
        if (offset + expectedBytes > bytes.Length)
        {
            return false;
        }

        var deltas = new (float X, float Y, float Z)[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var x = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)])); offset += 4;
            var y = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)])); offset += 4;
            var z = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)])); offset += 4;
            deltas[i] = (x, y, z);
        }

        if (offset != bytes.Length)
        {
            return false;
        }

        payload = new BsdMorphPayload(sliderName, isHighWeight, vertexCount, deltas);
        return true;
    }
}

internal static class TriMorphReader
{
    private static ReadOnlySpan<byte> Magic => "FRTRI003"u8;
    private const float DequantizeScale = 1f / 2048f;

    public static bool TryRead(string filePath, out TriMorphPayload? payload)
    {
        payload = null;
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            return TryRead(File.ReadAllBytes(filePath), out payload);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryRead(ReadOnlySpan<byte> bytes, out TriMorphPayload? payload)
    {
        payload = null;
        if (bytes.Length < 16 || !bytes[..8].SequenceEqual(Magic))
        {
            return false;
        }

        var offset = 8;
        var vertexCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]); offset += 4;
        var morphCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]); offset += 4;
        if (vertexCount <= 0 || vertexCount > 250_000 || morphCount <= 0 || morphCount > 10_000)
        {
            return false;
        }

        var names = new List<string>(morphCount);
        var counts = new List<int>(morphCount);
        for (var i = 0; i < morphCount; i++)
        {
            if (offset + 2 > bytes.Length)
            {
                return false;
            }

            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]); offset += 2;
            if (offset + nameLength + 4 > bytes.Length)
            {
                return false;
            }

            names.Add(Encoding.UTF8.GetString(bytes[offset..(offset + nameLength)]));
            offset += nameLength;
            counts.Add((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]));
            offset += 4;
        }

        var morphs = new List<TriMorphEntry>(morphCount);
        for (var i = 0; i < morphCount; i++)
        {
            var deltaCount = counts[i];
            if (deltaCount <= 0 || deltaCount > 250_000)
            {
                return false;
            }

            var expectedBytes = checked(deltaCount * 6);
            if (offset + expectedBytes > bytes.Length)
            {
                return false;
            }

            var deltas = new (float X, float Y, float Z)[deltaCount];
            for (var j = 0; j < deltaCount; j++)
            {
                var x = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * DequantizeScale; offset += 2;
                var y = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * DequantizeScale; offset += 2;
                var z = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * DequantizeScale; offset += 2;
                deltas[j] = (x, y, z);
            }

            morphs.Add(new TriMorphEntry(names[i], deltas));
        }

        if (offset != bytes.Length)
        {
            return false;
        }

        payload = new TriMorphPayload(vertexCount, morphs);
        return true;
    }
}
