using System.Numerics;

namespace Bodyslide.Core;

/// <summary>
/// Copies per-vertex bone weights from a reference body onto outfit vertices by proximity
/// (inverse-distance weighted over the nearest reference vertices), then caps influences per
/// vertex and normalizes to sum 1, as BodySlide's "Copy Bone Weights" does. Locked bones keep
/// their original weight. Fails closed on mismatched or non-finite input.
/// </summary>
internal static class ProximityWeightTransfer
{
    internal const int MaxInfluencesPerVertex = 4;

    internal sealed record Result(IReadOnlyList<IReadOnlyDictionary<string, float>> Weights, int UnmatchedCount);

    internal static Result? TryTransfer(
        IReadOnlyList<Vector3> outfitVertices,
        IReadOnlyList<IReadOnlyDictionary<string, float>> outfitOriginalWeights,
        IReadOnlyList<Vector3> referenceVertices,
        IReadOnlyList<IReadOnlyDictionary<string, float>> referenceWeights,
        float radius,
        int maxNeighbours,
        IReadOnlySet<string>? lockedBones = null)
    {
        if (referenceVertices.Count == 0 ||
            referenceVertices.Count != referenceWeights.Count ||
            outfitVertices.Count != outfitOriginalWeights.Count ||
            !(radius > 0f) || float.IsInfinity(radius) || maxNeighbours < 1 ||
            !Finite(outfitVertices) || !Finite(referenceVertices) ||
            !WeightsValid(referenceWeights) || !WeightsValid(outfitOriginalWeights))
        {
            return null;
        }

        var grid = new Dictionary<(int, int, int), List<int>>();
        for (var i = 0; i < referenceVertices.Count; i++)
        {
            var key = Cell(referenceVertices[i], radius);
            if (!grid.TryGetValue(key, out var bucket)) grid[key] = bucket = [];
            bucket.Add(i);
        }

        var locked = lockedBones ?? new HashSet<string>();
        var output = new IReadOnlyDictionary<string, float>[outfitVertices.Count];
        var unmatched = 0;
        var radiusSq = radius * radius;
        var near = new List<(float DistSq, int Index)>();
        for (var v = 0; v < outfitVertices.Count; v++)
        {
            var p = outfitVertices[v];
            near.Clear();
            var (cx, cy, cz) = Cell(p, radius);
            for (var x = cx - 1; x <= cx + 1; x++)
            for (var y = cy - 1; y <= cy + 1; y++)
            for (var z = cz - 1; z <= cz + 1; z++)
            {
                if (!grid.TryGetValue((x, y, z), out var bucket)) continue;
                foreach (var index in bucket)
                {
                    var d = Vector3.DistanceSquared(p, referenceVertices[index]);
                    if (d <= radiusSq) near.Add((d, index));
                }
            }

            if (near.Count == 0)
            {
                output[v] = outfitOriginalWeights[v];
                unmatched++;
                continue;
            }

            near.Sort(static (a, b) => a.DistSq.CompareTo(b.DistSq));
            var blended = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var distanceWeightSum = 0f;
            foreach (var (distSq, index) in near.Take(maxNeighbours))
            {
                var w = 1f / (MathF.Sqrt(distSq) + 1e-4f);
                distanceWeightSum += w;
                foreach (var (bone, weight) in referenceWeights[index])
                {
                    blended[bone] = blended.GetValueOrDefault(bone) + w * weight;
                }
            }

            foreach (var bone in blended.Keys.ToList())
            {
                blended[bone] /= distanceWeightSum;
            }

            foreach (var bone in locked)
            {
                if (outfitOriginalWeights[v].TryGetValue(bone, out var original)) blended[bone] = original;
            }

            output[v] = CapAndNormalize(blended, outfitOriginalWeights[v]);
        }

        return new Result(output, unmatched);
    }

    private static IReadOnlyDictionary<string, float> CapAndNormalize(
        Dictionary<string, float> blended, IReadOnlyDictionary<string, float> fallback)
    {
        var top = blended.Where(static kv => kv.Value > 0f)
            .OrderByDescending(static kv => kv.Value).ThenBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxInfluencesPerVertex).ToList();
        var sum = top.Sum(static kv => kv.Value);
        return sum > 0f
            ? top.ToDictionary(kv => kv.Key, kv => kv.Value / sum, StringComparer.OrdinalIgnoreCase)
            : fallback;
    }

    private static (int, int, int) Cell(Vector3 p, float cell) =>
        ((int)MathF.Floor(p.X / cell), (int)MathF.Floor(p.Y / cell), (int)MathF.Floor(p.Z / cell));

    private static bool Finite(IReadOnlyList<Vector3> points) =>
        points.All(static p => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z));

    private static bool WeightsValid(IReadOnlyList<IReadOnlyDictionary<string, float>> weights) =>
        weights.All(static w => w.Values.All(static x => float.IsFinite(x) && x >= 0f));
}
