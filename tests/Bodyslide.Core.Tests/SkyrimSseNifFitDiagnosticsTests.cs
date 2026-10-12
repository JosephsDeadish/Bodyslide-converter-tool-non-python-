using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class SkyrimSseNifFitDiagnosticsTests
{
    private static SkyrimSseNifShape Shape(params (float X, float Y, float Z)[] v) =>
        new("Body", v.Select(p => new MeshVertex(p.X, p.Y, p.Z)).ToArray(), [0, 1, 2], 16);

    [Fact]
    public void UniformScaleIsNotFlaggedAndStretchIs()
    {
        var before = Shape((0, 0, 0), (1, 0, 0), (0, 1, 0));
        var scaled = Shape((0, 0, 0), (1.1f, 0, 0), (0, 1.1f, 0));
        var stretched = Shape((0, 0, 0), (3, 0, 0), (0, 1, 0));

        Assert.False(SkyrimSseNifFitDiagnostics.AnalyzeStretch(before, scaled).StretchRisk);
        Assert.True(SkyrimSseNifFitDiagnostics.AnalyzeStretch(before, stretched).StretchRisk);
    }

    [Fact]
    public void MismatchedTopologyIsNotComparableAndFailsClosed()
    {
        var report = SkyrimSseNifFitDiagnostics.AnalyzeStretch(
            Shape((0, 0, 0), (1, 0, 0), (0, 1, 0)),
            Shape((0, 0, 0), (1, 0, 0)));

        Assert.False(report.Comparable);
        Assert.True(report.StretchRisk);
    }

    [Fact]
    public void DescribeStretchRisksIsEmptyForIdenticalAndUnsupportedBytes()
    {
        var nif = SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Body");

        Assert.Empty(SkyrimSseNifFitDiagnostics.DescribeStretchRisks(nif, nif));
        Assert.Empty(SkyrimSseNifFitDiagnostics.DescribeStretchRisks([1, 2, 3], nif));
    }

    [Fact]
    public void TopologyOnlyMatchAcceptsMovedVerticesButRejectsTopologyChanges()
    {
        var source = Shape((0, 0, 0), (1, 0, 0), (0, 1, 0));
        var moved = Shape((0, 0, 0), (2, 0, 0), (0, 2, 0));
        var different = new SkyrimSseNifShape("Body", moved.Vertices, [0, 2, 1], 16);
        var fewer = Shape((0, 0, 0), (1, 0, 0));

        Assert.True(SkyrimSseNifShapeCorrespondence.TryVerifyTopologyOnlyMatch(source, moved, out var evidence));
        Assert.StartsWith("topology-only-match:", evidence);
        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyExactOrderedMatch(source, moved, out _));
        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyTopologyOnlyMatch(source, different, out _));
        Assert.False(SkyrimSseNifShapeCorrespondence.TryVerifyTopologyOnlyMatch(source, fewer, out _));
    }
}
