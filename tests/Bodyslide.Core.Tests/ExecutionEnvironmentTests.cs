namespace Bodyslide.Core.Tests;

public sealed class ExecutionEnvironmentTests
{
    [Fact]
    public void GetExecutionRoot_PrefersProcessDirectory()
    {
        var processPath = Path.Combine(Path.GetTempPath(), "slidesmith-process", "SlideSmith.exe");
        var appBaseDirectory = Path.Combine(Path.GetTempPath(), "slidesmith-appbase");
        var currentDirectory = Path.Combine(Path.GetTempPath(), "slidesmith-current");

        var root = ExecutionEnvironment.GetExecutionRoot(processPath, appBaseDirectory, currentDirectory);

        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(processPath)), root);
    }

    [Fact]
    public void GetExecutionRoot_FallsBackToAppContextBaseDirectory()
    {
        var appBaseDirectory = Path.Combine(Path.GetTempPath(), "slidesmith-appbase");
        var currentDirectory = Path.Combine(Path.GetTempPath(), "slidesmith-current");

        var root = ExecutionEnvironment.GetExecutionRoot(processPath: null, appBaseDirectory, currentDirectory);

        Assert.Equal(Path.GetFullPath(appBaseDirectory), root);
    }

    [Fact]
    public void GetDefaultOutputRoot_UsesResolvedExecutionRoot()
    {
        var processPath = Path.Combine(Path.GetTempPath(), "slidesmith-process", "SlideSmith.exe");

        var outputRoot = ExecutionEnvironment.GetDefaultOutputRoot(processPath, appContextBaseDirectory: null, currentDirectory: null);

        Assert.Equal(
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(processPath))!, "output"),
            outputRoot);
    }
}
