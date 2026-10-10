using System.Buffers.Binary;
using System.Text;

namespace Bodyslide.Core;

internal sealed record BsdMorphPayload(string SliderName, bool IsHighWeight, int VertexCount, IReadOnlyList<(float X, float Y, float Z)> Deltas);
internal sealed record OsdMorphEntry(string Name, IReadOnlyList<(int Index, float X, float Y, float Z)> SparseDeltas);
internal sealed record OsdMorphPayload(int InferredVertexCount, IReadOnlyList<OsdMorphEntry> Morphs);
internal sealed record TriMorphEntry(string Name, IReadOnlyList<(float X, float Y, float Z)> Deltas);
internal sealed record TriMorphShape(string? Name, int VertexCount, IReadOnlyList<TriMorphEntry> Morphs);
internal sealed record TriMorphPayload(IReadOnlyList<TriMorphShape> Shapes);
internal readonly record struct MorphDeltaStats(int TotalCount, int MeaningfulCount, float TotalMagnitude, float MaxMagnitude)
{
    public float MeaningfulRatio => TotalCount <= 0 ? 0f : MeaningfulCount / (float)TotalCount;
}

internal static class MorphPayloadLimits
{
    internal const int MaximumFileBytes = 64 * 1024 * 1024;
    internal const int MaximumVertices = 250_000;
    internal const int MaximumExpandedDeltas = 8 * 1024 * 1024;

