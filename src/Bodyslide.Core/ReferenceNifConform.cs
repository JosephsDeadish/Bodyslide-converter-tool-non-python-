using System.Numerics;

namespace Bodyslide.Core;

/// <summary>
/// Conforms the vertex positions of an outfit NIF to a target body using a source/target
/// reference body pair (see <see cref="ProximityConform"/>). Only plain full-precision
/// BSTriShape / BSSubIndexTriShape geometry is rewritten; anything else, or any reference pair
/// whose shapes do not correspond vertex-for-vertex, returns null so callers keep the existing
/// behaviour instead of writing a guessed result.
/// </summary>
internal static class ReferenceNifConform
{
    internal sealed record Outcome(byte[] Bytes, int ConformedVertices, int UnmatchedVertices);

    internal static Outcome? TryConform(
        byte[] outfitBytes,
        byte[] referenceSourceBytes,
        byte[] referenceTargetBytes,
        float radius = 0.06f,
        int maxNeighbours = 8)
    {
        var outfit = SkyrimSseNifShapeReader.Read(outfitBytes);
        var source = SkyrimSseNifShapeReader.Read(referenceSourceBytes);
        var target = SkyrimSseNifShapeReader.Read(referenceTargetBytes);
        if (!outfit.Supported || !source.Supported || !target.Supported ||
            outfit.Shapes.Count == 0 || source.Shapes.Count == 0 ||
            source.Shapes.Count != target.Shapes.Count)
        {
            return null;
        }

        if (outfit.Shapes.Any(static s => !IsPlainTriShape(s)))
        {
            return null;
        }

        for (var i = 0; i < source.Shapes.Count; i++)
        {
            if (!SkyrimSseNifShapeCorrespondence.TryVerifyTopologyOnlyMatch(source.Shapes[i], target.Shapes[i], out _))
            {
                return null;
            }
        }

        var referenceSource = source.Shapes.SelectMany(static s => s.Vertices).Select(ToVector).ToArray();
        var referenceTarget = target.Shapes.SelectMany(static s => s.Vertices).Select(ToVector).ToArray();

        var output = outfitBytes.ToArray();
        var conformed = 0;
        var unmatched = 0;
        foreach (var shape in outfit.Shapes)
        {
            var result = ProximityConform.TryConform(
                shape.Vertices.Select(ToVector).ToArray(), referenceSource, referenceTarget, radius, maxNeighbours);
            if (result is null)
            {
                return null;
            }

            var requiredEnd = (long)shape.VertexDataOffset + (long)shape.Vertices.Count * shape.VertexStride;
            if (shape.VertexDataOffset < 0 || shape.VertexStride < 12 || requiredEnd > output.Length)
            {
                return null;
            }

            for (var v = 0; v < result.Vertices.Length; v++)
            {
                var offset = shape.VertexDataOffset + v * shape.VertexStride;
                BitConverter.TryWriteBytes(output.AsSpan(offset, 4), result.Vertices[v].X);
                BitConverter.TryWriteBytes(output.AsSpan(offset + 4, 4), result.Vertices[v].Y);
                BitConverter.TryWriteBytes(output.AsSpan(offset + 8, 4), result.Vertices[v].Z);
            }

            conformed += result.ConformedCount;
            unmatched += result.UnmatchedCount;
        }

        return new Outcome(output, conformed, unmatched);
    }

    private static bool IsPlainTriShape(SkyrimSseNifShape shape) =>
        shape.GeometryBlockType is "BSTriShape" or "BSSubIndexTriShape" && shape.Vertices.Count > 0;

    private static Vector3 ToVector(MeshVertex v) => new(v.X, v.Y, v.Z);
}
