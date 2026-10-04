using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

[Collection("NonParallel")]
public sealed class CompactDiagnosticsTests
{
    [Fact]
    public void DefaultRemainsVerboseAndWithCopiesPreserveCompactMode()
    {
        var request = new ConversionRequest("armor.nif", "CBBE");
        Assert.False(request.CompactDiagnostics);
        Assert.True((request with { CompactDiagnostics = true } with
        {
            InputPath = "batch-item.nif",
            TargetBody = "3BA"
        }).CompactDiagnostics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompactExportPreservesPackageEvidenceAndInstallArtifacts(bool zip)
    {
        var root = Path.Combine(Environment.CurrentDirectory, $"compact-diagnostics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var nif = Path.Combine(root, "armor.nif");
            await File.WriteAllTextAsync(nif, "synthetic source mesh");
            var armor = new ImportedArmor(root, [nif], [], [], []);
            var mesh = new ConvertedMesh("plate", "direct-copy", 1,
                new Dictionary<string, double> { ["chest"] = 1 });
            var project = await new BodySlideOspProjectService().GenerateAsync(armor, mesh, "CBBE", CancellationToken.None);
            var full = await ExportAsync(root, armor, mesh, project, compact: false, zip);
            var compact = await ExportAsync(root, armor, mesh, project, compact: true, zip);

            string[] RelativeFiles((ConversionRequest Request, IReadOnlyList<string> Files) export) =>
                export.Files.Where(path => !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    .Select(path => Path.GetRelativePath(export.Request.OutputDirectory!, path)).Order().ToArray();
            Assert.Equal(RelativeFiles(full), RelativeFiles(compact));

            foreach (var relative in RelativeFiles(compact))
            {
                var fullPath = Path.Combine(full.Request.OutputDirectory!, relative);
                var compactPath = Path.Combine(compact.Request.OutputDirectory!, relative);
                Assert.True(new FileInfo(compactPath).Length > 0, relative);
                if (relative.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                    !relative.StartsWith(".conversion-learning-cache", StringComparison.Ordinal))
                {
                    var fullText = await File.ReadAllTextAsync(fullPath);
                    var compactText = await File.ReadAllTextAsync(compactPath);
                    using var fullJson = JsonDocument.Parse(fullText);
                    using var compactJson = JsonDocument.Parse(compactText);
                    Assert.Equal(PropertyPaths(fullJson.RootElement), PropertyPaths(compactJson.RootElement));
                    if (!relative.EndsWith(".slidesmith-body.json", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.True(!compactText.Contains('\n'), $"Indented diagnostic: {relative}");
                        Assert.True(compactText.Length <= fullText.Length, relative);
                    }
                }
                else if (!relative.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                         !relative.Equals("README.txt", StringComparison.Ordinal) &&
                         !relative.Equals("remaining-gaps-checklist.md", StringComparison.Ordinal) &&
                         !relative.StartsWith(".conversion-learning-cache", StringComparison.Ordinal))
                {
                    var fullBytes = await File.ReadAllBytesAsync(fullPath);
                    var compactBytes = await File.ReadAllBytesAsync(compactPath);
                    Assert.True(fullBytes.SequenceEqual(compactBytes), $"Changed install artifact: {relative}");
                }
            }

            foreach (var name in new[] { "preview.html", "preview-workbench.html" })
            {
                var compactHtml = await File.ReadAllTextAsync(Path.Combine(compact.Request.OutputDirectory!, name));
                var fullHtml = await File.ReadAllTextAsync(Path.Combine(full.Request.OutputDirectory!, name));
                Assert.Contains("Compact diagnostics", compactHtml);
                Assert.Contains("not geometry or runtime proof", compactHtml);
                Assert.Contains("conversion-quality.json", compactHtml);
                Assert.Contains("preview.svg", compactHtml);
                Assert.DoesNotContain("<script", compactHtml);
                Assert.True(compactHtml.Length < fullHtml.Length / 2, name);
            }

            var config = XDocument.Load(Path.Combine(compact.Request.OutputDirectory!, "fomod", "ModuleConfig.xml"));
            foreach (var file in config.Descendants("file"))
            {
                Assert.True(File.Exists(Path.Combine(compact.Request.OutputDirectory!, file.Attribute("source")!.Value)));
            }

            var plugins = new PluginAnalysisResult([], [], "");
            var compactIssues = LocalExportService.BuildPackageArtifactIssues(compact.Request, armor,
                compact.Request.OutputDirectory!, compact.Files, project, plugins);
            var fullIssues = LocalExportService.BuildPackageArtifactIssues(full.Request, armor,
                full.Request.OutputDirectory!, full.Files, project, plugins);
            Assert.Equal(fullIssues.Select(issue => issue.Code).Order(), compactIssues.Select(issue => issue.Code).Order());
            Assert.DoesNotContain(compactIssues, issue =>
                issue.Code.StartsWith("missing-", StringComparison.Ordinal) ||
                issue.Code == "invalid-package-artifact");

            if (zip)
            {
                var archivePath = Assert.Single(compact.Files, path => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                using var archive = ZipFile.OpenRead(archivePath);
                using var fullArchive = ZipFile.OpenRead(Assert.Single(full.Files,
                    path => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)));
                Assert.Equal(fullArchive.Entries.Select(entry => entry.FullName).Order(),
                    archive.Entries.Select(entry => entry.FullName).Order());
                foreach (var entry in archive.Entries)
                {
                    using var source = File.OpenRead(Path.Combine(compact.Request.OutputDirectory!,
                        entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    using var archived = entry.Open();
                    using var copy = new MemoryStream();
                    await archived.CopyToAsync(copy);
                    Assert.Equal(source.Length, copy.Length);
                    Assert.Equal(await File.ReadAllBytesAsync(source.Name), copy.ToArray());
                }
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CompactHtmlEscapesNamesAndKeepsActionableIssues()
    {
        var method = typeof(LocalExportService).GetMethod("BuildCompactDiagnosticHtml",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var summary = new ConversionValidationSummary("FAIL", 0, 1, 0, 0,
            [new ConversionValidationIssue("unsupported-source-nif", "high", "<unsafe> mesh needs repair")]);
        var html = (string)method.Invoke(null,
            [new ConversionRequest("armor.nif", "<target>", CompactDiagnostics: true),
             new ImportedArmor("armor.nif", ["armor.nif"], [], [], []), summary, "<title>"])!;
        Assert.Contains("&lt;target&gt;", html);
        Assert.Contains("&lt;title&gt;", html);
        Assert.Contains("&lt;unsafe&gt;", html);
        Assert.Contains("FAIL", html);
        Assert.DoesNotContain("<unsafe>", html);
    }

    private static string[] PropertyPaths(JsonElement element, string prefix = "")
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            return element.EnumerateObject().SelectMany(property =>
                new[] { prefix + property.Name }.Concat(PropertyPaths(property.Value, prefix + property.Name + "."))).Order().ToArray();
        }
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().SelectMany((item, index) => PropertyPaths(item, prefix + index + ".")).Order().ToArray();
        }
        return [];
    }

    private static async Task<(ConversionRequest Request, IReadOnlyList<string> Files)> ExportAsync(
        string root, ImportedArmor armor, ConvertedMesh mesh, BodySlideProject project, bool compact, bool zip)
    {
        var request = new ConversionRequest(root, "CBBE", Path.Combine(root, compact ? "compact" : "full"),
            OutputZip: zip, CompactDiagnostics: compact);
        var result = await new LocalExportService().ExportAsync(request, armor,
            new MeshAnalysis("plate", false, 1), mesh, new MorphSet("low", "high", true),
            new PhysicsConfig("none", "<CBPCConfig><bone name=\"NPC L Breast01\"/></CBPCConfig>",
                "<system><bone name=\"NPC L Breast01\"/></system>"),
            new ClippingReport(false, [], []), new CorrectionResult(false, "not-required"), project,
            new PluginAnalysisResult([], [], ""), new TextureSummary(0, [], [], []),
            new PoseSimulationResult([], new Dictionary<string, IReadOnlyList<string>>(), [], 0),
            ["conversion:test"], new BodyDetectionReport("CBBE", 1, ["test"]),
            new SkeletonMappingResult("XPMSSE", "CBBE", [], []), null,
            new VoxelCollisionResult(false, [], new Dictionary<string, double>(), 16), CancellationToken.None);
        return (request, result.OutputFiles);
    }
}
