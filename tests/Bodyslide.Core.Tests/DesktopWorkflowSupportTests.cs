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
            Assert.True(options.FromModOrganizerLauncher);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}
