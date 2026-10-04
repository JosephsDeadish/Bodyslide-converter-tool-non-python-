namespace Bodyslide.Core.Tests;

public sealed class DesktopLaunchPathResolverTests
{
    [Theory]
    [InlineData("includedFrameworks", false)]
    [InlineData("includedFrameworks", true)]
    [InlineData("frameworks", false)]
    public void InstalledNestedCliDiscoversParentDesktopByRuntimeConfiguration(string property, bool useDll)
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-manager", Guid.NewGuid().ToString("N"));
        var installedDirectory = Path.Combine(root, "CalienteTools", "SlideSmith");
        var cliDirectory = Path.Combine(installedDirectory, "cli");
        Directory.CreateDirectory(cliDirectory);
        try
        {
            File.WriteAllText(Path.Combine(installedDirectory, "SlideSmith.runtimeconfig.json"),
                $$$"""{"runtimeOptions":{"{{{property}}}":[{"name":"Microsoft.NETCore.App"},{"name":"Microsoft.WindowsDesktop.App"}]}}""");
            var desktopPath = Path.Combine(installedDirectory, useDll ? "SlideSmith.dll" : "SlideSmith.exe");
            File.WriteAllText(desktopPath, "desktop");
            var directories = DesktopLaunchPathResolver.GetLikelyDesktopCandidateDirectories(cliDirectory);
            var candidates = DesktopLaunchPathResolver.GetDesktopCandidates(directories, useDll);
            Assert.Contains(desktopPath, candidates);
            Assert.Single(candidates, candidate => candidate == desktopPath);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("""{"runtimeOptions":{"framework":{"name":"Microsoft.WindowsDesktop.App"}}}""", true)]
    [InlineData("""{"runtimeOptions":{"includedFrameworks":[{"name":"Microsoft.NETCore.App"}]}}""", false)]
    [InlineData("""{"runtimeOptions":{"includedFrameworks":"Microsoft.WindowsDesktop.App"}}""", false)]
    [InlineData("""{"runtimeOptions":{"framework":{"name":123}}}""", false)]
    [InlineData("""{"runtimeOptions":null}""", false)]
    [InlineData("[]", false)]
    [InlineData("not json", false)]
    public void SharedExecutableNameInOtherDirectoriesRequiresDesktopRuntime(string configuration, bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-candidates", Guid.NewGuid().ToString("N"));
        var cliDirectory = Path.Combine(root, "cli");
        var otherDirectory = Path.Combine(root, "other");
        Directory.CreateDirectory(cliDirectory);
        Directory.CreateDirectory(otherDirectory);
        try
        {
            File.WriteAllText(Path.Combine(otherDirectory, "SlideSmith.runtimeconfig.json"), configuration);
            foreach (var useDll in new[] { false, true })
            {
                var candidates = DesktopLaunchPathResolver.GetDesktopCandidates([cliDirectory, otherDirectory], useDll);
                var sharedName = useDll ? "SlideSmith.dll" : "SlideSmith.exe";
                Assert.Equal(expected, candidates.Contains(Path.Combine(otherDirectory, sharedName)));
                Assert.Contains(Path.Combine(cliDirectory, sharedName), candidates);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void DesktopCandidatesAreExplicitAndDoNotEnumerateUnrelatedExecutables()
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-candidates", Guid.NewGuid().ToString("N"));
        var desktopDirectory = Path.Combine(root, "desktop");
        Directory.CreateDirectory(desktopDirectory);
        try
        {
            File.WriteAllText(Path.Combine(desktopDirectory, "unrelated-desktop-helper.exe"), "helper");
            var candidates = DesktopLaunchPathResolver.GetDesktopCandidates([Path.Combine(root, "cli"), desktopDirectory], false);
            Assert.Contains(Path.Combine(desktopDirectory, "SlideSmith.exe"), candidates);
            Assert.DoesNotContain(Path.Combine(desktopDirectory, "unrelated-desktop-helper.exe"), candidates);
            Assert.Empty(DesktopLaunchPathResolver.GetDesktopCandidates([], false));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("installed", "cli/SlideSmith-CLI.exe")]
    [InlineData("desktop", "../cli/SlideSmith-CLI.exe")]
    [InlineData("installed", "SlideSmith-CLI.exe")]
    [InlineData("installed", "cli/SlideSmith.exe")]
    [InlineData("desktop", "../cli/SlideSmith.exe")]
    public void FindCliExecutableDoesNotMistakeDesktopForBundledCli(string desktopFolder, string cliRelativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-cli-guidance", Guid.NewGuid().ToString("N"));
        var desktopDirectory = Path.Combine(root, desktopFolder);
        var cliPath = Path.GetFullPath(Path.Combine(desktopDirectory, cliRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        Directory.CreateDirectory(desktopDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(cliPath)!);
        try
        {
            File.WriteAllText(Path.Combine(desktopDirectory, "SlideSmith.exe"), "desktop");
            File.WriteAllText(cliPath, "cli");
            Assert.Equal(cliPath, DesktopLaunchPathResolver.FindCliExecutable(desktopDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindCliExecutableReturnsNullWhenOnlyDesktopExists()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var desktopDirectory = Path.Combine(root, "desktop");
        Directory.CreateDirectory(desktopDirectory);
        try
        {
            File.WriteAllText(Path.Combine(desktopDirectory, "SlideSmith.exe"), "desktop");
            Assert.Null(DesktopLaunchPathResolver.FindCliExecutable(desktopDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
