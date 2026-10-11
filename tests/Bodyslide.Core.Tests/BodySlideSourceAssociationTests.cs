using Bodyslide.Core;
using System.Text;
using System.Xml.Linq;

namespace Bodyslide.Core.Tests;

public sealed class BodySlideSourceAssociationTests
{
    [Theory]
    [InlineData(@"meshes\armor\first\", @"meshes\armor\second\")]
    [InlineData("armor/first", "armor/second")]
    [InlineData("meshes/armor/first/", "meshes/armor/first_extra/")]
    public async Task SameNamedMeshesUseDeclaredOutputDirectory(string ownPath, string otherPath)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var meshDirectory = Path.Combine(root, "meshes", "armor", "first");
            var projects = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
            Directory.CreateDirectory(meshDirectory);
            Directory.CreateDirectory(projects);
            var mesh = Path.Combine(meshDirectory, "jacket_0.nif");
            var project = Path.Combine(projects, "pack.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, $"""
                <SliderSetInfo>
                  <SliderSet name="First"><OutputPath>{ownPath}</OutputPath><OutputFile>jacket</OutputFile>
                    <Slider name="FirstFit"/><Slider name="HideFirst" zap="true"/>
                  </SliderSet>
                  <SliderSet name="Second"><OutputPath>{otherPath}</OutputPath><OutputFile>jacket</OutputFile>
                    <Slider name="SecondFit"/><Slider name="HideSecond" zap="true"/>
                  </SliderSet>
                </SliderSetInfo>
                """);
            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);
            Assert.Contains("FirstFit", result.Sliders);
            Assert.Contains("HideFirst", result.ZapSliders);
            Assert.DoesNotContain("SecondFit", result.Sliders);
            Assert.DoesNotContain("HideSecond", result.ZapSliders);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OsdAssociationRequiresExactLinkedMorphRecordName()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "shared.osd"), OsdMorph("Waist_1"));
            await File.WriteAllTextAsync(project, OspWithDataTarget("Torso"));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            var payload = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Equal("Waist_1", payload.SourceMorphRecordName);
            Assert.Null(payload.SourceShapeName);
            Assert.Null(payload.SourceVertexOrderFingerprint);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Torso", false, true)]
    [InlineData("OtherShape", false, false)]
    [InlineData("Torso", true, false)]
    public async Task OsdPayloadReuseRequiresExactUniqueInputShapeCorrespondence(
        string inputShapeName,
        bool duplicateExactMesh,
        bool expectedVerified)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            var inputNif = SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets(inputShapeName);
            await File.WriteAllBytesAsync(mesh, inputNif);
            var inputMeshes = new List<string> { mesh };
            if (duplicateExactMesh)
            {
                var duplicateMesh = Path.Combine(root, "meshes", "duplicate", "copy.nif");
                Directory.CreateDirectory(Path.GetDirectoryName(duplicateMesh)!);
                await File.WriteAllBytesAsync(duplicateMesh, inputNif);
                inputMeshes.Add(duplicateMesh);
            }
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "shared.osd"), OsdMorph("Waist"));
            await File.WriteAllTextAsync(project, OspWithDataTarget("Torso"));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, inputMeshes, [], [], [project]), "CBBE", CancellationToken.None);

            var payload = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Equal(expectedVerified ? "verified" : "unresolved", payload.ShapeIdentityStatus);
            Assert.Equal(expectedVerified ? "verified" : "unverified", payload.VertexOrderStatus);
            if (expectedVerified)
            {
                Assert.Equal("Torso", payload.SourceShapeName);
                Assert.Equal("Torso", payload.TargetShapeName);
                Assert.Equal(3, payload.VertexCount);
                Assert.Equal(3, payload.Deltas.Count);
                Assert.Equal((0.25f, 0f, 0f), payload.Deltas[0]);
                Assert.Equal((0f, 0f, 0f), payload.Deltas[1]);
                Assert.Equal(payload.SourceVertexOrderFingerprint, payload.TargetVertexOrderFingerprint);
                Assert.Contains(
                    payload.ShapeCorrespondenceEvidence!,
                    static evidence => evidence.StartsWith(
                        "exact-ordered-shape-geometry-and-topology-sha256:",
                        StringComparison.Ordinal));
            }
            else
            {
                Assert.Null(payload.TargetShapeName);
                Assert.Null(payload.TargetVertexOrderFingerprint);
                Assert.Null(payload.ShapeCorrespondenceEvidence);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Torso", true)]
    [InlineData("OtherShape", false)]
    public async Task DynamicShapeOsdReuseRequiresExactShapeCorrespondence(
        string inputShapeName,
        bool expectedVerified)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllBytesAsync(mesh, SkyrimSseNifShapeReaderTests.CreateDynamicNifForShapeTargets(inputShapeName));
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateDynamicNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "shared.osd"), OsdMorph("Waist"));
            await File.WriteAllTextAsync(project, OspWithDataTarget("Torso"));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            var payload = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Equal(expectedVerified ? "verified" : "unresolved", payload.ShapeIdentityStatus);
            Assert.Equal(expectedVerified ? "verified" : "unverified", payload.VertexOrderStatus);
            if (expectedVerified)
            {
                Assert.Equal("Torso", payload.SourceShapeName);
                Assert.Equal("Torso", payload.TargetShapeName);
                Assert.Equal(payload.SourceVertexOrderFingerprint, payload.TargetVertexOrderFingerprint);
                Assert.Contains(payload.ShapeCorrespondenceEvidence!, static evidence =>
                    evidence.StartsWith("exact-ordered-shape-geometry-and-topology-sha256:", StringComparison.Ordinal));
            }
            else
            {
                Assert.Null(payload.TargetShapeName);
                Assert.Null(payload.ShapeCorrespondenceEvidence);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportedPackProjectsDoNotAddUnrelatedArmorSliders(bool singleProjectFile)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var meshes = Path.Combine(root, "meshes");
            var projects = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
            Directory.CreateDirectory(meshes);
            Directory.CreateDirectory(projects);
            var mesh = Path.Combine(meshes, "jacket_0.nif");
            await File.WriteAllTextAsync(mesh, "mesh");
            var first = Path.Combine(projects, "jacket.osp");
            var second = Path.Combine(projects, "boots.osp");
            const string own = "<SliderSet name=\"Jacket\"><OutputFile>jacket</OutputFile><Slider name=\"JacketFit\"/><Slider name=\"HideJacket\" zap=\"true\"/></SliderSet>";
            const string other = "<SliderSet name=\"Boots\"><OutputFile>boots.nif</OutputFile><Slider name=\"BootFit\"/><Slider name=\"HideBoots\" zap=\"true\"/></SliderSet>";
            await File.WriteAllTextAsync(first, "<SliderSetInfo>" + own + (singleProjectFile ? other : "") + "</SliderSetInfo>");
            if (!singleProjectFile) await File.WriteAllTextAsync(second, "<SliderSetInfo>" + other + "</SliderSetInfo>");
            var armor = new ImportedArmor(mesh, [mesh], [], [], singleProjectFile ? [first] : [first, second]);
            var result = await BodySlideSourceProjectSupport.ResolveAsync(armor, "CBBE", CancellationToken.None);

            Assert.Contains("JacketFit", result.Sliders);
            Assert.Contains("HideJacket", result.ZapSliders);
            Assert.DoesNotContain("BootFit", result.Sliders);
            Assert.DoesNotContain("HideBoots", result.ZapSliders);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("jacket_1.nif", true)]
    [InlineData("jacket", true)]
    [InlineData("jackets", false)]
    [InlineData("boots", false)]
    public async Task OutputMetadataUsesExactWeightNormalizedMeshIdentity(string outputFile, bool matches)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project,
                $"<SliderSetInfo><SliderSet><OutputFile>{outputFile}</OutputFile><Slider name=\"SourceFit\"/></SliderSet></SliderSetInfo>");
            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);
            Assert.Equal(matches, result.Sliders.Contains("SourceFit"));
            Assert.Equal(matches, result.SourceAssetSupport!.HasOsp);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task NamespacedOspSettingsAreDiscoveredButExplicitlyReportedAsUnpreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <osp:SliderSetInfo xmlns:osp="urn:example:bodyslide">
                  <osp:SliderSet name="Jacket">
                    <osp:OutputFile>jacket</osp:OutputFile>
                    <osp:Slider name="NamespacedFit" />
                  </osp:SliderSet>
                </osp:SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Contains("NamespacedFit", result.Sliders);
            Assert.True(result.SourceAssetSupport!.HasOsp);
            Assert.Contains("namespaced-osp-settings-not-preserved", result.SourceAssetSupport.UnsupportedOspSemantics);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MatchedOspReportsSettingsThatAreNotPreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket" baseShape="Custom Base" bsversion="19" seamNormals="true" customFlag="true">
                    <DataFolder customRoot="true">../shared-data</DataFolder>
                    <SourceFile variant="reference">base.nif</SourceFile>
                    <OutputPath mode="custom">meshes\custom\</OutputPath>
                    <OutputFile gender="female" GenWeights="true" options="custom">jacket</OutputFile>
                    <Slider xmlns:vendor="urn:vendor" vendor:default="0.9" name="Fit" default="0.25" small="0.1" big="0.8" invert="true" uv="true">
                      <Data target="Torso">shared.osd#Fit</Data>
                      <Low value="5" custom="true" />
                      <High value="95" />
                      <CustomSliderSetting enabled="true" />
                    </Slider>
                    <Reference>reference.nif</Reference>
                    <Zap target="Sleeves" />
                    <Shape target="Torso">jacket.nif</Shape>
                    <CustomOption enabled="true" />
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Equal(
                new[]
                {
                    "custom-base-shape",
                    "data-folder-rebuilt",
                    "external-data-folder",
                    "namespaced-osp-settings-not-preserved",
                    "output-options",
                    "output-path-rebuilt",
                    "seam-or-lock-normal-settings",
                    "slider-set-unknown-attributes",
                    "slider-unknown-elements",
                    "slider-weight-range-options",
                    "slider-weight-ranges-rebuilt",
                    "source-osp-version",
                    "source-path-options",
                    "source-reference-links",
                    "source-shape-mappings",
                    "source-slider-data-links",
                    "unknown-slider-set-elements",
                    "uv-slider-data",
                    "weight-variant-output-mode-rebuilt",
                    "zap-target-semantics"
                },
                result.SourceAssetSupport!.UnsupportedOspSemantics);
            Assert.Equal(
                new OspSliderControlSettings(0.25, 0.1, 0.8, true, false),
                result.SliderControlSettings!["Fit"]);
            var target = new BodySlideMeshTarget(
                "Jacket", "Jacket", "base.nif", "meshes\\custom\\", "jacket_0.nif", "jacket_1.nif");
            var outputSlider = Assert.Single(
                XDocument.Parse(BodySlideOspProjectService.BuildOspXmlWithSliderSettings(
                    result.Sliders, result.ZapSliders, [target], "female", result.SliderControlSettings))
                    .Descendants("Slider"),
                static slider => (string?)slider.Attribute("name") == "Fit");
            Assert.Equal("false", (string?)outputSlider.Attribute("uv"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MatchedOspReportsOrdinaryDataFolderAsRebuilt()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket">
                    <DataFolder>JacketShapeData</DataFolder>
                    <Slider name="Fit" />
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Contains("data-folder-rebuilt", result.SourceAssetSupport!.UnsupportedOspSemantics);
            Assert.DoesNotContain("external-data-folder", result.SourceAssetSupport.UnsupportedOspSemantics);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MatchedOspNormalizesSimpleLowHighRangesToWeightControls()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket">
                    <OutputFile GenWeights="true">jacket</OutputFile>
                    <Slider name="Fit" invert="true">
                      <Low value="0.1" />
                      <High value="0.9" />
                    </Slider>
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Equal(new OspSliderControlSettings(null, 0.1, 0.9, true), result.SliderControlSettings!["Fit"]);
            Assert.DoesNotContain("slider-weight-ranges-rebuilt", result.SourceAssetSupport!.UnsupportedOspSemantics);
            Assert.DoesNotContain("slider-weight-range-options", result.SourceAssetSupport.UnsupportedOspSemantics);

            var target = new BodySlideMeshTarget(
                "Project", "Project", "source.nif", "meshes\\armor\\", "jacket_0.nif", "jacket_1.nif");
            var outputSlider = Assert.Single(XDocument.Parse(BodySlideOspProjectService.BuildOspXmlWithSliderSettings(
                ["Fit"], [], [target], "female", result.SliderControlSettings)).Descendants("Slider"));
            Assert.Equal("0.1", (string?)outputSlider.Attribute("small"));
            Assert.Equal("0.9", (string?)outputSlider.Attribute("big"));
            Assert.Equal("true", (string?)outputSlider.Attribute("invert"));
            Assert.Equal("false", (string?)outputSlider.Attribute("uv"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ConflictingOspSliderRangesAreReportedAndNotPreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket">
                    <OutputFile GenWeights="true">jacket</OutputFile>
                    <Slider name="Fit" small="0.2">
                      <Low value="0.1" />
                      <High value="0.9" />
                    </Slider>
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.DoesNotContain("Fit", result.SliderControlSettings!.Keys);
            Assert.Contains("ambiguous-slider-control-settings", result.SourceAssetSupport!.UnsupportedOspSemantics);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ConflictingOrInvalidOspSliderControlsAreNotPreservedSilently()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket">
                    <OutputFile>jacket</OutputFile>
                    <Slider name="Fit" default="NaN" invert="sometimes" />
                    <Slider name="Conflict" default="0.2" />
                    <Slider name="Conflict" default="0.8" />
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Equal("Fit", result.SliderControlSettings!.Keys.Single());
            Assert.Null(result.SliderControlSettings["Fit"].DefaultValue);
            Assert.False(result.SliderControlSettings["Fit"].Invert);
            Assert.Contains("invalid-slider-control-values", result.SourceAssetSupport!.UnsupportedOspSemantics);
            Assert.Contains("ambiguous-slider-control-settings", result.SourceAssetSupport.UnsupportedOspSemantics);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InvalidOspZapAndUvBooleansAreReported()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket">
                    <OutputFile>jacket</OutputFile>
                    <Slider name="Fit" zap="sometimes" uv="maybe" />
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Contains("Fit", result.Sliders);
            Assert.DoesNotContain("Fit", result.ZapSliders);
            Assert.Contains("invalid-slider-boolean-values", result.SourceAssetSupport!.UnsupportedOspSemantics);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OsdPayloadRecordsRetainOnlyExplicitOspShapeLinksVerifiedBySourceNif()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "shared.osd"), OsdMorph("Waist"));
            await File.WriteAllTextAsync(project, OspWithDataTarget("Torso"));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            var payload = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Equal("Torso", payload.SourceShapeName);
            Assert.Equal("Waist", payload.SourceMorphRecordName);
            Assert.Equal("unresolved", payload.ShapeIdentityStatus);
            Assert.Equal("unverified", payload.VertexOrderStatus);
            var sourceShape = Assert.Single(SkyrimSseNifShapeReader.Read(
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso")).Shapes);
            Assert.Equal(
                SkyrimSseNifShapeCorrespondence.ComputeVertexOrderFingerprint(sourceShape),
                payload.SourceVertexOrderFingerprint);
            Assert.Null(payload.TargetVertexOrderFingerprint);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DuplicateOsdMorphRecordsAreNotExposedAsReusablePayloads()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "shared.osd"),
                OsdMorphs("Waist", "Waist"));
            await File.WriteAllTextAsync(project, OspWithDataTarget("Torso"));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.False(result.ReusableMorphPayloads?.ContainsKey("Waist"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OsdPayloadWithIndexesOutsideLinkedSourceShapeIsNotShapeAssociated()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "shared.osd"), OsdMorph("Waist", index: 3));
            await File.WriteAllTextAsync(project, OspWithDataTarget("Torso"));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            var payload = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Null(payload.SourceShapeName);
            Assert.Equal("unresolved", payload.ShapeIdentityStatus);
            Assert.Equal("unverified", payload.VertexOrderStatus);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OsdPayloadLinkedFromDifferentSourceNifsRemainsAmbiguous()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "alternate.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "shared.osd"), OsdMorph("Waist"));
            await File.WriteAllTextAsync(project, OspWithMultipleSourceNifs());

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            var payload = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Null(payload.SourceShapeName);
            Assert.Equal("unresolved", payload.ShapeIdentityStatus);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OsdPayloadDoesNotAssociateWhenOspTargetIsNotInSourceNif()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso"));
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "shared.osd"), OsdMorph("Waist"));
            await File.WriteAllTextAsync(project, OspWithDataTarget("ArmorOverlay"));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            var payload = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Null(payload.SourceShapeName);
            Assert.Equal("unresolved", payload.ShapeIdentityStatus);
            Assert.Equal("unverified", payload.VertexOrderStatus);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OsdPayloadDoesNotAssociateWhenOspLinksOneRecordToMultipleShapes()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
        var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
        var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
        Directory.CreateDirectory(meshDirectory);
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(shapeDataDirectory);
        try
        {
            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "base.nif"),
                SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso", "ArmorOverlay"));
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "shared.osd"), OsdMorph("Waist"));
            await File.WriteAllTextAsync(project, OspWithDataTargets("Torso", "ArmorOverlay"));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            var payload = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Null(payload.SourceShapeName);
            Assert.Equal("unresolved", payload.ShapeIdentityStatus);
            Assert.Equal("unverified", payload.VertexOrderStatus);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MatchedOspReportsWeightVariantModeAsRebuilt()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var mesh = Path.Combine(root, "jacket_0.nif");
            var project = Path.Combine(root, "source.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="Jacket" baseShape="Base Shape" bsversion="20">
                    <DataFolder>Jacket</DataFolder>
                    <SourceFile>jacket.nif</SourceFile>
                    <OutputPath>meshes\armor\</OutputPath>
                    <OutputFile gender="female" GenWeights="true">jacket</OutputFile>
                    <Slider name="Fit" small="0" big="0" invert="false" zap="false" uv="false" />
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.Equal(
                ["data-folder-rebuilt", "output-path-rebuilt", "weight-variant-output-mode-rebuilt"],
                result.SourceAssetSupport!.UnsupportedOspSemantics);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BodyTriMorphsRetainTheirSourceShapeThroughSliderAssociation()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
            var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
            var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
            Directory.CreateDirectory(meshDirectory);
            Directory.CreateDirectory(projectDirectory);
            Directory.CreateDirectory(shapeDataDirectory);

            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="TravelerProject">
                    <DataFolder>TravelerProject</DataFolder>
                    <OutputPath>meshes\armor\traveler\</OutputPath>
                    <OutputFile>traveler_0.nif</OutputFile>
                    <Slider name="Waist" />
                  </SliderSet>
                </SliderSetInfo>
                """);
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "traveler.tri"),
                BodyTri(
                    ("Torso", "Waist", (ushort)0, (short)2),
                    ("ArmorOverlay", "Waist", (ushort)2, (short)3)));

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            Assert.NotNull(result.ReusableMorphPayloads);
            var waistCandidates = result.ReusableMorphPayloads!["Waist"].Payloads!;
            Assert.Equal(2, waistCandidates.Count);
            Assert.Contains(waistCandidates, candidate =>
                candidate.SourceShapeName == "Torso" &&
                candidate.VertexCount == 1 &&
                candidate.ShapeIdentityStatus == "unresolved" &&
                candidate.VertexOrderStatus == "unverified");
            Assert.Contains(waistCandidates, candidate =>
                candidate.SourceShapeName == "ArmorOverlay" &&
                candidate.VertexCount == 3 &&
                candidate.ShapeIdentityStatus == "unresolved" &&
                candidate.VertexOrderStatus == "unverified");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MatchingLinkedNifDoesNotByItselfVerifyTriMorphCorrespondence()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var meshDirectory = Path.Combine(root, "meshes", "armor", "traveler");
            var projectDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "SliderSets");
            var shapeDataDirectory = Path.Combine(root, "CalienteTools", "BodySlide", "ShapeData", "TravelerProject");
            Directory.CreateDirectory(meshDirectory);
            Directory.CreateDirectory(projectDirectory);
            Directory.CreateDirectory(shapeDataDirectory);

            var mesh = Path.Combine(meshDirectory, "traveler_0.nif");
            var project = Path.Combine(projectDirectory, "traveler.osp");
            var referenceNif = SkyrimSseNifShapeReaderTests.CreateNifForShapeTargets("Torso");
            await File.WriteAllBytesAsync(mesh, referenceNif);
            await File.WriteAllBytesAsync(Path.Combine(shapeDataDirectory, "source.nif"), referenceNif);
            await File.WriteAllBytesAsync(
                Path.Combine(shapeDataDirectory, "traveler.tri"),
                BodyTri(("Torso", "Waist", (ushort)0, (short)2)));
            await File.WriteAllTextAsync(project, """
                <SliderSetInfo>
                  <SliderSet name="TravelerProject">
                    <DataFolder>TravelerProject</DataFolder>
                    <SourceFile>source.nif</SourceFile>
                    <OutputPath>meshes\armor\traveler\</OutputPath>
                    <OutputFile>traveler_0.nif</OutputFile>
                    <Slider name="Waist" />
                  </SliderSet>
                </SliderSetInfo>
                """);

            var result = await BodySlideSourceProjectSupport.ResolveAsync(
                new ImportedArmor(mesh, [mesh], [], [], [project]), "CBBE", CancellationToken.None);

            var candidate = Assert.Single(result.ReusableMorphPayloads!["Waist"].Payloads!);
            Assert.Equal("Torso", candidate.SourceShapeName);
            Assert.Equal("unresolved", candidate.ShapeIdentityStatus);
            Assert.Equal("unverified", candidate.VertexOrderStatus);
            Assert.Null(candidate.TargetShapeName);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static byte[] BodyTri(params (string Shape, string Morph, ushort Index, short X)[] shapes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write("PIRT"u8);
        writer.Write((ushort)shapes.Length);
        foreach (var (shape, morph, index, x) in shapes)
        {
            var shapeBytes = Encoding.UTF8.GetBytes(shape);
            var morphBytes = Encoding.UTF8.GetBytes(morph);
            writer.Write((byte)shapeBytes.Length);
            writer.Write(shapeBytes);
            writer.Write((ushort)1);
            writer.Write((byte)morphBytes.Length);
            writer.Write(morphBytes);
            writer.Write(1f);
            writer.Write((ushort)1);
            writer.Write(index);
            writer.Write(x);
            writer.Write((short)0);
            writer.Write((short)0);
        }
        return stream.ToArray();
    }

    private static string OspWithDataTarget(string target) => OspWithDataTargets(target);

    private static string OspWithMultipleSourceNifs() => """
        <SliderSetInfo>
          <SliderSet name="TravelerBase">
            <DataFolder>TravelerProject</DataFolder>
            <SourceFile>base.nif</SourceFile>
            <OutputPath>meshes\armor\traveler\</OutputPath>
            <OutputFile>traveler_0.nif</OutputFile>
            <Slider name="Waist"><Data target="Torso">shared.osd#Waist</Data></Slider>
          </SliderSet>
          <SliderSet name="TravelerAlternate">
            <DataFolder>TravelerProject</DataFolder>
            <SourceFile>alternate.nif</SourceFile>
            <OutputPath>meshes\armor\traveler\</OutputPath>
            <OutputFile>traveler_0.nif</OutputFile>
            <Slider name="Waist"><Data target="Torso">shared.osd#Waist</Data></Slider>
          </SliderSet>
        </SliderSetInfo>
        """;

    private static string OspWithDataTargets(params string[] targets) => $"""
        <SliderSetInfo>
          <SliderSet name="TravelerProject">
            <DataFolder>TravelerProject</DataFolder>
            <SourceFile>base.nif</SourceFile>
            <OutputPath>meshes\armor\traveler\</OutputPath>
            <OutputFile>traveler_0.nif</OutputFile>
            <Slider name="Waist">{string.Concat(targets.Select(static target => $"<Data target=\"{target}\">shared.osd#Waist</Data>"))}</Slider>
          </SliderSet>
        </SliderSetInfo>
        """;

    private static byte[] OsdMorph(string name, int index = 0)
    {
        return OsdMorphs([name], index);
    }

    private static byte[] OsdMorphs(string firstName, string secondName) =>
        OsdMorphs([firstName, secondName]);

    private static byte[] OsdMorphs(IReadOnlyList<string> names, int index = 0)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write("OSD\0"u8);
        writer.Write(1);
        writer.Write(names.Count);
        foreach (var name in names)
        {
            writer.Write((byte)Encoding.UTF8.GetByteCount(name));
            writer.Write(Encoding.UTF8.GetBytes(name));
            writer.Write((ushort)1);
            writer.Write(index);
            writer.Write(0.25f);
            writer.Write(0f);
            writer.Write(0f);
        }
        return stream.ToArray();
    }
}
