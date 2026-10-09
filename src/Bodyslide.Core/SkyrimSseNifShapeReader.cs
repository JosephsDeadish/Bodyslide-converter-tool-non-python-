using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Bodyslide.Core;

internal sealed record SkyrimSseNifShape(
    string Name,
    IReadOnlyList<MeshVertex> Vertices,
    IReadOnlyList<ushort> TriangleIndices,
    int VertexStride,
    int VertexDataOffset = -1);

internal sealed record SkyrimSseNifShapeReadResult(
    bool Supported,
    string Diagnostic,
    IReadOnlyList<SkyrimSseNifShape> Shapes);

internal static class SkyrimSseNifShapeReader
{
    private const string HeaderText = "Gamebryo File Format, Version 20.2.0.7";
    private const uint FileVersion = 0x14020007;
    private const uint SkyrimSeUserVersion = 12;
    private const uint SkyrimSeStreamVersion = 100;
    private const int MaximumBlockCount = 1_000_000;
    private const int MaximumBlockTypeCount = ushort.MaxValue + 1;
    private const int MaximumStringCount = 500_000;
    private const int MaximumStringBytes = 1_048_576;
    private const int MaximumExtraDataReferences = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static SkyrimSseNifShapeReadResult Read(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var shapes = ReadSupportedShapes(bytes);
            return new SkyrimSseNifShapeReadResult(true, "supported-unskinned-bstrishape", shapes);
        }
        catch (InvalidDataException exception)
        {
            return new SkyrimSseNifShapeReadResult(false, exception.Message, []);
        }
        catch (OverflowException)
        {
            return Unsupported("nif-count-or-offset-overflow");
        }
        catch (ArgumentOutOfRangeException)
        {
            return Unsupported("nif-field-outside-input");
        }
        catch (DecoderFallbackException)
        {
            return Unsupported("nif-invalid-utf8-string");
        }
    }

    private static IReadOnlyList<SkyrimSseNifShape> ReadSupportedShapes(ReadOnlySpan<byte> bytes)
    {
        var lineEnd = bytes.IndexOf((byte)'\n');
        if (lineEnd < 0)
        {
            throw new InvalidDataException("nif-header-line-missing");
        }

        var headerLine = Encoding.ASCII.GetString(bytes[..lineEnd]).TrimEnd('\r');
        if (!string.Equals(headerLine, HeaderText, StringComparison.Ordinal))
        {
            throw new InvalidDataException("unsupported-nif-version");
        }

        var offset = lineEnd + 1;
        var version = ReadUInt32(bytes, ref offset);
        var endian = ReadByte(bytes, ref offset);
        var userVersion = ReadUInt32(bytes, ref offset);
        var blockCount = ReadCount(bytes, ref offset, MaximumBlockCount, "nif-block-count-out-of-range");
        var streamVersion = ReadUInt32(bytes, ref offset);
        var blockTypeCount = ReadCount(bytes, ref offset, MaximumBlockTypeCount, "nif-block-type-count-out-of-range");
        if (version != FileVersion ||
            endian != 1 ||
            userVersion != SkyrimSeUserVersion ||
            streamVersion != SkyrimSeStreamVersion)
        {
            throw new InvalidDataException("unsupported-nif-profile");
        }

        var blockTypes = new string[blockTypeCount];
        for (var i = 0; i < blockTypes.Length; i++)
        {
            blockTypes[i] = ReadSizedString(bytes, ref offset);
        }

        var blockTypeIndexes = new ushort[blockCount];
        for (var i = 0; i < blockTypeIndexes.Length; i++)
        {
            blockTypeIndexes[i] = ReadUInt16(bytes, ref offset);
            if (blockTypeIndexes[i] >= blockTypes.Length)
            {
                throw new InvalidDataException("nif-block-type-index-out-of-range");
            }
        }

        var blockSizes = new uint[blockCount];
        for (var i = 0; i < blockSizes.Length; i++)
        {
            blockSizes[i] = ReadUInt32(bytes, ref offset);
        }

        var stringCount = ReadCount(bytes, ref offset, MaximumStringCount, "nif-string-count-out-of-range");
        var maximumStringLength = ReadUInt32(bytes, ref offset);
        if (maximumStringLength > MaximumStringBytes)
        {
            throw new InvalidDataException("nif-maximum-string-length-out-of-range");
        }

        var strings = new string[stringCount];
        for (var i = 0; i < strings.Length; i++)
        {
            strings[i] = ReadSizedString(bytes, ref offset);
            if (Encoding.UTF8.GetByteCount(strings[i]) > maximumStringLength)
            {
                throw new InvalidDataException("nif-string-exceeds-declared-maximum");
            }
        }

        var groupCount = ReadCount(bytes, ref offset, MaximumBlockCount, "nif-group-count-out-of-range");
        Skip(bytes, ref offset, checked(groupCount * sizeof(uint)));
        var blockOffset = offset;
        var totalBlockBytes = 0L;
        foreach (var blockSize in blockSizes)
        {
            totalBlockBytes = checked(totalBlockBytes + blockSize);
        }

        if (totalBlockBytes > bytes.Length - blockOffset)
        {
            throw new InvalidDataException("nif-block-table-exceeds-input");
        }

        var shapes = new List<SkyrimSseNifShape>();
        var shapeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var typeName = blockTypes[blockTypeIndexes[blockIndex]];
            var blockSize = checked((int)blockSizes[blockIndex]);
            var blockEnd = checked(blockOffset + blockSize);

            if (IsUnsupportedGeometryType(typeName))
            {
                throw new InvalidDataException($"unsupported-geometry-block:{typeName}");
            }

            if (string.Equals(typeName, "BSTriShape", StringComparison.Ordinal))
            {
                var shape = ReadBSTriShape(bytes, blockOffset, blockEnd, strings);
                if (!shapeNames.Add(shape.Name))
                {
                    throw new InvalidDataException($"duplicate-shape-name:{shape.Name}");
                }

                shapes.Add(shape);
            }

            blockOffset = blockEnd;
        }

        if (shapes.Count == 0)
        {
            throw new InvalidDataException("nif-has-no-supported-bstrishape");
        }

        return shapes;
    }

    private static SkyrimSseNifShape ReadBSTriShape(
        ReadOnlySpan<byte> bytes,
        int blockStart,
        int blockEnd,
        IReadOnlyList<string> strings)
    {
        var offset = blockStart;
        var nameIndex = ReadUInt32(bytes[..blockEnd], ref offset);
        if (nameIndex >= strings.Count || string.IsNullOrWhiteSpace(strings[(int)nameIndex]))
        {
            throw new InvalidDataException("bstrishape-name-index-invalid");
        }

        var extraDataCount = ReadCount(bytes[..blockEnd], ref offset, MaximumExtraDataReferences, "bstrishape-extra-data-count-out-of-range");
        Skip(bytes[..blockEnd], ref offset, checked(extraDataCount * sizeof(uint)));
        Skip(bytes[..blockEnd], ref offset, sizeof(int));

        const int NiAvObjectFieldBytes = sizeof(uint) + (3 * sizeof(float)) + (9 * sizeof(float)) + sizeof(float) + sizeof(int);
        const int BoundFieldBytes = (3 * sizeof(float)) + sizeof(float);
        Skip(bytes[..blockEnd], ref offset, NiAvObjectFieldBytes + BoundFieldBytes);
        var skinInstance = ReadInt32(bytes[..blockEnd], ref offset);
        Skip(bytes[..blockEnd], ref offset, 2 * sizeof(int));
        if (skinInstance != -1)
        {
            throw new InvalidDataException($"unsupported-skinned-shape:{strings[(int)nameIndex]}");
        }

        var descriptor = ReadUInt64(bytes[..blockEnd], ref offset);
        var triangleCount = ReadUInt16(bytes[..blockEnd], ref offset);
        var vertexCount = ReadUInt16(bytes[..blockEnd], ref offset);
        var dataSize = ReadUInt32(bytes[..blockEnd], ref offset);
        var vertexStride = checked((int)(descriptor & 0xF) * 4);
        var hasPositions = (descriptor & (1UL << 44)) != 0;
        if (!hasPositions || vertexStride < 3 * sizeof(float) || vertexCount == 0 || triangleCount == 0)
        {
            throw new InvalidDataException($"unsupported-bstrishape-vertex-layout:{strings[(int)nameIndex]}");
        }

        var vertexBytes = checked((int)vertexCount * vertexStride);
        var triangleBytes = checked((int)triangleCount * 3 * sizeof(ushort));
        var expectedDataSize = checked(vertexBytes + triangleBytes);
        if (dataSize != expectedDataSize || dataSize > blockEnd - offset)
        {
            throw new InvalidDataException($"bstrishape-data-size-mismatch:{strings[(int)nameIndex]}");
        }

        var vertexDataOffset = offset;
        var vertices = new MeshVertex[vertexCount];
        for (var i = 0; i < vertices.Length; i++)
        {
            var vertexOffset = checked(offset + (i * vertexStride));
            var x = ReadSingleAt(bytes, vertexOffset);
            var y = ReadSingleAt(bytes, vertexOffset + sizeof(float));
            var z = ReadSingleAt(bytes, vertexOffset + (2 * sizeof(float)));
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
            {
                throw new InvalidDataException($"bstrishape-nonfinite-vertex:{strings[(int)nameIndex]}");
            }

            vertices[i] = new MeshVertex(x, y, z);
        }

        var triangleOffset = checked(offset + vertexBytes);
        var triangleIndexes = new ushort[triangleCount * 3];
        for (var i = 0; i < triangleIndexes.Length; i++)
        {
            var index = ReadUInt16At(bytes, checked(triangleOffset + (i * sizeof(ushort))));
            if (index >= vertexCount)
            {
                throw new InvalidDataException($"bstrishape-triangle-index-out-of-range:{strings[(int)nameIndex]}");
            }

            triangleIndexes[i] = index;
        }

        return new SkyrimSseNifShape(strings[(int)nameIndex], vertices, triangleIndexes, vertexStride, vertexDataOffset);
    }

    private static bool IsUnsupportedGeometryType(string typeName) =>
        !string.Equals(typeName, "BSTriShape", StringComparison.Ordinal) &&
        (typeName.Contains("TriShape", StringComparison.Ordinal) ||
         typeName.Contains("TriStrips", StringComparison.Ordinal) ||
         string.Equals(typeName, "NiMesh", StringComparison.Ordinal) ||
         string.Equals(typeName, "BSGeometry", StringComparison.Ordinal));

    private static string ReadSizedString(ReadOnlySpan<byte> bytes, ref int offset)
    {
        var length = ReadCount(bytes, ref offset, MaximumStringBytes, "nif-string-length-out-of-range");
        EnsureAvailable(bytes, offset, length);
        var value = StrictUtf8.GetString(bytes.Slice(offset, length));
        offset = checked(offset + length);
        return value;
    }

    private static int ReadCount(ReadOnlySpan<byte> bytes, ref int offset, int maximum, string diagnostic)
    {
        var value = ReadUInt32(bytes, ref offset);
        if (value > maximum)
        {
            throw new InvalidDataException(diagnostic);
        }

        return checked((int)value);
    }

    private static byte ReadByte(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(byte));
        return bytes[offset++];
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
        offset = checked(offset + sizeof(ushort));
        return value;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(uint));
        var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
        offset = checked(offset + sizeof(uint));
        return value;
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, ref int offset) =>
        unchecked((int)ReadUInt32(bytes, ref offset));

    private static ulong ReadUInt64(ReadOnlySpan<byte> bytes, ref int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(ulong));
        var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..]);
        offset = checked(offset + sizeof(ulong));
        return value;
    }

    private static ushort ReadUInt16At(ReadOnlySpan<byte> bytes, int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(ushort));
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    }

    private static float ReadSingleAt(ReadOnlySpan<byte> bytes, int offset)
    {
        EnsureAvailable(bytes, offset, sizeof(float));
        return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]));
    }

    private static void Skip(ReadOnlySpan<byte> bytes, ref int offset, int length)
    {
        EnsureAvailable(bytes, offset, length);
        offset = checked(offset + length);
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("nif-field-outside-block");
        }
    }

    private static SkyrimSseNifShapeReadResult Unsupported(string diagnostic) =>
        new(false, diagnostic, []);
}

