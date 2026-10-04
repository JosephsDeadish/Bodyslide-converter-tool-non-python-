namespace Bodyslide.Core.Tests;

public sealed class DesktopLaunchPathResolverTests
{
    [Theory]
    [InlineData("SlideSmith.exe")]
    [InlineData("SlideSmith.dll")]
    public void HandoffPreservesCallerWorkingDirectoryForRelativeResultPaths(string desktopFile)
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-handoff", Guid.NewGuid().ToString("N"));
        var managerDirectory = Path.Combine(root, "MO2 profile with spaces");
        var desktopDirectory = Path.Combine(root, "bundle", "desktop");
        Directory.CreateDirectory(managerDirectory);
        Directory.CreateDirectory(desktopDirectory);
        try
        {
            var directory = DesktopLaunchPathResolver.ResolveWorkingDirectory(
                managerDirectory, Path.Combine(desktopDirectory, desktopFile));

            Assert.Equal(managerDirectory, directory);
            Assert.Equal(Path.Combine(managerDirectory, "results"),
                Path.GetFullPath("results", directory));
            Assert.Equal(desktopDirectory, DesktopLaunchPathResolver.ResolveWorkingDirectory(
                Path.Combine(root, "missing"), Path.Combine(desktopDirectory, desktopFile)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetLikelyDesktopCandidateDirectories_IncludesNestedDesktopFolderAndSiblingDesktopFolder()
    {
        var executableDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "CalienteTools", "SlideSmith", "cli");

        var directories = DesktopLaunchPathResolver.GetLikelyDesktopCandidateDirectories(executableDirectory);

        Assert.Contains(Path.GetDirectoryName(executableDirectory)!, directories);
        Assert.Contains(Path.Combine(executableDirectory, "desktop"), directories);
        Assert.Contains(Path.Combine(Path.GetDirectoryName(executableDirectory)!, "desktop"), directories);
    }

    [Fact]
    public void GetLikelyDesktopCandidateDirectories_IncludesSiblingDesktopProjectOutput()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "repo", "src");
        var standaloneOutput = Path.Combine(repoRoot, "Bodyslide.Standalone", "bin", "Debug", "net10.0");
        var expectedDesktopOutput = Path.Combine(repoRoot, "Bodyslide.Desktop", "bin", "Debug", "net10.0-windows");

        var directories = DesktopLaunchPathResolver.GetLikelyDesktopCandidateDirectories(standaloneOutput);

        Assert.Contains(expectedDesktopOutput, directories);
    }

    [Fact]
    public void GetLikelyDesktopCandidateDirectories_ReturnsExecutableDirectoryFirst()
    {
        var executableDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "CalienteTools", "SlideSmith");

        var directories = DesktopLaunchPathResolver.GetLikelyDesktopCandidateDirectories(executableDirectory);

        Assert.Equal(Path.GetFullPath(executableDirectory), directories[0]);
    }
}
