using System.Diagnostics;

namespace Bodyslide.Core;

internal static class DesktopProcessLifetime
{
    internal static int WaitForCompletion(Process desktopProcess)
    {
        desktopProcess.WaitForExit();
        return desktopProcess.ExitCode;
    }
}