    internal static byte[] ReadFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        if (stream.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("Morph payload exceeds the 64 MiB read limit.");
        }
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    internal static bool IsFinite(float x, float y, float z) =>
        float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z);
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
            return TryRead(MorphPayloadLimits.ReadFile(filePath), out payload);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
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
        if (bytes.Length < 13 || bytes.Length > MorphPayloadLimits.MaximumFileBytes || !bytes[..4].SequenceEqual(Magic))
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
            if (!MorphPayloadLimits.IsFinite(x, y, z))
            {
                return false;
            }
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
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
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
            return TryRead(MorphPayloadLimits.ReadFile(filePath), out payload);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
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
        if (bytes.Length > MorphPayloadLimits.MaximumFileBytes)
        {
            return false;
        }
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
            try
            {
                return TryReadBodyTri(bytes, out payload);
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
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
        if (vertexCount <= 0 || vertexCount > MorphPayloadLimits.MaximumVertices || morphCount <= 0 || morphCount > 10_000 ||
            (long)vertexCount * morphCount > MorphPayloadLimits.MaximumExpandedDeltas)
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
            if (rawDeltaCount == 0 || rawDeltaCount > vertexCount)
            {
                return false;
            }

            counts.Add((int)rawDeltaCount);
            offset += 4;
        }

        var morphs = new List<TriMorphEntry>(morphCount);
        var densePayloadBytes = counts.Sum(count => (long)count * (usesQuantizedInt16 ? 6 : 12));
        var indexedPayloadBytes = counts.Sum(count => (long)count * (usesQuantizedInt16 ? 8 : 14));
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
            var vertexIndexes = usesExplicitIndexes.Value ? new HashSet<int>() : null;
            for (var j = 0; j < deltaCount; j++)
            {
                var vertexIndex = usesExplicitIndexes.Value
                    ? BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)])
                    : j;
                if (vertexIndexes is not null && !vertexIndexes.Add(vertexIndex))
                {
                    return false;
                }
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

                if (vertexIndex >= vertexCount || !MorphPayloadLimits.IsFinite(x, y, z))
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

        payload = new TriMorphPayload([new TriMorphShape(null, vertexCount, morphs)]);
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
            var emptyOffset = 6;
            if (!TryConsumeBodyTriUvSection(bytes, ref emptyOffset)) return false;
            payload = new TriMorphPayload([]);
            return true;
        }

        var offset = 6;
        var shapes = new List<TriMorphShape>(shapeCount);
        var shapeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expandedDeltaCount = 0;

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

            var shapeName = StrictUtf8.GetString(bytes.Slice(offset, shapeNameLength));
            if (string.IsNullOrWhiteSpace(shapeName) || !shapeNames.Add(shapeName))
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

            var shapeMorphs = new List<(string Name, List<(int Index, float X, float Y, float Z)> Sparse)>(morphCount);
            var morphNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

                var morphName = StrictUtf8.GetString(bytes[offset..(offset + morphNameLength)]);
                if (!morphNames.Add(morphName))
                {
                    return false;
                }
                offset += morphNameLength;
                var multiplier = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]));
                offset += 4;
                if (!float.IsFinite(multiplier))
                {
                    return false;
                }
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

                var sparse = new List<(int Index, float X, float Y, float Z)>(deltaCount);
                var vertexIndexes = new HashSet<int>();
                for (var deltaIndex = 0; deltaIndex < deltaCount; deltaIndex++)
                {
                    var vertexIndex = (int)BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]);
                    if (vertexIndex < 0 || vertexIndex > 250_000)
                    {
                        payload = default;
                        return false;
                    }
                    if (!vertexIndexes.Add(vertexIndex))
                    {
                        return false;
                    }

                    offset += 2;
                    var x = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * multiplier; offset += 2;
                    var y = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * multiplier; offset += 2;
                    var z = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..(offset + 2)]) * multiplier; offset += 2;
                    if (!MorphPayloadLimits.IsFinite(x, y, z))
                    {
                        return false;
                    }
                    sparse.Add((vertexIndex, x, y, z));
                    shapeVertexCount = Math.Max(shapeVertexCount, vertexIndex + 1);
                }

                shapeMorphs.Add((morphName, sparse));
            }

            expandedDeltaCount += (long)shapeVertexCount * morphCount;
            if (expandedDeltaCount > MorphPayloadLimits.MaximumExpandedDeltas ||
                shapeVertexCount > MorphPayloadLimits.MaximumVertices)
            {
                return false;
            }

            var morphEntries = shapeMorphs
                .Select(morph =>
                {
                    var deltas = new (float X, float Y, float Z)[shapeVertexCount];
                    foreach (var (index, x, y, z) in morph.Sparse)
                    {
                        deltas[index] = (x, y, z);
                    }

                    return new TriMorphEntry(morph.Name, deltas);
                })
                .ToList();
            shapes.Add(new TriMorphShape(shapeName, shapeVertexCount, morphEntries));
        }

        if (!TryConsumeBodyTriUvSection(bytes, ref offset))
        {
            return false;
        }

        payload = new TriMorphPayload(shapes);
        return true;
    }

    private static bool TryConsumeBodyTriUvSection(ReadOnlySpan<byte> bytes, ref int offset)
    {
        if (offset == bytes.Length) return true;
        if (bytes.Length - offset < 2) return false;
        var shapeCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
        offset += 2;
        if (shapeCount > 10_000) return false;
        for (var shape = 0; shape < shapeCount; shape++)
        {
            if (bytes.Length - offset < 3) return false;
            var nameLength = bytes[offset++];
            if (bytes.Length - offset < nameLength + 2) return false;
            offset += nameLength;
            var morphCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
            offset += 2;
            for (var morph = 0; morph < morphCount; morph++)
            {
                if (bytes.Length - offset < 7) return false;
                var morphNameLength = bytes[offset++];
                if (bytes.Length - offset < morphNameLength + 6) return false;
                offset += morphNameLength;
                var multiplier = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]));
                offset += 4;
                if (!float.IsFinite(multiplier)) return false;
                var deltaCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
                offset += 2;
                if (bytes.Length - offset < deltaCount * 6) return false;
                for (var delta = 0; delta < deltaCount; delta++)
                {
                    offset += 2;
                    var x = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]) * multiplier;
                    offset += 2;
                    var y = BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]) * multiplier;
                    offset += 2;
                    if (!MorphPayloadLimits.IsFinite(x, y, 0f)) return false;
                }
            }
        }
        return offset == bytes.Length;
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
            return TryRead(MorphPayloadLimits.ReadFile(filePath), out payload);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
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
        if (bytes.Length > MorphPayloadLimits.MaximumFileBytes)
        {
            return false;
        }
        if (bytes.Length >= 12 && bytes[..OutfitStudioMagic.Length].SequenceEqual(OutfitStudioMagic))
        {
            var version = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..8]);
            var morphCount = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..12]);
            if (version <= 0)
            {
                return false;
            }

            return TryReadPayload(bytes, headerSize: 12, morphCount: morphCount, out payload);
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
        foreach (var (indexByteWidth, allowPadding) in new[] { (2, false), (4, false), (2, true), (4, true) })
        {
            if (TryReadPayload(bytes, headerSize, morphCount, indexByteWidth, allowPadding, out payload, out var duplicateVertexIndex))
            {
                break;
            }
            if (duplicateVertexIndex)
            {
                payload = null;
                return false;
            }
        }

        if (payload is null)
        {
            return false;
        }

        if (payload.Morphs.Any(morph => morph.SparseDeltas.Any(delta =>
                delta.Index < 0 || delta.Index >= MorphPayloadLimits.MaximumVertices ||
                !MorphPayloadLimits.IsFinite(delta.X, delta.Y, delta.Z))))
        {
            payload = null;
            return false;
        }
        return true;
    }

    private static bool TryReadPayload(
        ReadOnlySpan<byte> bytes,
        int headerSize,
        int morphCount,
        int indexByteWidth,
        bool allowPadding,
        out OsdMorphPayload? payload,
        out bool duplicateVertexIndex)
    {
        payload = null;
        duplicateVertexIndex = false;
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

            var expectedBytes = checked(deltaCount * (indexByteWidth + 12));
            if (offset + expectedBytes > bytes.Length)
            {
                return false;
            }

            var sparseDeltas = new List<(int Index, float X, float Y, float Z)>(deltaCount);
            var vertexIndexes = new HashSet<int>();
            for (var deltaIndex = 0; deltaIndex < deltaCount; deltaIndex++)
            {
                int vertexIndex;
                if (indexByteWidth == 2)
                {
                    vertexIndex = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..(offset + 2)]);
                    offset += 2;
                }
                else
                {
                    vertexIndex = BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]);
                    offset += 4;
                }
                if (!vertexIndexes.Add(vertexIndex))
                {
                    duplicateVertexIndex = true;
                    return false;
                }

                var x = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]));
                offset += 4;
                var y = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]));
                offset += 4;
                var z = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..(offset + 4)]));
                offset += 4;
                sparseDeltas.Add((vertexIndex, x, y, z));
                if (vertexIndex >= 0 && vertexIndex < MorphPayloadLimits.MaximumVertices)
                {
                    inferredVertexCount = Math.Max(inferredVertexCount, vertexIndex + 1);
                }
            }

            morphs.Add(new OsdMorphEntry(morphName, sparseDeltas));
        }

        if ((offset != bytes.Length && (!allowPadding || !HasOnlyTrailingZeroPadding(bytes[offset..]))) || morphs.Count == 0)
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
