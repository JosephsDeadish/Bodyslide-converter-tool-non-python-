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
