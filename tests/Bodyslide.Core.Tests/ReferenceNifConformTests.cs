using Bodyslide.Core;
using Xunit;

namespace Bodyslide.Core.Tests;

public sealed class ReferenceNifConformTests
{
    private static readonly (float X, float Y, float Z)[] ReferenceVerts =
        [(0f, 0f, 0f), (0.05f, 0f, 0f), (0f, 0.05f, 0f)];

    private static (float X, float Y, float Z)[] Lifted(float dz) =>
        ReferenceVerts.Select(v => (v.X, v.Y, v.Z + dz)).ToArray();

    [Fact]
    public void OutfitVerticesFollowReferenceOffsetAndStayReadable()
    {
        var outfit = SkyrimSseNifShapeReaderTests.CreateNifFromVertices((0.01f, 0.01f, 0.02f), (0.04f, 0.0f, 0.02f), (0f, 0.04f, 0.02f));
        var source = SkyrimSseNifShapeReaderTests.CreateNifFromVertices(ReferenceVerts);
        var target = SkyrimSseNifShapeReaderTests.CreateNifFromVertices(Lifted(0.1f));

        var outcome = ReferenceNifConform.TryConform(outfit, source, target, radius: 0.2f, maxNeighbours: 3);

        Assert.NotNull(outcome);
        Assert.Equal(3, outcome!.ConformedVertices);
        var read = SkyrimSseNifShapeReader.Read(outcome.Bytes);
        Assert.True(read.Supported, read.Diagnostic);
        Assert.All(read.Shapes[0].Vertices, v => Assert.InRange(v.Z, 0.11f, 0.13f));
    }

    [Fact]
    public void MismatchedReferencePairFailsClosed()
    {
        var outfit = SkyrimSseNifShapeReaderTests.CreateNifFromVertices(ReferenceVerts);
        var source = SkyrimSseNifShapeReaderTests.CreateNifFromVertices(ReferenceVerts);
        var target = SkyrimSseNifShapeReaderTests.CreateNifFromVertices(ReferenceVerts.Take(2).ToArray());

        Assert.Null(ReferenceNifConform.TryConform(outfit, source, target));
    }

    [Fact]
    public void UnreadableInputFailsClosed()
    {
        var good = SkyrimSseNifShapeReaderTests.CreateNifFromVertices(ReferenceVerts);
        Assert.Null(ReferenceNifConform.TryConform([1, 2, 3], good, good));
    }
}
