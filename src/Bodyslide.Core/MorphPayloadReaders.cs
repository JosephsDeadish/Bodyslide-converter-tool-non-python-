using System.Buffers.Binary;
using System.Text;

namespace Bodyslide.Core;

internal sealed record BsdMorphPayload(string SliderName, bool IsHighWeight, int VertexCount, IReadOnlyList<(float X, float Y, float Z)> Deltas);
internal sealed record OsdMorphEntry(string Name, IReadOnlyList<(int Index, float X, float Y, float Z)> SparseDeltas);
internal sealed record OsdMorphPayload(int InferredVertexCount, IReadOnlyList<OsdMorphEntry> Morphs);
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
    private static ReadOnlySpan<byte> LegacyFaceGenMagic => "FRTRI002"u8;
    private static ReadOnlySpan<byte> FaceGenMagic => "FRTRI003"u8;
    private static ReadOnlySpan<byte> BodyTriMagic => "PIRT"u8;
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
        if (bytes.Length >= FaceGenMagic.Length && bytes[..FaceGenMagic.Length].SequenceEqual(FaceGenMagic))
        {
            return TryReadFaceGenTri(bytes, FaceGenMagic, usesQuantizedInt16: true, out payload);
        }

        if (bytes.Length >= LegacyFaceGenMagic.Length && bytes[..LegacyFaceGenMagic.Length].SequenceEqual(LegacyFaceGenMagic))
        {
            return TryReadFaceGenTri(bytes, LegacyFaceGenMagic, usesQuantizedInt16: false, out payload);
        }

        if (bytes.Length >= BodyTriMagic.Length && bytes[..BodyTriMagic.Length].SequenceEqual(BodyTriMagic))
        {
            return TryReadBodyTri(bytes, out payload);
        }

        return false;
    }

    private static bool TryReadFaceGenTri(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> magic,
        bool usesQuantizedInt16,
        out TriMorphPayload? payload)
    {
        payload = null;
        if (bytes.Length < 16 || !bytes[..magic.Length].SequenceEqual(magic))
        {
            return false;
        }

        var offset = magic.Length;
        var rawVertexCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]); offset += 4;
        var rawMorphCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]); offset += 4;
        if (rawVertexCount > int.MaxValue || rawMorphCount > int.MaxValue)
        {
            return false;
        }

        var vertexCount = (int)rawVertexCount;
        var morphCount = (int)rawMorphCount;
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
            var rawDeltaCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..(offset + 4)]);
            if (rawDeltaCount > int.MaxValue)
            {
                return false;
            }

            counts.Add((int)rawDeltaCount);
            offset += 4;
        }

        var morphs = new List<TriMorphEntry>(morphCount);
        var densePayloadBytes = counts.Sum(count => checked(count * (usesQuantizedInt16 ? 6 : 12)));
        var indexedPayloadBytes = counts.Sum(count => checked(count * (usesQuantizedInt16 ? 8 : 14)));
        var remainingBytes = bytes.Length - offset;
        var usesExplicitIndexes = remainingBytes switch
        {
            var value when value == indexedPayloadBytes => true,
            var value when value == densePayloadBytes => false,
            _ => (bool?)null
        };
        if (usesExplicitIndexes is null)
        {
            return false;
        }

        for (var i = 0; i < morphCount; i++)
        {
            var deltaCount = counts[i];
            if (deltaCount <= 0 || deltaCount > vertexCount || deltaCount > 250_000)
            {
                return false;
            }

            var expectedBytes = checked(deltaCount * (usesQuantizedInt16
                ? (usesExplicitIndexes.Value ? 8 : 6)
                : (usesExplicitIndexes.Value ? 14 : 12)));
            if (offset + expectedBytes > bytes.Length)
            {
                return false;
            }

            var deltas = new (float X, float Y, float Z)[vertexCount];
            for (var j = 0; j < deltaCount; j++)
            {
                var vertexIndex = usesExplicitIndexes.Value
                    ? BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)])
                    : j;
                if (usesExplicitIndexes.Value)
                {
                    offset += 2;
                }

                float x;
                float y;
                float z;
                if (usesQuantizedInt16)
                {
                    x = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * DequantizeScale; offset += 2;
                    y = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * DequantizeScale; offset += 2;
                    z = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * DequantizeScale; offset += 2;
                }
                else
                {
                    x = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)])); offset += 4;
                    y = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)])); offset += 4;
                    z = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)])); offset += 4;
                }

                if (vertexIndex >= vertexCount)
                {
                    return false;
                }

                deltas[vertexIndex] = (x, y, z);
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

    private static bool TryReadBodyTri(ReadOnlySpan<byte> bytes, out TriMorphPayload? payload)
    {
        payload = null;
        if (bytes.Length < 6 || !bytes[..BodyTriMagic.Length].SequenceEqual(BodyTriMagic))
        {
            return false;
        }

        var shapeCount = (int)BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..6]);
        if (shapeCount < 0 || shapeCount > 10_000)
        {
            return false;
        }

        if (shapeCount == 0)
        {
            payload = new TriMorphPayload(0, []);
            return true;
        }

        var offset = 6;
        List<TriMorphEntry>? firstShapeMorphs = null;
        var firstShapeVertexCount = 0;

        for (var shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            if (offset + 3 > bytes.Length)
            {
                return false;
            }

            var shapeNameLength = bytes[offset++];
            if (shapeNameLength == 0 || offset + shapeNameLength + 2 > bytes.Length)
            {
                return false;
            }

            offset += shapeNameLength;
            var morphCount = (int)BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]);
            offset += 2;
            if (morphCount < 0 || morphCount > 65_535)
            {
                return false;
            }

            var shapeMorphs = shapeIndex == 0 ? new List<(string Name, List<(int Index, float X, float Y, float Z)> Sparse)>(morphCount) : null;
            var shapeVertexCount = 0;
            for (var morphIndex = 0; morphIndex < morphCount; morphIndex++)
            {
                if (offset + 7 > bytes.Length)
                {
                    return false;
                }

                var morphNameLength = bytes[offset++];
                if (offset + morphNameLength + 6 > bytes.Length)
                {
                    return false;
                }

                var morphName = Encoding.UTF8.GetString(bytes[offset..(offset + morphNameLength)]);
                offset += morphNameLength;
                var multiplier = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]));
                offset += 4;
                var deltaCount = (int)BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]);
                offset += 2;
                if (deltaCount < 0 || deltaCount > 65_535)
                {
                    return false;
                }

                var expectedBytes = checked(deltaCount * 8);
                if (offset + expectedBytes > bytes.Length)
                {
                    return false;
                }

                if (shapeMorphs is null)
                {
                    offset += expectedBytes;
                    continue;
                }

                var sparse = new List<(int Index, float X, float Y, float Z)>(deltaCount);
                for (var deltaIndex = 0; deltaIndex < deltaCount; deltaIndex++)
                {
                    var vertexIndex = (int)BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]);
                    if (vertexIndex < 0 || vertexIndex > 250_000)
                    {
                        payload = default;
                        return false;
                    }

                    offset += 2;
                    var x = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * multiplier; offset += 2;
                    var y = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * multiplier; offset += 2;
                    var z = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * multiplier; offset += 2;
                    sparse.Add((vertexIndex, x, y, z));
                    shapeVertexCount = Math.Max(shapeVertexCount, vertexIndex + 1);
                }

                shapeMorphs.Add((morphName, sparse));
            }

            if (shapeIndex == 0)
            {
                firstShapeMorphs = shapeMorphs?
                    .Select(morph =>
                    {
                        var deltas = new (float X, float Y, float Z)[shapeVertexCount];
                        foreach (var (index, x, y, z) in morph.Sparse)
                        {
                            if (index >= 0 && index < deltas.Length)
                            {
                                deltas[index] = (x, y, z);
                            }
                        }

                        return new TriMorphEntry(morph.Name, deltas);
                    })
                    .ToList() ?? [];
                firstShapeVertexCount = shapeVertexCount;
            }
        }

        if (firstShapeMorphs is null)
        {
            return false;
        }

        payload = new TriMorphPayload(firstShapeVertexCount, firstShapeMorphs);
        return true;
    }
}