internal static class SkyrimSseNifShapeCorrespondence
{
    public static bool TryVerifyExactOrderedMatch(
        SkyrimSseNifShape source,
        SkyrimSseNifShape target,
        out string evidence)
    {
        evidence = string.Empty;
        if (!string.Equals(source.Name, target.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (source.Vertices.Count == 0 ||
            source.Vertices.Count != target.Vertices.Count ||
            !source.Vertices.SequenceEqual(target.Vertices) ||
            source.TriangleIndices.Count != target.TriangleIndices.Count ||
            !source.TriangleIndices.SequenceEqual(target.TriangleIndices))
        {
            return false;
        }

        evidence = $"exact-ordered-shape-geometry-and-topology-sha256:{ComputeFingerprint(source)}";
        return true;
    }

    private static string ComputeFingerprint(SkyrimSseNifShape shape)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> value = stackalloc byte[sizeof(uint)];
        foreach (var vertex in shape.Vertices)
        {
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.X));
            hash.AppendData(value);
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.Y));
            hash.AppendData(value);
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.Z));
            hash.AppendData(value);
        }

        Span<byte> triangleIndex = stackalloc byte[sizeof(ushort)];
        foreach (var index in shape.TriangleIndices)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(triangleIndex, index);
            hash.AppendData(triangleIndex);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
