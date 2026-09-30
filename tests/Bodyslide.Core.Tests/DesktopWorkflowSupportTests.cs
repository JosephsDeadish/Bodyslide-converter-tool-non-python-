namespace Bodyslide.Core.Tests;

public sealed class DesktopWorkflowSupportTests
{
    [Fact]
    public void TryResolveResultOutputDirectory_WalksUpFromNestedFomodArtifact()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var fomodDirectory = Path.Combine(outputDirectory, "fomod");
        Directory.CreateDirectory(fomodDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");
        var moduleConfigPath = Path.Combine(fomodDirectory, "ModuleConfig.xml");
        File.WriteAllText(moduleConfigPath, "<config />");

        try
        {
            var resolved = DesktopWorkflowSupport.TryResolveResultOutputDirectory(moduleConfigPath);

            Assert.Equal(outputDirectory, resolved);
            Assert.True(DesktopWorkflowSupport.LooksLikeSlideSmithOutputDirectory(resolved));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void TryResolveResultOutputDirectory_DoesNotTreatPlainFomodModAsSlideSmithOutput()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modDirectory = Path.Combine(workingDirectory, "mod");
        var fomodDirectory = Path.Combine(modDirectory, "fomod");
        Directory.CreateDirectory(fomodDirectory);
        var moduleConfigPath = Path.Combine(fomodDirectory, "ModuleConfig.xml");
        File.WriteAllText(moduleConfigPath, "<config />");

        try
        {
            var resolved = DesktopWorkflowSupport.TryResolveResultOutputDirectory(moduleConfigPath);
            Assert.Null(resolved);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesMo2StyleArgumentsAndNestedResultPaths()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--mo2-output",
                    Path.Combine(outputDirectory, "preview-workbench.html"),
                    "--mo2-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesGenericOutputArgumentForResultLoading()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--output",
                    outputDirectory
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesModOrganizerLauncherAlias()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--output",
                    outputDirectory,
                    "--modorganizer-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("--mo2")]
    [InlineData("--modorganizer")]
    [InlineData("--from-modorganizer")]
    public void ParseLaunchOptions_RecognizesBareModOrganizerLauncherFlags(string launcherFlag)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    launcherFlag,
                    "--output",
                    outputDirectory
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_TreatsMo2OutputArgumentAsMo2Launch()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--mo2-output",
                    outputDirectory
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesSingleDashMo2Arguments()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "-mo2-output",
                    outputDirectory,
                    "-mo2-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesSlashPrefixedMo2Arguments()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/mo2-output={outputDirectory}",
                    "/mo2-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesSlashColonMo2OutputArguments()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/mo2-output:{outputDirectory}",
                    "/mo2-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesVortexArgumentsAndResultPath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--vortex-output",
                    outputDirectory,
                    "--vortex-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesColonSeparatedVortexArgumentValue()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/vortex-output:{outputDirectory}",
                    "/vortex-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesColonSeparatedMo2Arguments()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/mo2-output:{outputDirectory}",
                    "/modorganizer-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesMo2OutputWithQuotedInlineValue()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mods root");
        var outputDirectory = Path.Combine(workingDirectory, "output with spaces");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"--mo2-output=\"{outputDirectory}\"",
                    "--modorganizer-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesModOrganizerPathWithColonSeparatedValue()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mods root");
        var outputDirectory = Path.Combine(workingDirectory, "output with spaces");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/modorganizer-path:\"{outputDirectory}\"",
                    "/mo2-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesModOrganizerOutputAlias()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mods root");
        var outputDirectory = Path.Combine(workingDirectory, "output with spaces");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"--modorganizer-output=\"{outputDirectory}\"",
                    "--modorganizer-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_UsesModOrganizerResultAliasAsStartupInputWhenNoResultMarkersExist()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mods root");
        var inputDirectory = Path.Combine(workingDirectory, "input fallback");
        Directory.CreateDirectory(inputDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"--modorganizer-result=\"{inputDirectory}\"",
                    "--mo2-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesMixedQuotedInlineAndColonModManagerPayloads()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mods root");
        var outputDirectory = Path.Combine(workingDirectory, "staging output");
        var inputDirectory = Path.Combine(workingDirectory, "incoming payload");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(inputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/input:\"{inputDirectory}\"",
                    $"--vortex-output=\"{outputDirectory}\"",
                    "--from-vortex"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_UsesLatestMixedOutputAliasAsInputFallbackWhenNoResultMarkersExist()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mods root");
        var inputDirectory = Path.Combine(workingDirectory, "mo2 profile");
        var nonResultDirectory = Path.Combine(workingDirectory, "staging output");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(nonResultDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/mo2-path:\"{inputDirectory}\"",
                    $"--output:{nonResultDirectory}",
                    "--mo2-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(nonResultDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_ReadsStartupDiagnosticsArgumentAndPrefersMo2ResultPath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var diagnosticsPath = Path.Combine(workingDirectory, "startup.log");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");
        File.WriteAllLines(diagnosticsPath,
        [
            "startup: begin",
            "desktop-launch: shell start failed for candidate-a",
            "desktop-launch: started fallback candidate-b",
            "desktop-launch: handoff complete"
        ]);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--startup-diagnostics",
                    diagnosticsPath,
                    "--mo2-output",
                    outputDirectory,
                    "--mo2-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
            Assert.Contains("handoff complete", options.StartupDiagnostics, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("candidate-b", options.StartupDiagnostics, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_ReadsInlineLauncherHandoffDiagnosticsPayload()
    {
        var diagnostics = "desktop-launch: shell failed | desktop-launch: fallback started";
        var encodedDiagnostics = Uri.EscapeDataString(diagnostics);
        var options = DesktopWorkflowSupport.ParseLaunchOptions(
        [
            $"--launcher-handoff-diagnostics={encodedDiagnostics}"
        ]);

        Assert.Null(options.StartupOutputDirectory);
        Assert.Null(options.StartupInputPath);
        Assert.Equal(diagnostics, options.StartupDiagnostics);
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesVortexOutputWithQuotedInlineValue()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mods root");
        var outputDirectory = Path.Combine(workingDirectory, "output with spaces");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"--vortex-output=\"{outputDirectory}\"",
                    "--vortex-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesGenericOutputArgumentWithVortexLauncherFlag()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--output",
                    outputDirectory,
                    "--vortex"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesNxmHandlerAndFromVortexLaunchFlags()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--output",
                    outputDirectory,
                    "--nxmhandler",
                    "--from-vortex"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesRealWorldVortexStagingPathAndOutputPair()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Vortex Mods");
        var outputDirectory = Path.Combine(workingDirectory, "SlideSmith Output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "/vortex-staging",
                    workingDirectory,
                    "--output",
                    outputDirectory,
                    "--vortex-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_HandlesLauncherProfileValueBeforePositionalInputPath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "mods", "armor-pack");
        Directory.CreateDirectory(inputDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                    [
                        "--profile",
                        "Default",
                        inputDirectory,
                        "--from-mo2"
                    ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesInputArgumentAsStartupInputPath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "mods");
        Directory.CreateDirectory(inputDirectory);
        var inputFile = Path.Combine(inputDirectory, "armor.nif");
        File.WriteAllText(inputFile, "mesh");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--input",
                    inputFile,
                    "--mo2-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputFile, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesColonSeparatedInputArgument()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "mods");
        Directory.CreateDirectory(inputDirectory);
        var inputFile = Path.Combine(inputDirectory, "armor.nif");
        File.WriteAllText(inputFile, "mesh");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/input:{inputFile}",
                    "/modorganizer-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputFile, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_UsesExistingPathAsStartupInputWhenItIsNotASlideSmithResult()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "mo2-mod");
        Directory.CreateDirectory(inputDirectory);
        var inputFile = Path.Combine(inputDirectory, "armor.nif");
        File.WriteAllText(inputFile, "mesh");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--mo2-launcher",
                    inputFile
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputFile, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_UsesResultFallbackArgumentAsInputWhenPathIsNotResultOutput()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modDirectory = Path.Combine(workingDirectory, "deployed-mod");
        Directory.CreateDirectory(modDirectory);
        var inputFile = Path.Combine(modDirectory, "armor.nif");
        File.WriteAllText(inputFile, "mesh");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--vortex-staging",
                    modDirectory
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(modDirectory, options.StartupInputPath);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_UsesInputFallbackForModOrganizerPathArgumentsWhenNoResultMarkersExist()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "mo2-mod");
        Directory.CreateDirectory(inputDirectory);
        var inputFile = Path.Combine(inputDirectory, "armor.nif");
        File.WriteAllText(inputFile, "mesh");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--modorganizer-path",
                    inputFile,
                    "--modorganizer-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputFile, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesVortexPathArgumentsAsLauncherInputFallback()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "vortex-staging");
        Directory.CreateDirectory(inputDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/vortex-path={inputDirectory}",
                    "/vortex-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_DoesNotTreatNamedResultArgumentAsStartupInputWhenResultMarkersAreMissing()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var notResultDirectory = Path.Combine(workingDirectory, "vortex-staging");
        Directory.CreateDirectory(notResultDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--vortex-output",
                    notResultDirectory,
                    "--vortex-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_UsesMo2ModArgumentAsStartupInputWhenResultMarkersAreMissing()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modDirectory = Path.Combine(workingDirectory, "mo2-mod");
        Directory.CreateDirectory(modDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--mo2-mod",
                    modDirectory,
                    "--mo2-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(modDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_UsesVortexModArgumentAsStartupInputWhenResultMarkersAreMissing()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modDirectory = Path.Combine(workingDirectory, "vortex-mod");
        Directory.CreateDirectory(modDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--vortex-mod",
                    modDirectory,
                    "--vortex-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(modDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_DoesNotTreatPositionalInputInsideOutputTreeAsResult()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var nestedInputDirectory = Path.Combine(outputDirectory, "meshes", "custom");
        Directory.CreateDirectory(nestedInputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");
        var inputPath = Path.Combine(nestedInputDirectory, "armor.nif");
        File.WriteAllText(inputPath, "mesh");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions([inputPath]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputPath, options.StartupInputPath);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_DoesNotTreatPositionalInputDirectoryInsideOutputTreeAsResult()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var inputDirectory = Path.Combine(outputDirectory, "meshes", "custom-pack");
        Directory.CreateDirectory(inputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions([inputDirectory]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputDirectory, options.StartupInputPath);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_ResolvesNamedResultArgumentFromNestedOutputDirectory()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var fomodDirectory = Path.Combine(outputDirectory, "fomod");
        Directory.CreateDirectory(fomodDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");
        File.WriteAllText(Path.Combine(fomodDirectory, "ModuleConfig.xml"), "<config/>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--mo2-output",
                    fomodDirectory,
                    "--mo2-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_PrefersNamedResultArgumentOverEarlierUnknownOptionValue()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--game",
                    "Skyrim Special Edition",
                    "--mo2-output",
                    outputDirectory,
                    "--mo2-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("--mo2-output")]
    [InlineData("--modorganizer-path")]
    [InlineData("--vortex-output")]
    [InlineData("--vortex-deploy-path")]
    [InlineData("--vortex-deployment-path")]
    [InlineData("--vortex-staging-path")]
    [InlineData("--vortex-mod-path")]
    [InlineData("--mods-path")]
    [InlineData("--output-dir")]
    [InlineData("--output-path")]
    public void ParseLaunchOptions_ResolvesCommonModManagerResultArgumentAliases(string resultArgumentName)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    resultArgumentName,
                    outputDirectory,
                    "--vortex-launcher"
                ]);

            Assert.Equal(outputDirectory, options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesVortexDeploymentPathAliasWithColonSeparatedQuotedValue()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "vortex stage root");
        var stagingDirectory = Path.Combine(workingDirectory, "staging path");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"/vortex-deployment-path:\"{stagingDirectory}\"",
                    "--from-vortex"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(stagingDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesVortexStagingPathAliasAsStartupInputFallback()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var stagingDirectory = Path.Combine(workingDirectory, "vortex-staging");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--staging-path",
                    stagingDirectory,
                    "--from-vortex"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(stagingDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_RecognizesMo2PathAliasWithInlineQuotedValue()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "mods root");
        var modDirectory = Path.Combine(workingDirectory, "mod path");
        Directory.CreateDirectory(modDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    $"--mo2-path=\"{modDirectory}\"",
                    "--from-mo2"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(modDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(workingDirectory)!, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_TreatsMo2OutputAliasAsStartupInputWhenNoResultMarkersExist()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modDirectory = Path.Combine(workingDirectory, "mod-output");
        Directory.CreateDirectory(modDirectory);

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--mo2-output",
                    modDirectory,
                    "--from-mo2"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(modDirectory, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_FallsBackToExplicitInputWhenResultArgumentPathIsMissing()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        Directory.CreateDirectory(inputDirectory);
        var inputFile = Path.Combine(inputDirectory, "armor.nif");
        File.WriteAllText(inputFile, "mesh");
        var missingOutputPath = Path.Combine(workingDirectory, "missing-output");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--output",
                    missingOutputPath,
                    "--input",
                    inputFile,
                    "--mo2-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputFile, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_FallsBackToPositionalPathWhenResultArgumentPathIsMissing()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        Directory.CreateDirectory(inputDirectory);
        var inputFile = Path.Combine(inputDirectory, "armor.nif");
        File.WriteAllText(inputFile, "mesh");
        var missingOutputPath = Path.Combine(workingDirectory, "missing-output");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--output",
                    missingOutputPath,
                    inputFile,
                    "--mo2-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputFile, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_DoesNotTreatUnknownPathOptionValueAsPositionalInput()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cacheFilePath = Path.Combine(workingDirectory, "cache.json");
        Directory.CreateDirectory(workingDirectory);
        File.WriteAllText(cacheFilePath, "{}");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--cache-path",
                    cacheFilePath,
                    "--mo2-launcher"
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Null(options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void ParseLaunchOptions_KeepsPositionalInputAfterLauncherFlag()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var inputDirectory = Path.Combine(workingDirectory, "input");
        Directory.CreateDirectory(inputDirectory);
        var inputFile = Path.Combine(inputDirectory, "armor.nif");
        File.WriteAllText(inputFile, "mesh");

        try
        {
            var options = DesktopWorkflowSupport.ParseLaunchOptions(
                [
                    "--mo2-launcher",
                    inputFile
                ]);

            Assert.Null(options.StartupOutputDirectory);
            Assert.Equal(inputFile, options.StartupInputPath);
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public void BuildStartupHandoffPlan_PrioritizesStartupResultOverInputToKeepStartupOrderSafe()
    {
        var options = new DesktopLaunchOptions(
            StartupOutputDirectory: @"C:\out",
            StartupInputPath: @"C:\input\armor.nif",
            FromModOrganizerLauncher: true);

        var plan = DesktopWorkflowSupport.BuildStartupHandoffPlan(
            options,
            shouldAutoInspectInputPath: static _ => true,
            fileExists: static _ => true,
            directoryExists: static _ => true);

        Assert.True(plan.ShouldLoadStartupResult);
        Assert.False(plan.ShouldApplyStartupInput);
        Assert.False(plan.ShouldQueueStartupAutoInspect);
        Assert.Null(plan.StartupInputPath);
    }

    [Fact]
    public void BuildStartupHandoffPlan_QueuesAutoInspectWhenStartupInputExistsAndIsInspectable()
    {
        var inputPath = @"C:\input\armor.nif";
        var options = new DesktopLaunchOptions(
            StartupOutputDirectory: null,
            StartupInputPath: inputPath,
            FromModOrganizerLauncher: true);

        var plan = DesktopWorkflowSupport.BuildStartupHandoffPlan(
            options,
            shouldAutoInspectInputPath: static path => path.EndsWith(".nif", StringComparison.OrdinalIgnoreCase),
            fileExists: static _ => true,
            directoryExists: static _ => false);

        Assert.False(plan.ShouldLoadStartupResult);
        Assert.True(plan.ShouldApplyStartupInput);
        Assert.True(plan.ShouldQueueStartupAutoInspect);
        Assert.Equal(inputPath, plan.StartupInputPath);
    }

    [Fact]
    public void BuildStartupHandoffPlan_SkipsAutoInspectForNonInspectableStartupInput()
    {
        var inputPath = @"C:\mods\pack.zip";
        var options = new DesktopLaunchOptions(
            StartupOutputDirectory: null,
            StartupInputPath: inputPath,
            FromModOrganizerLauncher: true);

        var plan = DesktopWorkflowSupport.BuildStartupHandoffPlan(
            options,
            shouldAutoInspectInputPath: static _ => false,
            fileExists: static _ => true,
            directoryExists: static _ => false);

        Assert.False(plan.ShouldLoadStartupResult);
        Assert.True(plan.ShouldApplyStartupInput);
        Assert.False(plan.ShouldQueueStartupAutoInspect);
        Assert.Equal(inputPath, plan.StartupInputPath);
    }

    [Fact]
    public void BuildStartupHandoffPlan_IgnoresMissingStartupInputPath()
    {
        var options = new DesktopLaunchOptions(
            StartupOutputDirectory: null,
            StartupInputPath: @"C:\missing\armor.nif",
            FromModOrganizerLauncher: true);

        var plan = DesktopWorkflowSupport.BuildStartupHandoffPlan(
            options,
            shouldAutoInspectInputPath: static _ => true,
            fileExists: static _ => false,
            directoryExists: static _ => false);

        Assert.False(plan.ShouldLoadStartupResult);
        Assert.False(plan.ShouldApplyStartupInput);
        Assert.False(plan.ShouldQueueStartupAutoInspect);
        Assert.Null(plan.StartupInputPath);
    }

    [Fact]
    public void FindCommonDirectory_DoesNotCollapseCaseDistinctDirectoriesOnCaseSensitivePlatforms()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var upper = Path.Combine(workingDirectory, "Armor");
        var lower = Path.Combine(workingDirectory, "armor");
        Directory.CreateDirectory(upper);
        Directory.CreateDirectory(lower);

        try
        {
            var common = DesktopWorkflowSupport.FindCommonDirectory([upper, lower]);

            Assert.Equal(Path.GetFullPath(workingDirectory), common);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}
