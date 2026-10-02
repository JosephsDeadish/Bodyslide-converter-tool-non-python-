using System.IO.Compression;
using System.Security;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class PackageArtifactRegressionTests
{
    [Theory]
    [InlineData("README.txt", "missing-readme")]
    [InlineData("conversion-quality.json", "missing-conversion-quality-report")]
    [InlineData("conversion-manifest.json", "missing-conversion-manifest")]
    [InlineData("fomod/ModuleConfig.xml", "missing-fomod-module-config")]
    public void DeletedRecordedArtifactsAreNotExistenceEvidence(string relativePath, string expectedCode)
    {
        using var package = new PackageFixture();
        var recordedFiles = Directory.GetFiles(package.Root, "*", SearchOption.AllDirectories);
        File.Delete(package.PathFor(relativePath));

        var issues = package.Verify(recordedFiles);

        Assert.Contains(issues, issue => issue.Code == expectedCode);
        Assert.Contains(issues, issue => issue.Code == "missing-recorded-output-artifact");
    }

    [Fact]
    public void DeletedRecordedZipProducesMissingIssueRatherThanReadFailure()
    {
        using var package = new PackageFixture();
        var issues = package.Verify([package.Root + ".zip"], outputZip: true);

        Assert.Contains(issues, issue => issue.Code == "missing-output-zip");
        Assert.DoesNotContain(issues, issue => issue.Code == "zip-validation-failed");
    }

    [Theory]
    [InlineData("conversion-quality.json", "")]
    [InlineData("conversion-quality.json", "{\"Validation\":")]
    [InlineData("conversion-quality.json", "null")]
    [InlineData("conversion-quality.json", "[]")]
    [InlineData("conversion-manifest.json", "{")]
    [InlineData("dependency-map.json", "false")]
    [InlineData("fomod/ModuleConfig.xml", "<config>")]
    [InlineData("fomod/ModuleConfig.xml", "<unrelated/>")]
    [InlineData("fomod/info.xml", "<unrelated/>")]
    [InlineData("preview.svg", "<html/>")]
    public void InvalidRequiredArtifactsCannotPassExistenceOnlyChecks(string relativePath, string content)
    {
        using var package = new PackageFixture();
        package.Write(relativePath, content);

        var issues = package.Verify();

        Assert.Contains(issues, issue => issue.Code == "invalid-package-artifact" &&
                                        issue.Message.Contains(relativePath, StringComparison.Ordinal));
    }

    [Fact]
    public void RecordedFallbackManifestIsValidatedWithoutRequiringPreferredName()
    {
        using var package = new PackageFixture();
        File.Move(package.PathFor("conversion-manifest.json"), package.PathFor("conversion-manifest-fallback.json"));

        var issues = package.Verify([package.PathFor("conversion-manifest-fallback.json")]);

        Assert.DoesNotContain(issues, issue => issue.Code == "missing-conversion-manifest");
        Assert.DoesNotContain(issues, issue => issue.Code == "invalid-package-artifact");
    }

    [Fact]
    public void CompleteMinimalPackageRetainsNoArtifactFailures()
    {
        using var package = new PackageFixture();
        Assert.Empty(package.Verify());
    }

    [Fact]
    public void EmptyStagedMeshCannotSatisfyInstallableMeshGate()
    {
        using var package = new PackageFixture();
        package.Write("meshes/slidesmith/cbbe/armor_0.nif", string.Empty);

        Assert.Contains(package.Verify(), issue => issue.Code == "missing-staged-mesh-output" && issue.Severity == "high");
    }

    [Fact]
    public void EmptyReferenceNifCannotSatisfyBodySlideSourceReference()
    {
        using var package = new PackageFixture();
        package.WriteBodySlide();
        package.Write("CalienteTools/BodySlide/ShapeData/Project/armor_0.nif", string.Empty);

        var issues = package.Verify(bodySlide: true);

        Assert.Contains(issues, issue => issue.Code == "missing-bodyslide-shape-data");
        Assert.Contains(issues, issue => issue.Code == "bodyslide-semantic-mismatch" &&
            issue.Message.Contains("missing ShapeData NIFs", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CalienteTools/BodySlide/ShapeData/Project/../armor_0.nif")]
    [InlineData("CalienteTools/BodySlide/ShapeData/Project/sub/../../armor_0.nif")]
    [InlineData("other/Project/armor_0.nif")]
    [InlineData("/CalienteTools/BodySlide/ShapeData/Project/armor_0.nif")]
    [InlineData("C:/CalienteTools/BodySlide/ShapeData/Project/armor_0.nif")]
    [InlineData("CalienteTools\\BodySlide\\ShapeData\\Project\\..\\armor_0.nif")]
    [InlineData("CalienteTools/BodySlide/ShapeData/Project/armor_0.bsd")]
    public void SourceFileMustActuallyStayInsideGeneratedShapeData(string sourceFile)
    {
        using var package = new PackageFixture();
        package.WriteBodySlide(sourceFile: sourceFile);

        var issues = package.Verify(bodySlide: true);

        Assert.Contains(issues, issue => issue.Code == "bodyslide-semantic-mismatch" &&
                                        issue.Message.Contains("SourceFile", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceFileCannotBorrowExistingLeafFromAnotherDirectory()
    {
        using var package = new PackageFixture();
        package.WriteBodySlide(sourceFile: "CalienteTools/BodySlide/ShapeData/Project/missing/armor_0.nif");

        Assert.Contains(package.Verify(bodySlide: true), issue => issue.Code == "bodyslide-semantic-mismatch" &&
            issue.Message.Contains("missing ShapeData NIFs", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CalienteTools/BodySlide/ShapeData/Project/armor_0.nif")]
    [InlineData("CalienteTools\\BodySlide\\ShapeData\\Project\\armor_0.nif")]
    public void GeneratedSourceFileSeparatorsRemainSupported(string sourceFile)
    {
        using var package = new PackageFixture();
        package.WriteBodySlide(sourceFile: sourceFile);

        Assert.DoesNotContain(package.Verify(bodySlide: true), issue =>
            issue.Message.Contains("SourceFile", StringComparison.Ordinal) ||
            issue.Message.Contains("missing ShapeData NIFs", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/CalienteTools/BodySlide/ShapeData/Project", "meshes/slidesmith/cbbe", "armor_0.nif", "SetFolder")]
    [InlineData("other/CalienteTools/BodySlide/ShapeData/Project", "meshes/slidesmith/cbbe", "armor_0.nif", "SetFolder")]
    [InlineData("CalienteTools/BodySlide/ShapeData/Project", "/meshes/slidesmith/cbbe", "armor_0.nif", "OutputPath")]
    [InlineData("CalienteTools/BodySlide/ShapeData/Project", "meshes/../outside", "armor_0.nif", "OutputPath")]
    [InlineData("CalienteTools/BodySlide/ShapeData/Project", "meshes/slidesmith/cbbe", "../armor_0.nif", "OutputFile")]
    public void BodySlideRoutingRejectsRootedOrTraversingPaths(string setFolder, string outputPath, string outputFile, string field)
    {
        using var package = new PackageFixture();
        package.WriteBodySlide(setFolder: setFolder, outputPath: outputPath, outputFile: outputFile);

        Assert.Contains(package.Verify(bodySlide: true), issue => issue.Code == "bodyslide-semantic-mismatch" &&
            issue.Message.Contains(field, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("bsd")]
    [InlineData("tri")]
    [InlineData("osd")]
    public void UnreadableMorphPayloadIsReportedEvenWithoutExpectedSliders(string extension)
    {
        using var package = new PackageFixture();
        package.WriteBodySlide();
        package.Write($"CalienteTools/BodySlide/ShapeData/Project/broken.{extension}", "broken payload");

        Assert.Contains(package.Verify(bodySlide: true), issue => issue.Code == "bodyslide-semantic-mismatch" &&
            issue.Message.Contains($"ShapeData {extension.ToUpperInvariant()} payload", StringComparison.Ordinal));
    }

    [Fact]
    public void FomodCommentsDoNotCountAsInstallerEntries()
    {
        using var package = new PackageFixture();
        package.Write("fomod/ModuleConfig.xml", "<config><!-- <folder source=\"meshes\"/> --></config>");

        Assert.Contains(package.Verify(), issue => issue.Code == "fomod-missing-folder-entry");
    }

    [Fact]
    public void FomodSingleQuotedAttributesAreRealInstallerEntries()
    {
        using var package = new PackageFixture();
        package.Write("fomod/ModuleConfig.xml", "<config><folder source='meshes' destination='meshes'/></config>");

        Assert.DoesNotContain(package.Verify(), issue => issue.Code == "fomod-missing-folder-entry");
    }

    [Theory]
    [InlineData("README.txt", false, "zip-missing-readme")]
    [InlineData("README.txt/", true, "zip-missing-readme")]
    [InlineData("/README.txt", true, "zip-invalid-entry-path")]
    [InlineData("meshes/../README.txt", true, "zip-invalid-entry-path")]
    [InlineData("C:/README.txt", true, "zip-invalid-entry-path")]
    public void ZipDirectoryEmptyAndUnsafeEntriesAreNotValidPackageEvidence(string entryName, bool content, string expectedCode)
    {
        using var package = new PackageFixture();
        using (var archive = ZipFile.Open(package.Root + ".zip", ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry(entryName);
            if (content)
            {
                using var writer = new StreamWriter(entry.Open());
                writer.Write("content");
            }
        }

        Assert.Contains(package.Verify(outputZip: true), issue => issue.Code == expectedCode);
    }

    private sealed class PackageFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Environment.CurrentDirectory, $"package-artifact-{Guid.NewGuid():N}");

        public PackageFixture()
        {
            Write("README.txt", "Manual in-game validation is still required.");
            Write("meta.ini", "[General]");
            foreach (var name in new[]
                     {
                         "conversion-manifest.json", "dependency-map.json", "conversion-quality.json",
                         "skeleton-compatibility.json", "pose-simulation-report.json", "world-physics.json"
                     })
            {
                Write(name, "{}");
            }

            Write("preview.svg", "<svg/>");
            Write("preview.html", "<html/>");
            Write("preview-workbench.html", "<html/>");
            Write("meshes/slidesmith/cbbe/armor_0.nif", "synthetic mesh");
            Write("fomod/ModuleConfig.xml", "<config><folder source=\"meshes\" destination=\"meshes\"/></config>");
            Write("fomod/info.xml", "<fomod/>");
        }

        public string PathFor(string relativePath) => Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public void Write(string relativePath, string content)
        {
            var path = PathFor(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void WriteBodySlide(
            string sourceFile = "CalienteTools/BodySlide/ShapeData/Project/armor_0.nif",
            string setFolder = "CalienteTools/BodySlide/ShapeData/Project",
            string outputPath = "meshes/slidesmith/cbbe",
            string outputFile = "armor_0.nif")
        {
            Write("CalienteTools/BodySlide/ShapeData/Project/armor_0.nif", "synthetic reference mesh");
            Write("CalienteTools/BodySlide/SliderGroups/Project.xml", "<SliderGroups><Group name=\"Project\"><Member name=\"Project\"/></Group></SliderGroups>");
            Write("CalienteTools/BodySlide/SliderSets/Project.osp", $"""
                <SliderSetInfo version="1"><SliderSet name="Project">
                  <SetFolder>{SecurityElement.Escape(setFolder)}</SetFolder>
                  <SourceFile>{SecurityElement.Escape(sourceFile)}</SourceFile>
                  <OutputPath>{SecurityElement.Escape(outputPath)}</OutputPath>
                  <OutputFile gender="f">{SecurityElement.Escape(outputFile)}</OutputFile>
                </SliderSet></SliderSetInfo>
                """);
            Write("fomod/ModuleConfig.xml", "<config><folder source=\"meshes\"/><folder source=\"CalienteTools\"/></config>");
        }

        public IReadOnlyList<ConversionValidationIssue> Verify(
            IReadOnlyList<string>? recordedFiles = null,
            bool bodySlide = false,
            bool outputZip = false)
        {
            var request = new ConversionRequest(PathFor("input.nif"), "CBBE",
                OutputDirectory: Root, GenerateBodySlideFiles: bodySlide, OutputZip: outputZip);
            return LocalExportService.BuildPackageArtifactIssues(
                request, new ImportedArmor(request.InputPath, [request.InputPath], [], [], []),
                Root, recordedFiles ?? [], new BodySlideProject("Project", "CBBE", [], string.Empty),
                new PluginAnalysisResult([], [], string.Empty));
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
            if (File.Exists(Root + ".zip"))
            {
                File.Delete(Root + ".zip");
            }
        }
    }
}
