namespace Bodyslide.Core;

internal sealed record SkyrimSseNifStretchReport(
    string ShapeName,
    int EdgeCount,
    double MaxEdgeRatio,
    double MinEdgeRatio,
    bool Comparable,
    bool StretchRisk);

/// <summary>
/// Geometry-only deformation check. It flags edge stretch/compression after a position transform;
/// it does not prove that preserved skin weights suit the target body.
/// </summary>
internal static class SkyrimSseNifFitDiagnostics
{
    public const double DefaultMaxEdgeRatio = 1.5d;
    public const double DefaultMinEdgeRatio = 0.5d;

    public static SkyrimSseNifStretchReport AnalyzeStretch(
        SkyrimSseNifShape before,
        SkyrimSseNifShape after,
        double maxEdgeRatio = DefaultMaxEdgeRatio,
        double minEdgeRatio = DefaultMinEdgeRatio)
    {
        if (before.Vertices.Count != after.Vertices.Count ||
            !before.TriangleIndices.SequenceEqual(after.TriangleIndices))
        {
            return new SkyrimSseNifStretchReport(before.Name, 0, 0d, 0d, false, true);
        }

        double max = 0d, min = double.MaxValue;
        var edges = 0;
        var tri = before.TriangleIndices;
        for (var t = 0; t + 2 < tri.Count; t += 3)
        {
            for (var e = 0; e < 3; e++)
            {
                int a = tri[t + e], b = tri[t + ((e + 1) % 3)];
                if (a >= before.Vertices.Count || b >= before.Vertices.Count)
                {
                    return new SkyrimSseNifStretchReport(before.Name, 0, 0d, 0d, false, true);
                }

                var original = Distance(before.Vertices[a], before.Vertices[b]);
                if (original <= 1e-6d)
                {
                    continue;
                }

                var ratio = Distance(after.Vertices[a], after.Vertices[b]) / original;
                max = Math.Max(max, ratio);
                min = Math.Min(min, ratio);
                edges++;
            }
        }

        if (edges == 0)
        {
            return new SkyrimSseNifStretchReport(before.Name, 0, 0d, 0d, true, false);
        }

        return new SkyrimSseNifStretchReport(before.Name, edges, max, min, true, max > maxEdgeRatio || min < minEdgeRatio);
    }

    private static double Distance(MeshVertex a, MeshVertex b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
