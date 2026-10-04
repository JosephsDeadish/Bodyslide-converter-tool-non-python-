namespace Bodyslide.Core.Tests;

public sealed class StandaloneStartupRoutingTests
{
    [Theory]
    [InlineData("--input", @"D:\MO2\mods\Armor\armor.nif")]
    [InlineData("--input=", @"D:\MO2\mods\Armor\armor.nif")]
    [InlineData("--input:", "/home/test/vortex/mods/armor.nif")]
    [InlineData("--input:", "/home/test/vortex/mods/armor=variant.nif")]
    [InlineData("--input:", @"D:\MO2\mods\Armor\armor=variant.nif")]
    [InlineData("--INPUT=", "/home/test/armor=variant.nif")]
    public void NamedConversionArgumentsMatchRoutingUnderLauncherSignals(string option, string input)
    {
        string[] args = option.EndsWith('=') || option.EndsWith(':')
            ? ["--from-mo2", option + input, "--target=CBBE", "--output:/tmp/result", "--build-sliders=false"]
            : ["--from-mo2", option, input, "--target", "CBBE", "--output", "/tmp/result", "--build-sliders", "false"];
        var values = StandaloneStartupRouting.ParseNamedArguments(args);
        Assert.Equal(input, values["input"]);
        Assert.Equal("CBBE", values["target"]);
        Assert.Equal("/tmp/result", values["output"]);
        Assert.Equal("false", values["build-sliders"]);

        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(args,
            @"D:\MO2\SlideSmith.exe", @"D:\Vortex\mods", _ => true);
        Assert.True(decision.ExplicitCliLaunchDetected);
        Assert.False(decision.ShouldAttemptDesktopHandoff);
    }

    [Theory]
    [InlineData("--self-check")]
    [InlineData("--list-bodies")]
    [InlineData("--export-cache")]
    [InlineData("--help")]
    [InlineData("--compact-diagnostics")]
    public void InlineCommandsUseSameNamesAsStartupRouting(string command)
    {
        var args = new[] { "--from-vortex", command + "=true" };
        var values = StandaloneStartupRouting.ParseNamedArguments(args);

        Assert.Equal("true", values[command[2..]]);
        Assert.False(StandaloneStartupRouting.EvaluateDesktopLaunchDecision(args,
            null, null, _ => true).ShouldAttemptDesktopHandoff);
    }

    [Fact]
    public void InlineValuesDoNotConsumeFollowingOptionOrPositionalToken()
    {
        var values = StandaloneStartupRouting.ParseNamedArguments(
            ["--input=armor.nif", "unrelated", "--target:CBBE", "--output-zip", "--build-sliders=false"]);

        Assert.Equal("armor.nif", values["input"]);
        Assert.Equal("CBBE", values["target"]);
        Assert.Equal("true", values["output-zip"]);
        Assert.Equal("false", values["build-sliders"]);
    }

    [Fact]
    public void NamedParserPreservesSeparateValuesFlagsAndLastValueWins()
    {
        var values = StandaloneStartupRouting.ParseNamedArguments(
            ["armor.nif", "CBBE", "--input", "/help", "--target=UNP", "--TARGET", "CBBE", "--output-zip"]);

        Assert.Equal("/help", values["input"]);
        Assert.Equal("CBBE", values["target"]);
        Assert.Equal("true", values["output-zip"]);
        Assert.False(values.ContainsKey("help"));
    }

    [Theory]
    [InlineData("--input=")]
    [InlineData("--input:")]
    public void ExplicitEmptyInlineValueDoesNotAcquireNextCommand(string inputOption)
    {
        var values = StandaloneStartupRouting.ParseNamedArguments([inputOption, "--self-check"]);

        Assert.Equal(string.Empty, values["input"]);
        Assert.Equal("true", values["self-check"]);
    }

