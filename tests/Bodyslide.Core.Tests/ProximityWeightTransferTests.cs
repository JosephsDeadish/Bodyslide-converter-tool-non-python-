using System.Numerics;
using Bodyslide.Core;
using Xunit;

namespace Bodyslide.Core.Tests;

public sealed class ProximityWeightTransferTests
{
    private static IReadOnlyDictionary<string, float> W(params (string, float)[] items) =>
        items.ToDictionary(i => i.Item1, i => i.Item2);

    [Fact]
    public void CopiesNearestReferenceWeightsAndNormalizes()
    {
        Vector3[] reference = [new(0, 0, 0), new(1, 0, 0)];
        var refWeights = new[] { W(("A", 1f)), W(("B", 1f)) };

        var result = ProximityWeightTransfer.TryTransfer(
            [new Vector3(0.05f, 0, 0)], [W(("Old", 1f))], reference, refWeights, 0.3f, 2);

        Assert.NotNull(result);
        var w = result!.Weights[0];
        Assert.Equal(1f, w["A"], 3);
        Assert.False(w.ContainsKey("Old"));
        Assert.Equal(0, result.UnmatchedCount);
    }

    [Fact]
    public void CapsInfluencesAtFourAndSumsToOne()
    {
        Vector3[] reference = [new(0, 0, 0)];
        var refWeights = new[] { W(("a", 0.3f), ("b", 0.25f), ("c", 0.2f), ("d", 0.15f), ("e", 0.1f)) };

        var result = ProximityWeightTransfer.TryTransfer([Vector3.Zero], [W(("x", 1f))], reference, refWeights, 0.5f, 1);

        var w = result!.Weights[0];
        Assert.Equal(4, w.Count);
        Assert.Equal(1f, w.Values.Sum(), 4);
        Assert.False(w.ContainsKey("e"));
    }

    [Fact]
    public void LockedBoneKeepsOriginalWeightBeforeNormalizing()
    {
        Vector3[] reference = [new(0, 0, 0)];
        var refWeights = new[] { W(("A", 1f)) };
        var result = ProximityWeightTransfer.TryTransfer(
            [Vector3.Zero], [W(("Lock", 0.5f))], reference, refWeights, 0.5f, 1, new HashSet<string> { "Lock" });

        var w = result!.Weights[0];
        Assert.Equal(0.5f / 1.5f, w["Lock"], 3);
    }

    [Fact]
    public void FarVertexKeepsOriginalWeightsAndIsCounted()
    {
        var original = W(("Old", 1f));
        var result = ProximityWeightTransfer.TryTransfer(
            [new Vector3(9, 9, 9)], [original], [Vector3.Zero], [W(("A", 1f))], 0.2f, 2);

        Assert.Same(original, result!.Weights[0]);
        Assert.Equal(1, result.UnmatchedCount);
    }

    [Fact]
    public void BadInputFailsClosed()
    {
        var one = new[] { W(("A", 1f)) };
        Assert.Null(ProximityWeightTransfer.TryTransfer([Vector3.Zero], one, [Vector3.Zero], [], 0.2f, 1));
        Assert.Null(ProximityWeightTransfer.TryTransfer([Vector3.Zero], one, [Vector3.Zero], [W(("A", float.NaN))], 0.2f, 1));
        Assert.Null(ProximityWeightTransfer.TryTransfer([Vector3.Zero], one, [Vector3.Zero], [W(("A", -1f))], 0.2f, 1));
        Assert.Null(ProximityWeightTransfer.TryTransfer([Vector3.Zero], [], [Vector3.Zero], one, 0.2f, 1));
    }
}
