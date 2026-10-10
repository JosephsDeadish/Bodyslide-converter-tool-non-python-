using System.Diagnostics;

namespace Bodyslide.Core.Tests;

public sealed class DesktopProcessLifetimeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public async Task HandoffStaysAliveUntilChildExitsAndPreservesItsExitCode(int exitCode)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            UseShellExecute = false,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add($"set /p handoff= & exit /b {exitCode}");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add($"read handoff; exit {exitCode}");
        }

        using var child = Process.Start(startInfo)!;
        var completion = Task.Run(() => DesktopProcessLifetime.WaitForCompletion(child));
        try
        {
            Assert.NotSame(completion, await Task.WhenAny(completion, Task.Delay(750)));
            Assert.False(child.HasExited);

            await child.StandardInput.WriteLineAsync("finish");
            await child.StandardInput.FlushAsync();

            Assert.Equal(exitCode, await completion.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            child.StandardInput.Close();
            if (!child.WaitForExit(10000))
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit();
            }
            await completion;
        }
    }
}
