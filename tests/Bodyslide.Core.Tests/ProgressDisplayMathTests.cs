namespace Bodyslide.Core.Tests;

public sealed class ProgressDisplayMathTests
{
    [Fact]
    public void MonotonicProgressUnits_DoesNotMoveBackward()
    {
        Assert.Equal(7.5d, ProgressDisplayMath.MonotonicProgressUnits(7.5d, 6.25d));
        Assert.Equal(7.5d, ProgressDisplayMath.MonotonicProgressUnits(6.25d, 7.5d));
    }

    [Fact]
    public void MonotonicStageIndex_DoesNotMoveBackward()
    {
        Assert.Equal(12, ProgressDisplayMath.MonotonicStageIndex(12, 8));
        Assert.Equal(12, ProgressDisplayMath.MonotonicStageIndex(8, 12));
    }
}
