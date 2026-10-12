using System.Numerics;
using Bodyslide.Core;
using Xunit;

namespace Bodyslide.Core.Tests;

public sealed class ProximityConformTests
{
    private static Vector3[] Grid() =>
        [.. from x in Enumerable.Range(0, 5) from y in Enumerable.Range(0, 5)
            select new Vector3(x * 0.1f, y * 0.1f, 0f)];

    [Fact]
    public void UniformReferenceOffsetMovesNearbyOutfitVertices()
    {
        var source = Grid();
        var target = source.Select(v => v + new Vector3(0f, 0f, 0.5f)).ToArray();
        var outfit = new[] { new Vector3(0.21f, 0.19f, 0.02f), new Vector3(0.05f, 0.31f, 0.01f) };

        var result = ProximityConform.TryConform(outfit, source, target, 0.3f, 4);

        Assert.NotNull(result);
        Assert.Equal(2, result!.ConformedCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(0.52f, result.Vertices[0].Z, 3);
        Assert.Equal(outfit[0].X, result.Vertices[0].X, 3);
    }

    [Fact]
    public void VertexOutsideRadiusIsLeftUnchangedAndCounted()
    {
        var source = Grid();
        var target = source.Select(v => v + Vector3.UnitZ).ToArray();
        var far = new Vector3(5f, 5f, 5f);

        var result = ProximityConform.TryConform([far], source, target, 0.2f, 4);

        Assert.NotNull(result);
        Assert.Equal(far, result!.Vertices[0]);
        Assert.Equal(1, result.UnmatchedCount);
    }

    [Fact]
    public void MismatchedReferenceCountsFailClosed()
    {
        var source = Grid();
        Assert.Null(ProximityConform.TryConform([Vector3.Zero], source, source.Take(3).ToArray(), 0.2f, 4));
    }

    [Fact]
    public void NonFiniteInputAndBadParametersFailClosed()
    {
        var source = Grid();
        var bad = source.ToArray();
        bad[0] = new Vector3(float.NaN, 0f, 0f);
        Assert.Null(ProximityConform.TryConform([Vector3.Zero], source, bad, 0.2f, 4));
        Assert.Null(ProximityConform.TryConform([new Vector3(float.PositiveInfinity, 0, 0)], source, source, 0.2f, 4));
        Assert.Null(ProximityConform.TryConform([Vector3.Zero], source, source, 0f, 4));
        Assert.Null(ProximityConform.TryConform([Vector3.Zero], source, source, 0.2f, 0));
    }
}
