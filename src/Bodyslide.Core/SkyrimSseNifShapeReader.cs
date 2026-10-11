using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Bodyslide.Core;

internal sealed record SkyrimSseNifShape(
    string Name,
    IReadOnlyList<MeshVertex> Vertices,
    IReadOnlyList<ushort> TriangleIndices,
    int VertexStride,
    int VertexDataOffset = -1,
    bool IsSkinned = false,
    string GeometryBlockType = "BSTriShape",
    IReadOnlyList<MeshVertex>? ParticleVertices = null,
    IReadOnlyList<MeshVertex>? ParticleNormals = null,
    IReadOnlyList<ushort>? ParticleTriangleIndices = null,
    ulong VertexDescriptor = 0,
    int SkinInstanceBlockIndex = -1,
    string? SkinInstanceType = null,
    int SkinDataBlockIndex = -1,
    int SkinPartitionBlockIndex = -1,
    int SkeletonRootBlockIndex = -1,
    IReadOnlyList<int>? SkinBoneBlockIndices = null,
    int SkinPartitionCount = 0,
    IReadOnlyList<SkyrimSseNifBodyPartition>? BodyPartitions = null);

internal sealed record SkyrimSseNifBodyPartition(ushort Flags, ushort BodyPart);

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
    private const int MaximumSkinBoneCount = 4096;
    private const int MaximumSkinPartitionCount = 4096;
    private const int MaximumSkinWeightCount = 8 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> NiNodeTypes = new(StringComparer.Ordinal)
    {
        "NiNode", "NiBillboardNode", "NiSwitchNode", "NiSortAdjustNode", "NiRoomGroup", "NiRoom",
        "NiBSAnimationNode", "NiBSParticleNode", "NiCollisionSwitch", "NiBone", "BSFadeNode",
        "BSValueNode", "BSOrderedNode", "BSRangeNode", "BSMultiBoundNode", "BSLeafAnimNode",
        "BSTreeNode"
    };

    public static bool HasSupportedProfile(ReadOnlySpan<byte> bytes)
    {
        var lineEnd = bytes.IndexOf((byte)'\n');
        if (lineEnd < 0 ||
            !string.Equals(Encoding.ASCII.GetString(bytes[..lineEnd]).TrimEnd('\r'), HeaderText, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var offset = lineEnd + 1;
            var version = ReadUInt32(bytes, ref offset);
            var endian = ReadByte(bytes, ref offset);
            var userVersion = ReadUInt32(bytes, ref offset);
            _ = ReadUInt32(bytes, ref offset);
            var streamVersion = ReadUInt32(bytes, ref offset);
            return version == FileVersion &&
                   endian == 1 &&
                   userVersion == SkyrimSeUserVersion &&
                   streamVersion == SkyrimSeStreamVersion;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static bool HasRecognizedNifHeader(ReadOnlySpan<byte> bytes)
    {
        var lineEnd = bytes.IndexOf((byte)'\n');
        if (lineEnd < 0)
        {
            return false;
        }

        var header = Encoding.ASCII.GetString(bytes[..lineEnd]).TrimEnd('\r');
        if (!header.StartsWith("Gamebryo File Format, Version ", StringComparison.Ordinal) &&
            !header.StartsWith("NetImmerse File Format, Version ", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var offset = lineEnd + 1;
            var version = ReadUInt32(bytes, ref offset);
            var endian = ReadByte(bytes, ref offset);
            _ = ReadUInt32(bytes, ref offset);
            var blockCount = ReadUInt32(bytes, ref offset);
            _ = ReadUInt32(bytes, ref offset);
            return version != 0 && endian <= 1 && blockCount <= MaximumBlockCount;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static SkyrimSseNifShapeReadResult Read(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var shapes = ReadSupportedShapes(bytes);
            var diagnostic = shapes.Any(static shape => shape.GeometryBlockType == "BSDynamicTriShape")
                ? "supported-bsdynamictrishape-dynamic-position-stream"
                : shapes.Any(static shape => shape.GeometryBlockType == "BSSubIndexTriShape")
                ? "supported-bssubindextrishape-position-index-only-segments-opaque"
                : shapes.Any(static shape => shape.IsSkinned)
                ? "supported-skinned-bstrishape-position-index-only"
                : "supported-unskinned-bstrishape";
            return new SkyrimSseNifShapeReadResult(true, diagnostic, shapes);
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

        var blockStarts = new int[blockCount];
        var blockEnds = new int[blockCount];
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            blockStarts[blockIndex] = blockOffset;
            blockOffset = checked(blockOffset + (int)blockSizes[blockIndex]);
            blockEnds[blockIndex] = blockOffset;
        }

        var shapes = new List<SkyrimSseNifShape>();
        var shapeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var typeName = blockTypes[blockTypeIndexes[blockIndex]];

            var isSupportedShapeType = string.Equals(typeName, "BSTriShape", StringComparison.Ordinal) ||
                                       string.Equals(typeName, "BSSubIndexTriShape", StringComparison.Ordinal) ||
                                       string.Equals(typeName, "BSDynamicTriShape", StringComparison.Ordinal);
            if (!isSupportedShapeType && IsUnsupportedGeometryType(typeName))
            {
                throw new InvalidDataException($"unsupported-geometry-block:{typeName}");
            }

            if (isSupportedShapeType)
            {
                var shape = ReadBSTriShape(
                    bytes,
                    blockStarts[blockIndex],
                    blockEnds[blockIndex],
                    strings,
                    blockTypes,
                    blockTypeIndexes,
                    blockStarts,
                    blockEnds,
                    typeName);
                if (!shapeNames.Add(shape.Name))
                {
                    throw new InvalidDataException($"duplicate-shape-name:{shape.Name}");
                }

                shapes.Add(shape);
            }

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
        IReadOnlyList<string> strings,
        IReadOnlyList<string> blockTypes,
        IReadOnlyList<ushort> blockTypeIndexes,
        IReadOnlyList<int> blockStarts,
        IReadOnlyList<int> blockEnds,
        string geometryBlockType)
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
        if (skinInstance < -1)
        {
            throw new InvalidDataException($"unsupported-skin-instance-reference:{strings[(int)nameIndex]}");
        }

        var isSkinned = skinInstance != -1;
        string? skinInstanceType = null;
        if (isSkinned &&
            (skinInstance < 0 ||
             skinInstance >= blockTypeIndexes.Count ||
             blockTypeIndexes[skinInstance] >= blockTypes.Count ||
             !IsSupportedSkinInstanceType(blockTypes[blockTypeIndexes[skinInstance]])))
        {
            throw new InvalidDataException($"unsupported-skin-instance-reference:{strings[(int)nameIndex]}");
        }
        if (isSkinned)
        {
            skinInstanceType = blockTypes[blockTypeIndexes[skinInstance]];
        }

        var descriptor = ReadUInt64(bytes[..blockEnd], ref offset);
        var triangleCount = ReadUInt16(bytes[..blockEnd], ref offset);
        var vertexCount = ReadUInt16(bytes[..blockEnd], ref offset);
        var skinData = isSkinned
            ? ReadSkinInstance(
                bytes,
                skinInstance,
                skinInstanceType!,
                strings[(int)nameIndex],
                blockTypes,
                blockTypeIndexes,
                blockStarts,
                blockEnds,
                vertexCount)
            : null;
        var dataSize = ReadUInt32(bytes[..blockEnd], ref offset);
        var vertexStride = checked((int)(descriptor & 0xF) * 4);
        var hasPositions = (descriptor & (1UL << 44)) != 0;
        var isDynamic = string.Equals(geometryBlockType, "BSDynamicTriShape", StringComparison.Ordinal);
        if ((!hasPositions && !isDynamic) ||
            (hasPositions && vertexStride < 3 * sizeof(float)) ||
            vertexCount == 0 ||
            triangleCount == 0)
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

        var particleDataOffset = checked(triangleOffset + triangleBytes);
        var particleDataSize = ReadUInt32(bytes[..blockEnd], ref particleDataOffset);
        var expectedParticleDataSize = checked((uint)vertexCount * 6u + (uint)triangleCount * 3u);
        if (particleDataSize != expectedParticleDataSize)
        {
            throw new InvalidDataException($"bstrishape-particle-data-size-mismatch:{strings[(int)nameIndex]}");
        }

        var particleVertices = ReadHalfVector3Array(bytes[..blockEnd], ref particleDataOffset, vertexCount, "particle-vertex", strings[(int)nameIndex]);
        var particleNormals = ReadHalfVector3Array(bytes[..blockEnd], ref particleDataOffset, vertexCount, "particle-normal", strings[(int)nameIndex]);
        var particleTriangleIndexes = new ushort[triangleCount * 3];
        for (var i = 0; i < particleTriangleIndexes.Length; i++)
        {
            var index = ReadUInt16(bytes[..blockEnd], ref particleDataOffset);
            if (index >= vertexCount)
            {
                throw new InvalidDataException($"bstrishape-particle-triangle-index-out-of-range:{strings[(int)nameIndex]}");
            }

            particleTriangleIndexes[i] = index;
        }

        if (!isDynamic &&
            !string.Equals(geometryBlockType, "BSSubIndexTriShape", StringComparison.Ordinal) &&
            particleDataOffset != blockEnd)
        {
            throw new InvalidDataException($"bstrishape-unmodeled-trailing-data:{strings[(int)nameIndex]}");
        }

        MeshVertex[] vertices;
        if (isDynamic)
        {
            if (hasPositions)
            {
                throw new InvalidDataException($"unsupported-bsdynamictrishape-static-position-stream:{strings[(int)nameIndex]}");
            }

            var dynamicSizeOffset = particleDataOffset;
            var dynamicDataSize = ReadUInt32(bytes[..blockEnd], ref dynamicSizeOffset);
            var expectedDynamicDataSize = checked((uint)vertexCount * 4u * sizeof(float));
            if (dynamicDataSize != expectedDynamicDataSize)
            {
                throw new InvalidDataException($"bstrishape-dynamic-data-size-mismatch:{strings[(int)nameIndex]}");
            }
            if (dynamicDataSize > blockEnd - dynamicSizeOffset)
            {
                throw new InvalidDataException($"bstrishape-dynamic-data-truncated:{strings[(int)nameIndex]}");
            }
            if (checked(dynamicSizeOffset + (int)dynamicDataSize) != blockEnd)
            {
                throw new InvalidDataException($"bstrishape-unmodeled-trailing-data:{strings[(int)nameIndex]}");
            }

            vertexDataOffset = dynamicSizeOffset;
            vertexStride = 4 * sizeof(float);
            vertices = new MeshVertex[vertexCount];
            for (var i = 0; i < vertices.Length; i++)
            {
                var vertexOffset = checked(vertexDataOffset + (i * vertexStride));
                var x = ReadSingleAt(bytes, vertexOffset);
                var y = ReadSingleAt(bytes, vertexOffset + sizeof(float));
                var z = ReadSingleAt(bytes, vertexOffset + (2 * sizeof(float)));
                var w = ReadSingleAt(bytes, vertexOffset + (3 * sizeof(float)));
                if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(w))
                {
                    throw new InvalidDataException($"bstrishape-nonfinite-vertex:{strings[(int)nameIndex]}");
                }

                vertices[i] = new MeshVertex(x, y, z);
            }
        }
        else
        {
            vertices = new MeshVertex[vertexCount];
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
        }

        return new SkyrimSseNifShape(
            strings[(int)nameIndex],
            vertices,
            triangleIndexes,
            vertexStride,
            vertexDataOffset,
            isSkinned,
            geometryBlockType,
            particleVertices,
            particleNormals,
            particleTriangleIndexes,
            descriptor,
            skinInstance,
            skinInstanceType,
            skinData?.SkinDataBlockIndex ?? -1,
            skinData?.SkinPartitionBlockIndex ?? -1,
            skinData?.SkeletonRootBlockIndex ?? -1,
            skinData?.BoneBlockIndices ?? [],
            skinData?.PartitionCount ?? 0,
            skinData?.BodyPartitions ?? []);
    }

    private static SkinInstanceData ReadSkinInstance(
        ReadOnlySpan<byte> bytes,
        int skinInstanceBlockIndex,
        string skinInstanceType,
        string shapeName,
        IReadOnlyList<string> blockTypes,
        IReadOnlyList<ushort> blockTypeIndexes,
        IReadOnlyList<int> blockStarts,
        IReadOnlyList<int> blockEnds,
        int shapeVertexCount)
    {
        var offset = blockStarts[skinInstanceBlockIndex];
        var blockEnd = blockEnds[skinInstanceBlockIndex];
        var skinDataBlockIndex = ReadRequiredBlockReference(
            bytes, ref offset, blockEnd, "NiSkinData", shapeName, blockTypes, blockTypeIndexes);
        var skinPartitionBlockIndex = ReadRequiredBlockReference(
            bytes, ref offset, blockEnd, "NiSkinPartition", shapeName, blockTypes, blockTypeIndexes);
        var skeletonRootBlockIndex = ReadRequiredNodeReference(
            bytes, ref offset, blockEnd, "skeleton-root", shapeName, blockTypes, blockTypeIndexes);

        var boneCount = ReadCount(bytes[..blockEnd], ref offset, MaximumSkinBoneCount, "skin-bone-count-out-of-range");
        var boneBlockIndexes = new int[boneCount];
        for (var boneIndex = 0; boneIndex < boneCount; boneIndex++)
        {
            boneBlockIndexes[boneIndex] = ReadRequiredNodeReference(
                bytes, ref offset, blockEnd, "skin-bone", shapeName, blockTypes, blockTypeIndexes);
        }

        var bodyPartitions = new List<SkyrimSseNifBodyPartition>();
        if (string.Equals(skinInstanceType, "BSDismemberSkinInstance", StringComparison.Ordinal))
        {
            var bodyPartitionCount = ReadCount(
                bytes[..blockEnd], ref offset, MaximumSkinPartitionCount, "dismember-partition-count-out-of-range");
            for (var partitionIndex = 0; partitionIndex < bodyPartitionCount; partitionIndex++)
            {
                var flags = ReadUInt16(bytes[..blockEnd], ref offset);
                var bodyPart = ReadUInt16(bytes[..blockEnd], ref offset);
                bodyPartitions.Add(new SkyrimSseNifBodyPartition(flags, bodyPart));
            }
        }

        if (offset != blockEnd)
        {
            throw new InvalidDataException($"skin-instance-unmodeled-trailing-data:{shapeName}");
        }

        ReadSkinData(
            bytes,
            blockStarts[skinDataBlockIndex],
            blockEnds[skinDataBlockIndex],
            boneCount,
            shapeVertexCount,
            shapeName);
        var partitionCount = ReadSkinPartition(
            bytes,
            blockStarts[skinPartitionBlockIndex],
            blockEnds[skinPartitionBlockIndex],
            boneCount,
            shapeVertexCount,
            shapeName);

        if (string.Equals(skinInstanceType, "BSDismemberSkinInstance", StringComparison.Ordinal) &&
            bodyPartitions.Count != partitionCount)
        {
            throw new InvalidDataException($"dismember-partition-count-mismatch:{shapeName}");
        }

        return new SkinInstanceData(
            skinDataBlockIndex,
            skinPartitionBlockIndex,
            skeletonRootBlockIndex,
            boneBlockIndexes,
            partitionCount,
            bodyPartitions);
    }

    private static void ReadSkinData(
        ReadOnlySpan<byte> bytes,
        int blockStart,
        int blockEnd,
        int expectedBoneCount,
        int shapeVertexCount,
        string shapeName)
    {
        var offset = blockStart;
        ReadFiniteSingles(bytes[..blockEnd], ref offset, 13, "skin-data-transform", shapeName);
        var boneCount = ReadCount(bytes[..blockEnd], ref offset, MaximumSkinBoneCount, "skin-data-bone-count-out-of-range");
        if (boneCount != expectedBoneCount)
        {
            throw new InvalidDataException($"skin-data-bone-count-mismatch:{shapeName}");
        }

        var hasVertexWeights = ReadBoolean(bytes[..blockEnd], ref offset, "skin-data-vertex-weights-flag", shapeName);
        var totalWeightCount = 0;
        for (var boneIndex = 0; boneIndex < boneCount; boneIndex++)
        {
            ReadFiniteSingles(bytes[..blockEnd], ref offset, 13, "skin-bone-transform", shapeName);
            var boundX = ReadSingle(bytes[..blockEnd], ref offset);
            var boundY = ReadSingle(bytes[..blockEnd], ref offset);
            var boundZ = ReadSingle(bytes[..blockEnd], ref offset);
            var boundRadius = ReadSingle(bytes[..blockEnd], ref offset);
            if (!float.IsFinite(boundX) || !float.IsFinite(boundY) || !float.IsFinite(boundZ) ||
                !float.IsFinite(boundRadius) || boundRadius < 0)
            {
                throw new InvalidDataException($"skin-bone-bound-invalid:{shapeName}");
            }

            var vertexWeightCount = ReadUInt16(bytes[..blockEnd], ref offset);
            totalWeightCount = checked(totalWeightCount + vertexWeightCount);
            if (totalWeightCount > MaximumSkinWeightCount)
            {
                throw new InvalidDataException($"skin-data-weight-count-out-of-range:{shapeName}");
            }
            if (!hasVertexWeights && vertexWeightCount != 0)
            {
                throw new InvalidDataException($"skin-data-weights-missing:{shapeName}");
            }

            var seenVertices = new HashSet<ushort>();
            for (var weightIndex = 0; hasVertexWeights && weightIndex < vertexWeightCount; weightIndex++)
            {
                var vertexIndex = ReadUInt16(bytes[..blockEnd], ref offset);
                var weight = ReadSingle(bytes[..blockEnd], ref offset);
                if (vertexIndex >= shapeVertexCount || !seenVertices.Add(vertexIndex) ||
                    !float.IsFinite(weight) || weight < 0 || weight > 1)
                {
                    throw new InvalidDataException($"skin-data-vertex-weight-invalid:{shapeName}");
                }
            }
        }

        if (offset != blockEnd)
        {
            throw new InvalidDataException($"skin-data-unmodeled-trailing-data:{shapeName}");
        }
    }

    private static int ReadSkinPartition(
        ReadOnlySpan<byte> bytes,
        int blockStart,
        int blockEnd,
        int skinBoneCount,
        int shapeVertexCount,
        string shapeName)
    {
        var offset = blockStart;
        var partitionCount = ReadCount(
            bytes[..blockEnd], ref offset, MaximumSkinPartitionCount, "skin-partition-count-out-of-range");
        var vertexDataSize = ReadCount(bytes[..blockEnd], ref offset, 64 * 1024 * 1024, "skin-partition-vertex-data-size-out-of-range");
        var vertexSize = ReadUInt32(bytes[..blockEnd], ref offset);
        var vertexDescriptor = ReadUInt64(bytes[..blockEnd], ref offset);
        var descriptorStride = checked((uint)(vertexDescriptor & 0xF) * 4u);
        if ((vertexDataSize > 0 && (vertexSize == 0 || vertexSize != descriptorStride || vertexDataSize % vertexSize != 0)) ||
            (vertexDataSize == 0 && vertexSize != 0 && descriptorStride != vertexSize))
        {
            throw new InvalidDataException($"skin-partition-vertex-layout-invalid:{shapeName}");
        }
        Skip(bytes[..blockEnd], ref offset, vertexDataSize);

        var totalWeightCount = 0;
        var totalPartitionVertexCount = 0;
        var totalPartitionTriangleCount = 0;
        var totalStripCount = 0;
        for (var partitionIndex = 0; partitionIndex < partitionCount; partitionIndex++)
        {
            var vertexCount = ReadUInt16(bytes[..blockEnd], ref offset);
            var triangleCount = ReadUInt16(bytes[..blockEnd], ref offset);
            var boneCount = ReadUInt16(bytes[..blockEnd], ref offset);
            var stripCount = ReadUInt16(bytes[..blockEnd], ref offset);
            var weightsPerVertex = ReadUInt16(bytes[..blockEnd], ref offset);
            if (boneCount > skinBoneCount || (vertexCount > 0 && weightsPerVertex == 0))
            {
                throw new InvalidDataException($"skin-partition-count-invalid:{shapeName}");
            }
            totalPartitionVertexCount = checked(totalPartitionVertexCount + vertexCount);
            totalPartitionTriangleCount = checked(totalPartitionTriangleCount + triangleCount);
            totalStripCount = checked(totalStripCount + stripCount);
            if (totalPartitionVertexCount > MaximumSkinWeightCount ||
                totalPartitionTriangleCount > MaximumSkinWeightCount ||
                totalStripCount > MaximumSkinWeightCount)
            {
                throw new InvalidDataException($"skin-partition-array-count-out-of-range:{shapeName}");
            }

            for (var i = 0; i < boneCount; i++)
            {
                if (ReadUInt16(bytes[..blockEnd], ref offset) >= skinBoneCount)
                {
                    throw new InvalidDataException($"skin-partition-bone-index-out-of-range:{shapeName}");
                }
            }

            var hasVertexMap = ReadBoolean(bytes[..blockEnd], ref offset, "skin-partition-vertex-map-flag", shapeName);
            if (!hasVertexMap && vertexCount != shapeVertexCount)
            {
                throw new InvalidDataException($"skin-partition-vertex-map-missing:{shapeName}");
            }

            for (var i = 0; hasVertexMap && i < vertexCount; i++)
            {
                if (ReadUInt16(bytes[..blockEnd], ref offset) >= shapeVertexCount)
                {
                    throw new InvalidDataException($"skin-partition-vertex-map-out-of-range:{shapeName}");
                }
            }

            var hasVertexWeights = ReadBoolean(bytes[..blockEnd], ref offset, "skin-partition-vertex-weights-flag", shapeName);
            var weightCount = checked((int)vertexCount * weightsPerVertex);
            if (hasVertexWeights)
            {
                totalWeightCount = checked(totalWeightCount + weightCount);
                if (totalWeightCount > MaximumSkinWeightCount)
                {
                    throw new InvalidDataException($"skin-partition-weight-count-out-of-range:{shapeName}");
                }
            }

            for (var i = 0; hasVertexWeights && i < weightCount; i++)
            {
                var weight = ReadSingle(bytes[..blockEnd], ref offset);
                if (!float.IsFinite(weight) || weight < 0 || weight > 1)
                {
                    throw new InvalidDataException($"skin-partition-weight-invalid:{shapeName}");
                }
            }

            var stripLengths = new ushort[stripCount];
            for (var i = 0; i < stripLengths.Length; i++)
            {
                stripLengths[i] = ReadUInt16(bytes[..blockEnd], ref offset);
            }

            if (stripCount == 0)
            {
                ReadPartitionTriangles(bytes[..blockEnd], ref offset, triangleCount, vertexCount, shapeName);
            }
            else
            {
                var calculatedTriangleCount = 0;
                foreach (var stripLength in stripLengths)
                {
                    calculatedTriangleCount = checked(calculatedTriangleCount + Math.Max(0, stripLength - 2));
                    for (var i = 0; i < stripLength; i++)
                    {
                        if (ReadUInt16(bytes[..blockEnd], ref offset) >= vertexCount)
                        {
                            throw new InvalidDataException($"skin-partition-strip-index-out-of-range:{shapeName}");
                        }
                    }
                }

                if (calculatedTriangleCount != triangleCount)
                {
                    throw new InvalidDataException($"skin-partition-strip-triangle-count-mismatch:{shapeName}");
                }
            }

            var hasBoneIndices = ReadBoolean(bytes[..blockEnd], ref offset, "skin-partition-bone-indices-flag", shapeName);
            for (var i = 0; hasBoneIndices && i < weightCount; i++)
            {
                if (boneCount == 0 || ReadByte(bytes[..blockEnd], ref offset) >= boneCount)
                {
                    throw new InvalidDataException($"skin-partition-bone-index-out-of-range:{shapeName}");
                }
            }

            _ = ReadByte(bytes[..blockEnd], ref offset);
            _ = ReadBoolean(bytes[..blockEnd], ref offset, "skin-partition-global-vertex-buffer-flag", shapeName);
            _ = ReadUInt64(bytes[..blockEnd], ref offset);
            ReadPartitionTriangles(bytes[..blockEnd], ref offset, triangleCount, shapeVertexCount, shapeName);
        }

        if (offset != blockEnd)
        {
            throw new InvalidDataException($"skin-partition-unmodeled-trailing-data:{shapeName}");
        }

        return partitionCount;
    }

    private static void ReadPartitionTriangles(
        ReadOnlySpan<byte> bytes,
        ref int offset,
        int triangleCount,
        int vertexCount,
        string shapeName)
    {
        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            for (var vertex = 0; vertex < 3; vertex++)
            {
                if (ReadUInt16(bytes, ref offset) >= vertexCount)
                {
                    throw new InvalidDataException($"skin-partition-triangle-index-out-of-range:{shapeName}");
                }
            }
        }
    }

    private static void ReadFiniteSingles(
        ReadOnlySpan<byte> bytes,
        ref int offset,
        int count,
        string fieldName,
        string shapeName)
    {
        for (var i = 0; i < count; i++)
        {
            if (!float.IsFinite(ReadSingle(bytes, ref offset)))
            {
                throw new InvalidDataException($"{fieldName}-nonfinite:{shapeName}");
            }
        }
    }

    private static bool ReadBoolean(ReadOnlySpan<byte> bytes, ref int offset, string fieldName, string shapeName)
    {
        var value = ReadByte(bytes, ref offset);
        if (value > 1)
        {
            throw new InvalidDataException($"{fieldName}-invalid:{shapeName}");
        }

        return value != 0;
    }

    private static int ReadRequiredBlockReference(
        ReadOnlySpan<byte> bytes,
        ref int offset,
        int blockEnd,
        string expectedType,
        string shapeName,
        IReadOnlyList<string> blockTypes,
        IReadOnlyList<ushort> blockTypeIndexes)
    {
        var blockIndex = ReadInt32(bytes[..blockEnd], ref offset);
        if (blockIndex < 0 ||
            blockIndex >= blockTypeIndexes.Count ||
            blockTypes[blockTypeIndexes[blockIndex]] != expectedType)
        {
            throw new InvalidDataException($"skin-reference-invalid:{shapeName}:{expectedType}");
        }

        return blockIndex;
    }

    private static int ReadRequiredNodeReference(
        ReadOnlySpan<byte> bytes,
        ref int offset,
        int blockEnd,
        string fieldName,
        string shapeName,
        IReadOnlyList<string> blockTypes,
        IReadOnlyList<ushort> blockTypeIndexes)
    {
        var blockIndex = ReadInt32(bytes[..blockEnd], ref offset);
        if (blockIndex < 0 ||
            blockIndex >= blockTypeIndexes.Count ||
            !NiNodeTypes.Contains(blockTypes[blockTypeIndexes[blockIndex]]))
        {
            throw new InvalidDataException($"skin-reference-invalid:{shapeName}:{fieldName}");
        }

        return blockIndex;
    }

    private sealed record SkinInstanceData(
        int SkinDataBlockIndex,
        int SkinPartitionBlockIndex,
        int SkeletonRootBlockIndex,
        IReadOnlyList<int> BoneBlockIndices,
        int PartitionCount,
        IReadOnlyList<SkyrimSseNifBodyPartition> BodyPartitions);

    private static MeshVertex[] ReadHalfVector3Array(
        ReadOnlySpan<byte> bytes,
        ref int offset,
        int count,
        string field,
        string shapeName)
    {
        var vectors = new MeshVertex[count];
        for (var i = 0; i < vectors.Length; i++)
        {
            var x = BitConverter.UInt16BitsToHalf(ReadUInt16(bytes, ref offset));
            var y = BitConverter.UInt16BitsToHalf(ReadUInt16(bytes, ref offset));
            var z = BitConverter.UInt16BitsToHalf(ReadUInt16(bytes, ref offset));
            if (!Half.IsFinite(x) || !Half.IsFinite(y) || !Half.IsFinite(z))
            {
                throw new InvalidDataException($"bstrishape-nonfinite-{field}:{shapeName}");
            }

            vectors[i] = new MeshVertex((float)x, (float)y, (float)z);
        }

        return vectors;
    }

    private static bool IsSupportedSkinInstanceType(string typeName) =>
        string.Equals(typeName, "NiSkinInstance", StringComparison.Ordinal) ||
        string.Equals(typeName, "BSDismemberSkinInstance", StringComparison.Ordinal);

    private static bool IsUnsupportedGeometryType(string typeName) =>
        !string.Equals(typeName, "BSTriShape", StringComparison.Ordinal) &&
        !string.Equals(typeName, "BSDynamicTriShape", StringComparison.Ordinal) &&
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

    private static float ReadSingle(ReadOnlySpan<byte> bytes, ref int offset) =>
        BitConverter.Int32BitsToSingle(unchecked((int)ReadUInt32(bytes, ref offset)));

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
    public static string ComputeVertexOrderFingerprint(SkyrimSseNifShape shape)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> value = stackalloc byte[sizeof(uint)];
        hash.AppendData("SlideSmith.OrderedVertexPositions.v1\0"u8);
        BinaryPrimitives.WriteInt32LittleEndian(value, shape.Vertices.Count);
        hash.AppendData(value);
        foreach (var vertex in shape.Vertices)
        {
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.X));
            hash.AppendData(value);
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.Y));
            hash.AppendData(value);
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.Z));
            hash.AppendData(value);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

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

        if (!string.Equals(source.GeometryBlockType, target.GeometryBlockType, StringComparison.Ordinal) ||
            source.Vertices.Count == 0 ||
            source.Vertices.Count != target.Vertices.Count ||
            !HasExactVertexOrder(source.Vertices, target.Vertices) ||
            source.TriangleIndices.Count != target.TriangleIndices.Count ||
            !source.TriangleIndices.SequenceEqual(target.TriangleIndices))
        {
            return false;
        }

        evidence =
            $"exact-ordered-shape-geometry-and-topology-sha256:{ComputeFingerprint(source)}" +
            $":vertex-order-sha256:{ComputeVertexOrderFingerprint(source)}";
        return true;
    }

    private static bool HasExactVertexOrder(
        IReadOnlyList<MeshVertex> source,
        IReadOnlyList<MeshVertex> target)
    {
        for (var index = 0; index < source.Count; index++)
        {
            var sourceVertex = source[index];
            var targetVertex = target[index];
            if (BitConverter.SingleToInt32Bits(sourceVertex.X) != BitConverter.SingleToInt32Bits(targetVertex.X) ||
                BitConverter.SingleToInt32Bits(sourceVertex.Y) != BitConverter.SingleToInt32Bits(targetVertex.Y) ||
                BitConverter.SingleToInt32Bits(sourceVertex.Z) != BitConverter.SingleToInt32Bits(targetVertex.Z))
            {
                return false;
            }
        }

        return true;
    }

    private static string ComputeFingerprint(SkyrimSseNifShape shape)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> value = stackalloc byte[sizeof(uint)];
        hash.AppendData("SlideSmith.ExactShapeCorrespondence.v2\0"u8);
        var shapeName = Encoding.UTF8.GetBytes(shape.Name);
        BinaryPrimitives.WriteInt32LittleEndian(value, shapeName.Length);
        hash.AppendData(value);
        hash.AppendData(shapeName);
        var geometryBlockType = Encoding.UTF8.GetBytes(shape.GeometryBlockType);
        BinaryPrimitives.WriteInt32LittleEndian(value, geometryBlockType.Length);
        hash.AppendData(value);
        hash.AppendData(geometryBlockType);
        BinaryPrimitives.WriteInt32LittleEndian(value, shape.Vertices.Count);
        hash.AppendData(value);
        foreach (var vertex in shape.Vertices)
        {
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.X));
            hash.AppendData(value);
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.Y));
            hash.AppendData(value);
            BinaryPrimitives.WriteInt32LittleEndian(value, BitConverter.SingleToInt32Bits(vertex.Z));
            hash.AppendData(value);
        }

        BinaryPrimitives.WriteInt32LittleEndian(value, shape.TriangleIndices.Count);
        hash.AppendData(value);
        Span<byte> triangleIndex = stackalloc byte[sizeof(ushort)];
        foreach (var index in shape.TriangleIndices)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(triangleIndex, index);
            hash.AppendData(triangleIndex);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
