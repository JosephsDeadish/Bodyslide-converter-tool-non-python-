using System.Numerics;

namespace Bodyslide.Core;

/// <summary>
/// Reference-body proximity conform, modelled on how BodySlide's conform works: each outfit
/// vertex follows the offset between a source and target reference body, taken from the
/// nearest reference vertices inside a radius (inverse-distance weighted). Fails closed when
/// the reference pair does not correspond vertex-for-vertex or contains non-finite data.
/// </summary>
internal static class ProximityConform
{
    internal sealed record Result(Vector3[] Vertices, int ConformedCount, int UnmatchedCount);

    internal static Result? TryConform(
        IReadOnlyList<Vector3> outfitVertices,
        IReadOnlyList<Vector3> referenceSource,
        IReadOnlyList<Vector3> referenceTarget,
        float radius,
        int maxNeighbours)
    {
        if (referenceSource.Count == 0 ||
            referenceSource.Count != referenceTarget.Count ||
            !(radius > 0f) || float.IsInfinity(radius) ||
            maxNeighbours < 1 ||
            !AllFinite(outfitVertices) || !AllFinite(referenceSource) || !AllFinite(referenceTarget))
        {
            return null;
        }

        var cell = radius;
        var grid = new Dictionary<(int, int, int), List<int>>();
        for (var i = 0; i < referenceSource.Count; i++)
        {
            var key = CellOf(referenceSource[i], cell);
            if (!grid.TryGetValue(key, out var bucket))
            {
                grid[key] = bucket = [];
            }
            bucket.Add(i);
        }

        var result = new Vector3[outfitVertices.Count];
        var conformed = 0;
        var candidates = new List<(float DistSq, int Index)>();
        var radiusSq = radius * radius;
        for (var v = 0; v < outfitVertices.Count; v++)
        {
            var p = outfitVertices[v];
            candidates.Clear();
            var (cx, cy, cz) = CellOf(p, cell);
            for (var x = cx - 1; x <= cx + 1; x++)
            for (var y = cy - 1; y <= cy + 1; y++)
            for (var z = cz - 1; z <= cz + 1; z++)
            {
                if (!grid.TryGetValue((x, y, z), out var bucket)) continue;
                foreach (var index in bucket)
                {
                    var d = Vector3.DistanceSquared(p, referenceSource[index]);
                    if (d <= radiusSq) candidates.Add((d, index));
                }
            }

            if (candidates.Count == 0)
            {
                result[v] = p;
                continue;
            }

            candidates.Sort(static (a, b) => a.DistSq.CompareTo(b.DistSq));
            var take = Math.Min(maxNeighbours, candidates.Count);
            var offset = Vector3.Zero;
            var weightSum = 0f;
            for (var n = 0; n < take; n++)
            {
                var (distSq, index) = candidates[n];
                var weight = 1f / (MathF.Sqrt(distSq) + 1e-4f);
                offset += weight * (referenceTarget[index] - referenceSource[index]);
                weightSum += weight;
            }

            result[v] = p + offset / weightSum;
            conformed++;
        }

        return new Result(result, conformed, outfitVertices.Count - conformed);
    }

    private static (int, int, int) CellOf(Vector3 p, float cell) =>
        ((int)MathF.Floor(p.X / cell), (int)MathF.Floor(p.Y / cell), (int)MathF.Floor(p.Z / cell));

    private static bool AllFinite(IReadOnlyList<Vector3> points)
    {
        foreach (var p in points)
        {
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) return false;
        }
        return true;
    }
}
