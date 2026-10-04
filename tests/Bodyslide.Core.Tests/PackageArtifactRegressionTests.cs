using System.IO.Compression;
using System.Security;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class PackageArtifactRegressionTests
{
    [Fact]
    public void WindowsBundleInstallerDeploysAllDesktopDependenciesBesideExecutableWithoutDuplicateRuntime()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "BodyslideConverter.slnx")))
        {
            repository = repository.Parent;
        }
        Assert.NotNull(repository);
        var config = System.Xml.Linq.XDocument.Load(Path.Combine(repository.FullName,
            "packaging", "windows-bundle", "fomod", "ModuleConfig.xml"));
        var desktopFolder = Assert.Single(config.Descendants("folder"), element =>
            string.Equals(element.Attribute("source")?.Value, "desktop", StringComparison.Ordinal));
        Assert.Equal("CalienteTools/SlideSmith", desktopFolder.Attribute("destination")?.Value);
        Assert.DoesNotContain(config.Descendants("file"), element =>
            string.Equals(element.Attribute("source")?.Value, "desktop/SlideSmith.exe", StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> InvalidRuntimePhysicsArtifacts()
    {
        foreach (var root in new[] { "CBPCConfig", "system" })
        {
            foreach (var staged in new[] { false, true })
            {
                foreach (var xml in new[]
                         {
                             string.Empty,
                             $"<{root}/>",
                             $"<{root}><bone name=\"NPC L Breast01\"/>",
                             "<unrelated><bone name=\"NPC L Breast01\"/></unrelated>",
                             $"<{root}><bone name=\" \"/></{root}>",
                             $"<{root}><!-- <bone name=\"NPC L Breast01\"/> --></{root}>",
                             $"<!DOCTYPE {root} [<!ENTITY bone 'NPC L Breast01'>]><{root}><bone name=\"&bone;\"/></{root}>"
                         })
                {
                    yield return [root, staged, xml];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(InvalidRuntimePhysicsArtifacts))]
    public void InvalidExportedPhysicsCannotBorrowEvidenceFromValidPartner(string root, bool staged, string xml)
    {
        using var package = new PackageFixture();
        var (rootPath, stagedPath) = PhysicsPaths(root);
        var validXml = $"<{root}><bone name=\"NPC L Breast01\"/></{root}>";
        package.Write(rootPath, staged ? validXml : xml);
        package.Write(stagedPath, staged ? xml : validXml);

        var invalidPath = staged ? stagedPath : rootPath;
        Assert.Contains(package.Verify(), issue => issue.Code == "physics-config-semantic-mismatch" &&
            issue.Message.Contains(invalidPath.Replace('/', Path.DirectorySeparatorChar), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CBPCConfig")]
    [InlineData("system")]
    public void LoneMalformedStagedPhysicsIsValidatedEvenWithoutRoot(string root)
    {
        using var package = new PackageFixture();
        var (_, stagedPath) = PhysicsPaths(root);
        package.Write(stagedPath, $"<{root}><bone name=\"NPC L Breast01\"/>");

        Assert.Contains(package.Verify(), issue => issue.Code == "physics-config-semantic-mismatch" &&
            issue.Message.Contains("could not be validated", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CBPCConfig", "NPC R Breast01", "1")]
    [InlineData("system", "NPC R Breast01", "1")]
    [InlineData("CBPCConfig", "NPC L Breast01", "2")]
    [InlineData("system", "NPC L Breast01", "2")]
    public void ChangedStagedPhysicsBonesOrSolverValuesAreNotMatchingEvidence(string root, string stagedBone, string stagedValue)
    {
        using var package = new PackageFixture();
        var (rootPath, stagedPath) = PhysicsPaths(root);
        package.Write(rootPath, $"<{root}><bone name=\"NPC L Breast01\" mass=\"1\"/></{root}>");
        package.Write(stagedPath, $"<{root}><bone name=\"{stagedBone}\" mass=\"{stagedValue}\"/></{root}>");

        Assert.Contains(package.Verify(), issue => issue.Code == "physics-config-semantic-mismatch" &&
            issue.Message.Contains("does not match", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("CBPCConfig")]
    [InlineData("system")]
    public void MatchingPhysicsXmlAcceptsWhitespaceAndAttributeQuoteDifferences(string root)
    {
        using var package = new PackageFixture();
        var (rootPath, stagedPath) = PhysicsPaths(root);
        package.Write(rootPath, $"<{root}><bone name=\"NPC L Breast01\"/></{root}>");
        package.Write(stagedPath, $"<{root}>\n  <bone name='NPC L Breast01' />\n</{root}>");

        Assert.DoesNotContain(package.Verify(), issue => issue.Code == "physics-config-semantic-mismatch" &&
            (issue.Message.Contains("does not match", StringComparison.Ordinal) ||
             issue.Message.Contains("could not be validated", StringComparison.Ordinal)));
    }

    [Fact]
    public void ValidPhysicsBoneNamesAreDecodedFromXmlRatherThanRegex()
    {
        using var package = new PackageFixture();
        var (rootPath, stagedPath) = PhysicsPaths("CBPCConfig");
        const string xml = "<CBPCConfig><bone mass='1' name='NPC L Breast&#48;1'/></CBPCConfig>";
        package.Write(rootPath, xml);
        package.Write(stagedPath, xml);

        var encodedIssues = package.Verify();
        package.Write(rootPath, "<CBPCConfig><bone name=\"NPC L Breast01\" mass=\"1\"/></CBPCConfig>");
        package.Write(stagedPath, "<CBPCConfig><bone name=\"NPC L Breast01\" mass=\"1\"/></CBPCConfig>");
        Assert.Equal(package.Verify(), encodedIssues);
    }

    [Theory]
    [InlineData("cbpc")]
    [InlineData("smp")]
    [InlineData("cbpc+smp")]
    public async Task GeneratedPhysicsPairsRetainNoRuntimeArtifactFailures(string profile)
    {
        using var package = new PackageFixture();
        var weighted = await new BasicWeightTransferService().TransferAsync(
            new ConvertedMesh("physics-enabled", "vertex-projection", 1, new Dictionary<string, double>()),
            new MeshAnalysis("physics-enabled", true, 1), "CBBE", null, CancellationToken.None);
        var physics = await new BasicPhysicsSupportService().BuildAsync(weighted, "CBBE", profile, CancellationToken.None);
        if (profile.Contains("cbpc", StringComparison.Ordinal)) Assert.NotNull(physics.CbpcConfigXml);
        if (profile.Contains("smp", StringComparison.Ordinal)) Assert.NotNull(physics.SmpConfigXml);
        foreach (var (xml, root) in new[] { (physics.CbpcConfigXml, "CBPCConfig"), (physics.SmpConfigXml, "system") })
        {
            if (xml is null) continue;
            var (rootPath, stagedPath) = PhysicsPaths(root);
            package.Write(rootPath, xml);
            package.Write(stagedPath, xml);
        }
        package.Write("fomod/ModuleConfig.xml", "<config><folder source=\"meshes\"/><folder source=\"SKSE\"/></config>");

        Assert.Empty(package.Verify());
    }

    [Fact]
    public void ValidStagedPhysicsWithoutRootIsNotMatchingPackageEvidence()
    {
        using var package = new PackageFixture();
        package.Write("SKSE/Plugins/hdtSMP64/smp-config.xml", "<system><bone name=\"NPC L Breast01\"/></system>");

        Assert.Contains(package.Verify(), issue => issue.Code == "physics-config-semantic-mismatch" &&
            issue.Message.Contains("no corresponding exported root config", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedTargetCannotBypassRuntimeXmlValidation()
    {
        using var package = new PackageFixture();
        package.Write("smp-config.xml", "<system><bone name=\"CustomBone\"/>");

        Assert.Contains(package.Verify(targetBody: "UnknownCustomTarget"), issue =>
            issue.Code == "physics-config-semantic-mismatch" &&
            issue.Message.Contains("could not be validated", StringComparison.Ordinal));
    }

    private static (string RootPath, string StagedPath) PhysicsPaths(string root) =>
        root == "CBPCConfig"
            ? ("cbpc-config.xml", "SKSE/Plugins/CBPCSystem/cbpc-config.xml")
            : ("smp-config.xml", "SKSE/Plugins/hdtSMP64/smp-config.xml");

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
                                        issue.Message.Replace('\\', '/').Contains(relativePath, StringComparison.Ordinal));
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
    public void GeneratedDependencyArrayIsValidPackageEvidence()
    {
        using var package = new PackageFixture();
        package.Write("dependency-map.json", System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new MeshDependencyMapEntry("armor.nif", [], [], [], [], "CBBE")
        }));
        Assert.DoesNotContain(package.Verify(), issue => issue.Code == "invalid-package-artifact" &&
            issue.Message.Contains("dependency-map.json", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("[{\"Mesh\":\"\"}]")]
    [InlineData("[{\"Mesh\":42}]")]
    public void InvalidDependencyArraysStillRequireReview(string content)
    {
        using var package = new PackageFixture();
        package.Write("dependency-map.json", content);
        Assert.Contains(package.Verify(), issue => issue.Code == "invalid-package-artifact" &&
            issue.Message.Contains("dependency-map.json", StringComparison.Ordinal));
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
    public void BodySlidePayloadSnapshotChecksAllTypesAndIsFreshForEachValidation()
    {
        using var package = new PackageFixture();
        package.WriteBodySlide();
        foreach (var extension in new[] { "bsd", "tri", "osd" })
        {
            package.Write($"CalienteTools/BodySlide/ShapeData/Project/broken.{extension}", "broken payload");
        }

        foreach (var extension in new[] { "bsd", "tri", "osd" })
        {
            Assert.Contains(package.Verify(bodySlide: true), issue => issue.Code == "bodyslide-semantic-mismatch" &&
                issue.Message.Contains($"ShapeData {extension.ToUpperInvariant()} payload 'broken.{extension}'", StringComparison.Ordinal));
            File.Delete(package.PathFor($"CalienteTools/BodySlide/ShapeData/Project/broken.{extension}"));
        }
        Assert.DoesNotContain(package.Verify(bodySlide: true), issue =>
            issue.Code == "bodyslide-semantic-mismatch" && issue.Message.Contains("broken.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("bsd")]
    [InlineData("tri")]
    [InlineData("osd")]
    public void BodySlidePayloadSnapshotPreservesTopDirectoryOnlyValidation(string extension)
    {
        using var package = new PackageFixture();
        package.WriteBodySlide();
        package.Write($"CalienteTools/BodySlide/ShapeData/Project/nested/broken.{extension}", "broken payload");

        Assert.DoesNotContain(package.Verify(bodySlide: true), issue =>
            issue.Code == "bodyslide-semantic-mismatch" && issue.Message.Contains("broken.", StringComparison.Ordinal));
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
            bool outputZip = false,
            string targetBody = "CBBE")
        {
            var request = new ConversionRequest(PathFor("input.nif"), targetBody,
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