    [Theory]
    [InlineData("--input:/home/test/armor=variant.nif", "input", "/home/test/armor=variant.nif")]
    [InlineData("--input:C:\\Armor\\armor=variant.nif", "input", "C:\\Armor\\armor=variant.nif")]
    [InlineData("--input=C:\\Armor\\armor:variant.nif", "input", "C:\\Armor\\armor:variant.nif")]
    [InlineData("--mo2-output:C:\\MO2\\profiles\\name=variant", "mo2-output", "C:\\MO2\\profiles\\name=variant")]
    [InlineData("/vortex-path:/home/test/armor=variant", "vortex-path", "/home/test/armor=variant")]
    public void FirstInlineDelimiterPreservesEveryCharacterOfOptionValue(string arg, string expectedName, string expectedValue)
    {
        Assert.True(StandaloneStartupRouting.TryReadOptionToken(arg, out var name, out var value));
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("--mo2-output:/home/test/armor=variant")]
    [InlineData("--vortex-path:C:\\Vortex\\mods\\name=variant")]
    public void ManagerInlinePathsWithEqualsSignsRemainLauncherOnly(string arg)
    {
        Assert.True(StandaloneStartupRouting.HasLauncherPathOptionArgument([arg]));
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision([arg], null, null, _ => false);
        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

    [Theory]
    [InlineData(@"D:\MO2\mods\Armor\armor.nif", "CBBE", true)]
    [InlineData(@"D:\Vortex\mods\Armor\armor.nif", "CBBE", true)]
    [InlineData("/home/test/vortex/mods/armor.nif", "CBBE", true)]
    [InlineData("armor.nif", "CBBE", true)]
    [InlineData("--input", "CBBE", false)]
    [InlineData("-input", "CBBE", false)]
    [InlineData("/input", "CBBE", false)]
    [InlineData("-", "CBBE", false)]
    [InlineData("--", "CBBE", false)]
    [InlineData("armor.nif", "--target", false)]
    [InlineData("armor.nif", "-target", false)]
    [InlineData("armor.nif", "/target", false)]
    [InlineData("armor.nif", "--", false)]
    [InlineData("moshortcut://launch/SlideSmith", "CBBE", false)]
    public void HasStandalonePositionalConversionUsage_MatchesRequestParsing(
        string input,
        string target,
        bool expected)
    {
        Assert.Equal(expected, StandaloneStartupRouting.HasStandalonePositionalConversionUsage([input, target]));
    }

    public static IEnumerable<object[]> ExplicitCliWithLauncherSignals()
    {
        string[][] cliArguments =
        [
            ["--help"], ["--list-bodies"], ["--list-presets"], ["--list-profiles"],
            ["--list-physics"], ["--self-check"], ["--conversion-guide"], ["--export-cache"],
            ["--body-reference"], ["--pause"],
            ["--input", @"D:\Mod Organizer 2\mods\Armor\armor.nif", "--target", "CBBE"],
            ["--output", @"D:\Vortex\mods\Converted Armor"],
            ["--input", @"D:\Vortex\mods\Armor", "--output", @"D:\MO2\mods\Converted"],
            ["--targets=CBBE,UNP", "--from-mo2"],
            ["--preset=Default", "--from-vortex"],
            ["--compact-diagnostics", "true"],
            [@"D:\MO2\mods\Armor\armor.nif", "CBBE"],
            ["/home/test/vortex/mods/armor.nif", "CBBE"]
        ];

        foreach (var cliArgs in cliArguments)
        {
            foreach (var strict in new[] { false, true })
            {
                yield return [cliArgs.Concat(["--mo2-output", @"D:\MO2\mods\Converted"]).ToArray(),
                    "VORTEX_SESSION", strict];
                yield return [cliArgs.Concat(["--vortex-launcher"]).ToArray(), "MO2_INSTANCE", strict];
                yield return [cliArgs, "USVFS_PARAMETERS", strict];
                yield return [cliArgs, string.Empty, strict];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ExplicitCliWithLauncherSignals))]
    public void EvaluateDesktopLaunchDecision_ExplicitCliAlwaysWinsMixedLauncherSignals(
        string[] args,
        string environmentVariable,
        bool strict)
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            args,
            executablePath: @"D:\MO2\mods\SlideSmith\SlideSmith.exe",
            workingDirectory: @"D:\Vortex\mods\SlideSmith",
            hasEnvironmentVariable: name => name == environmentVariable,
            strictLauncherMode: strict);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
        Assert.Equal(strict, decision.StrictLauncherModeEnabled);
        Assert.Equal(strict
            ? "strict-launcher-mode: explicit cli takes precedence"
            : "explicit cli takes precedence", decision.RoutingReason);
        if (environmentVariable.Length > 0)
        {
            Assert.True(decision.LauncherSignalDetected);
        }
    }

    [Theory]
    [InlineData("--mo2-launcher", "MO2_INSTANCE", false)]
    [InlineData("--vortex-launcher", "VORTEX_SESSION", true)]
    [InlineData("--from-mo2", "VORTEX_PROFILE_ID", true)]
    [InlineData("--from-vortex", "USVFS_PARAMETERS", false)]
    public void EvaluateDesktopLaunchDecision_LauncherOnlyWithMetadata_StillUsesDesktop(
        string launcherSwitch,
        string environmentVariable,
        bool strict)
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [launcherSwitch, "--profile", "Default", "--game", "SkyrimSE"],
            executablePath: @"D:\MO2\SlideSmith.exe",
            workingDirectory: @"D:\Vortex\mods",
            hasEnvironmentVariable: name => name == environmentVariable,
            strictLauncherMode: strict);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

    [Theory]
    [InlineData("--mo2-output", "/help")]
    [InlineData("--vortex-path", "/self-check")]
    [InlineData("--load-result", "/target")]
    [InlineData("--startup-diagnostics", "/list-bodies")]
    [InlineData("--profile", "/input")]
    public void EvaluateDesktopLaunchDecision_PathOptionValues_AreNotStandaloneCommands(
        string option,
        string value)
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--from-mo2", option, value],
            executablePath: null,
            workingDirectory: null,
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

    [Theory]
    [InlineData("--mo2-output=--help", false)]
    [InlineData("--vortex-path:/self-check", true)]
    public void EvaluateDesktopLaunchDecision_InlinePathValues_AreNotStandaloneCommands(string option, bool strict)
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--from-vortex", option],
            executablePath: null,
            workingDirectory: null,
            hasEnvironmentVariable: _ => false,
            strictLauncherMode: strict);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_CommandAfterMissingPathValue_StillUsesCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--mo2-output", "--self-check"],
            executablePath: null,
            workingDirectory: null,
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Theory]
    [InlineData(null, "MO2_INSTANCE", false)]
    [InlineData(null, "VORTEX_SESSION", true)]
    [InlineData(@"D:\MO2\mods\Armor", "VORTEX_SESSION", true)]
    [InlineData(@"D:\Vortex\mods\Armor", "MO2_INSTANCE", false)]
    [InlineData("moshortcut://launch/SlideSmith", "MO2_INSTANCE", true)]
    public void EvaluateDesktopLaunchDecision_LauncherWithoutCliIntent_StillUsesDesktop(
        string? argument,
        string environmentVariable,
        bool strict)
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            argument is null ? [] : [argument],
            executablePath: @"D:\MO2\SlideSmith.exe",
            workingDirectory: @"D:\Vortex\mods",
            hasEnvironmentVariable: name => name == environmentVariable,
            strictLauncherMode: strict);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

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

    [Fact]
    public void EvaluateDesktopLaunchDecision_InputAndOutputCliFlags_StayInCliMode()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [
                "--input",
                @"C:\Mods\Some Armor\nif\armor.nif",
                "--output",
                @"C:\Mods\Some Armor\converted"
            ],
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
    [InlineData("--modorganizer", null)]
    [InlineData("--mo2", null)]
    [InlineData("--from-modorganizer", null)]
    public void EvaluateDesktopLaunchDecision_ModManagerArgs_AttemptsDesktopHandoff(
        string option,
        string? path)
    {
        var args = path is null ? new[] { option } : new[] { option, path };
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            args,
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Theory]
    [InlineData("--load-result", @"C:\Mod Organizer 2\mods\Some Armor Pack")]
    [InlineData(@"--mo2-result:""C:\Mod Organizer 2\mods\Pack With Spaces""", null)]
    [InlineData(@"/modorganizer-result:D:\Mod Organizer 2\mods\Legacy Pack", null)]
    [InlineData(@"/modorganizer-mod:D:\Mod Organizer 2\mods\Legacy Pack", null)]
    [InlineData("--from-mo2", null)]
    public void EvaluateDesktopLaunchDecision_RealWorldMo2LaunchPatterns_AttemptDesktopHandoff(
        string firstArg,
        string? secondArg)
    {
        var args = secondArg is null
            ? new[] { firstArg }
            : new[] { firstArg, secondArg };

        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            args,
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_EnvWithInput_UsesCliAndRetainsModManagerDiagnostics()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--input", @"C:\mods\pack"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: name => name.Equals("MO2_INSTANCE", StringComparison.OrdinalIgnoreCase));

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_ExplicitCliWithLauncherArg_PrefersCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--list-bodies", "--mo2-output", @"C:\MO2\mods\SomePack"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_Mo2InputPathOnly_PrefersCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--input", @"D:\Mod Organizer 2\mods\My Armor\meshes\armor_1.nif"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_Mo2PathInlineQuotedWithCliFlag_PrefersCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--self-check", "--mo2-path:\"D:\\Mod Organizer 2\\mods\\Pack With Spaces\""],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_Mo2DirectoryWithoutTrailingSeparator_CommandPrefersCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--self-check"],
            executablePath: @"D:\Mod Organizer 2\instances\Portable\mods\SlideSmith\SlideSmith.exe",
            workingDirectory: @"D:\Mod Organizer 2\instances\Portable\mods\SlideSmith\MO2",
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.False(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_MoShortcutUri_AttemptsDesktopHandoff()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["moshortcut://launch/SlideSmith"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_MoShortcutUriWithPath_IsNotPositionalConversion()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["moshortcut://launch/SlideSmith", @"D:\MO2\mods\Armor"],
            executablePath: null,
            workingDirectory: null,
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.False(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_ExplicitCliInMo2ManagedLocation_PrefersCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [
                "--self-check",
                "--profile",
                "Default"
            ],
            executablePath: @"D:\Mod Organizer 2\instances\Portable\mods\SlideSmith\SlideSmith.exe",
            workingDirectory: @"D:\Mod Organizer 2\instances\Portable\mods\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.False(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Theory]
    [InlineData("-o:output")]
    [InlineData("/o:output")]
    [InlineData("--o:output")]
    public void TryReadOptionToken_ParsesSingleCharacterColonSeparatedValues(string arg)
    {
        var parsed = StandaloneStartupRouting.TryReadOptionToken(arg, out var option, out var inlineValue);

        Assert.True(parsed);
        Assert.Equal("o", option);
        Assert.Equal("output", inlineValue);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_ModOrganizerOutputAlias_AttemptsDesktopHandoff()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [
                "--modorganizer-output",
                @"D:\Mod Organizer 2\mods\Some Armor Pack"
            ],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_FromModManagerTag_AttemptsDesktopHandoff()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--from-modmanager"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
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

    [Fact]
    public void EvaluateDesktopLaunchDecision_SlashPrefixedMo2OutputArgument_IsDetected()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["/mo2-output:D:\\MO2\\mods\\SomePack"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_RealWorldMo2ProfileGameAndInputSignature_PrefersCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [
                "/profile=Default",
                "/game=SkyrimSE",
                "--input",
                @"D:\Mod Organizer 2\mods\Some Armor Pack\meshes\armor\sample_1.nif"
            ],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_Mo2ManagedPositionalInput_PrefersCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [
                @"D:\Mod Organizer 2\mods\Some Armor Pack\meshes\armor\sample_1.nif",
                "CBBE"
            ],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
        Assert.True(decision.ExplicitCliLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_RealWorldMo2PortableLauncherSignature_PrefersDesktop()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            [
                "--instance",
                "Portable",
                "--mo2-output",
                @"C:\Games\MO2\mods\Converted Armor",
                "--startup-diagnostics",
                @"C:\Temp\slidesmith-startup.log"
            ],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.LauncherSignalDetected);
        Assert.True(decision.ModManagerLaunchDetected);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_StrictLauncherModeWithoutLauncherSignal_UsesCli()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--list-bodies"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false,
            strictLauncherMode: true);

        Assert.False(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.StrictLauncherModeEnabled);
        Assert.Equal("strict-launcher-mode: explicit cli takes precedence", decision.RoutingReason);
    }

    [Fact]
    public void EvaluateDesktopLaunchDecision_StrictLauncherModeWithLauncherSignal_UsesDesktop()
    {
        var decision = StandaloneStartupRouting.EvaluateDesktopLaunchDecision(
            ["--mo2-output", @"D:\Mod Organizer 2\mods\Some Armor Pack"],
            executablePath: @"C:\Tools\SlideSmith\SlideSmith.exe",
            workingDirectory: @"C:\Tools\SlideSmith",
            hasEnvironmentVariable: _ => false,
            strictLauncherMode: true);

        Assert.True(decision.ShouldAttemptDesktopHandoff);
        Assert.True(decision.StrictLauncherModeEnabled);
        Assert.Equal("strict-launcher-mode: launcher signal detected", decision.RoutingReason);
    }
}