internal static class OsdMorphReader
{
    private static ReadOnlySpan<byte> OutfitStudioMagic => [0x4f, 0x53, 0x44, 0x00];
    private static ReadOnlySpan<byte> LegacyMagic => [0x4f, 0x53, 0x44, 0x01];

    public static bool TryRead(string filePath, out OsdMorphPayload? payload)
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

    public static bool TryRead(ReadOnlySpan<byte> bytes, out OsdMorphPayload? payload)
    {
        payload = null;
        if (bytes.Length >= 12 && bytes[..OutfitStudioMagic.Length].SequenceEqual(OutfitStudioMagic))
        {
            return TryReadPayload(bytes, headerSize: 12, morphCount: BinaryPrimitives.ReadInt32LittleEndian(bytes[8..12]), out payload);
        }

        if (bytes.Length >= 8 && bytes[..LegacyMagic.Length].SequenceEqual(LegacyMagic))
        {
            return TryReadPayload(bytes, headerSize: 8, morphCount: BinaryPrimitives.ReadInt32LittleEndian(bytes[4..8]), out payload);
        }

        return false;
    }

    private static bool TryReadPayload(
        ReadOnlySpan<byte> bytes,
        int headerSize,
        int morphCount,
        out OsdMorphPayload? payload)
    {
        payload = null;
        if (bytes.Length < headerSize)
        {
            return false;
        }

        var offset = headerSize;
        if (morphCount <= 0 || morphCount > 65_535)
        {
            return false;
        }

        var morphs = new List<OsdMorphEntry>(morphCount);
        var inferredVertexCount = 0;
        for (var morphIndex = 0; morphIndex < morphCount; morphIndex++)
        {
            if (offset + 3 > bytes.Length)
            {
                return false;
            }

            var nameLength = bytes[offset++];
            if (offset + nameLength + 2 > bytes.Length)
            {
                return false;
            }

            var morphName = Encoding.UTF8.GetString(bytes[offset..(offset + nameLength)]);
            offset += nameLength;
            var deltaCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]);
            offset += 2;
            if (deltaCount == 0)
            {
                morphs.Add(new OsdMorphEntry(morphName, []));
                continue;
            }

            var expectedBytes = checked(deltaCount * 14);
            if (offset + expectedBytes > bytes.Length)
            {
                return false;
            }

            var sparseDeltas = new List<(int Index, float X, float Y, float Z)>(deltaCount);
            for (var deltaIndex = 0; deltaIndex < deltaCount; deltaIndex++)
            {
                var vertexIndex = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]);
                offset += 2;
                var x = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]));
                offset += 4;
                var y = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]));
                offset += 4;
                var z = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]));
                offset += 4;
                sparseDeltas.Add((vertexIndex, x, y, z));
                inferredVertexCount = Math.Max(inferredVertexCount, vertexIndex + 1);
            }

            morphs.Add(new OsdMorphEntry(morphName, sparseDeltas));
        }

        if ((offset != bytes.Length && !HasOnlyTrailingZeroPadding(bytes[offset..])) || morphs.Count == 0)
        {
            return false;
        }

        payload = new OsdMorphPayload(inferredVertexCount, morphs);
        return true;
    }

    private static bool HasOnlyTrailingZeroPadding(ReadOnlySpan<byte> trailingBytes)
    {
        foreach (var value in trailingBytes)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return trailingBytes.Length > 0;
    }
}
