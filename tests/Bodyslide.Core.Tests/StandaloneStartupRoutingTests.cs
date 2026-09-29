namespace Bodyslide.Core.Tests;

public sealed class StandaloneStartupRoutingTests
{
    [Fact]
    public void EvaluateDesktopLaunchDecision_NoArgs_AttemptsDesktopHandoff()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.False(decision.LauncherSignalDetected);
        Assert.False(decision.ModManagerLaunchDetected);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

    [Theory]
    [InlineData("--list-bodies")]
    [InlineData("--list-presets")]
    [InlineData("--target")]
    public void EvaluateDesktopLaunchDecision_ExplicitCliWithoutLauncherSignal_SkipsDesktopHandoff(string firstArg)
    {
        var args = firstArg.Equals("--target", StringComparison.OrdinalIgnoreCase)
            ? new[] { "--target", "CBBE" }
            : new[] { firstArg };

        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            args,
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.False(decision.LauncherSignalDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Theory]
    [InlineData("--mo2-output", @"C:\MO2\mods\SomePack")]
    [InlineData("--vortex-staging", @"C:\Users\Test\AppData\Roaming\Vortex\skyrimse\mods")]
    [InlineData("--modorganizer-path", @"D:\Mod Organizer 2\mods\SomePack")]
    public void EvaluateDesktopLaunchDecision_ModManagerArgs_AttemptsDesktopHandoff(
        string option,
        string path)
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [option, path],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_EnvOnly_ModManagerLaunchDetected()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--input", @"C:\mods\pack"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: name => name.Equals("MO2_INSTANCE", StringComparison.OrdinalIgnoreCase));

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_ExplicitCliWithLauncherArg_PrefersDesktopHandoff()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--list-bodies", "--mo2-output", @"C:\MO2\mods\SomePack"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_Mo2InputPathOnly_TreatedAsLauncherSignal()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--input", @"D:\Mod Organizer 2\mods\My Armor\meshes\armor_1.nif"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_Mo2PathInlineQuotedWithCliFlag_StillPrefersDesktopHandoff()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--self-check", "--mo2-path:\"D:\\Mod Organizer 2\\mods\\Pack With Spaces\""],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_SlashPrefixedMo2ColonArgument_IsDetected()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["/modorganizer-path:D:\\MO2\\mods\\SomePack"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }
}
