namespace Bodyslide.Core;

public static class ProgressDisplayMath
{
    public static double MonotonicProgressUnits(double current, double previous) =>
        current < previous ? previous : current;

    public static int MonotonicStageIndex(int current, int previous) =>
        current < previous ? previous : current;
}
