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
    public void ParseLaunchOptions_IgnoresStartupDiagnosticsArgumentAndPrefersMo2ResultPath()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var outputDirectory = Path.Combine(workingDirectory, "output");
        var diagnosticsPath = Path.Combine(workingDirectory, "startup.log");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "preview-workbench.html"), "<html></html>");

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
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
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
    [InlineData("--mods-path")]
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
