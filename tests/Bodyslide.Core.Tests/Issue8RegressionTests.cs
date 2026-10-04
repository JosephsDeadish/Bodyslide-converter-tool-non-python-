using System.Reflection;
using System.IO.Compression;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class Issue8RegressionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnresolvedInstallerFolderOrArchiveStopsBeforeProducingMeshes(bool archive)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(Path.Combine(source, "meshes"));
        await File.WriteAllTextAsync(Path.Combine(source, "meshes", "breastplate_0.nif"), "mesh");
        foreach (var body in new[] { "BHUNP", "3BA", "CBBE", "UNP" })
        {
            var folder = Path.Combine(source, "options", body);
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "BDE_Armor.esp"), "plugin");
        }
        try
        {
            var input = source;
            if (archive)
            {
                input = Path.Combine(root, "source.zip");
                ZipFile.CreateFromDirectory(source, input);
            }
            var runner = new BatchConversionRunner(StandaloneConversionModules.CreateDefault());
            var exception = await Assert.ThrowsAsync<InstallerChoicesRequiredException>(() =>
                runner.ConvertAsync(new ConversionRequest(input, "3BA", output)));
            Assert.Contains("BDE_Armor.esp", exception.Message);
            Assert.Contains("MO2/Vortex", exception.Message);
            Assert.Equal(["BDE_Armor.esp"], exception.PluginNames);
            Assert.Single(exception.Conflicts);
            Assert.Contains("BHUNP", exception.Conflicts[0]);
            Assert.Contains("3BA", exception.Conflicts[0]);
            Assert.Empty(Directory.Exists(output)
                ? Directory.GetFiles(output, "*.nif", SearchOption.AllDirectories) : []);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void AlternativePluginVariantsRequireInstallerSelection()
    {
        var paths = new[] { "BHUNP", "3BA", "CBBE", "UNP" }
            .Select(folder => Path.Combine(Path.GetTempPath(), folder, "BDE_Armor.esp")).ToArray();
        var exception = Assert.Throws<InstallerChoicesRequiredException>(() => PluginSourceIdentity.RequireUnambiguous(paths));
        Assert.Contains("Installer choices", exception.Message);
        Assert.Contains("shared assets", exception.Message);
        Assert.Equal(["BDE_Armor.esp"], exception.PluginNames);
        Assert.Single(exception.Conflicts);
        PluginSourceIdentity.RequireUnambiguous([paths[0], paths[0], Path.Combine(Path.GetTempPath(), "Shared.esm")]);
    }

    [Fact]
    public void InstallerConflictDetailsAreImmutableSnapshots()
    {
        var names = new[] { "Armor.esp" };
        var conflicts = new[] { "BHUNP and 3BA alternatives" };
        var exception = new InstallerChoicesRequiredException(names, conflicts);
        names[0] = "changed.esp";
        conflicts[0] = "changed";
        Assert.Equal("Armor.esp", exception.PluginNames[0]);
        Assert.Equal("BHUNP and 3BA alternatives", exception.Conflicts[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)exception.PluginNames)[0] = "changed");
    }

    [Fact]
    public async Task SelectedInstalledVariantCanBeConvertedWithoutCombiningAlternatives()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var installed = Path.Combine(root, "installed-BHUNP");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(Path.Combine(installed, "meshes"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(installed, "meshes", "breastplate_0.nif"), "mesh");
            await File.WriteAllTextAsync(Path.Combine(installed, "BDE_Armor.esp"), "selected plugin");
            var alternative = Path.Combine(root, "uninstalled-3BA");
            Directory.CreateDirectory(alternative);
            await File.WriteAllTextAsync(Path.Combine(alternative, "BDE_Armor.esp"), "alternative plugin");
            var imported = await new LocalArmorImportService().ImportAsync(installed, CancellationToken.None);
            Assert.Equal([Path.Combine(installed, "BDE_Armor.esp")], imported.SourcePluginFiles);
            var results = await new BatchConversionRunner(StandaloneConversionModules.CreateDefault())
                .ConvertAsync(new ConversionRequest(installed, "3BA", output, SourceBodyOverride: "BHUNP"));
            Assert.Single(results);
            Assert.True(results[0].Success);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ConversionResolutionIsScopedAndSharedAcrossBodySlidePreparation()
    {
        var armor = new ImportedArmor("armor.nif", [], [], [], []);
        var scoped = BodySlideSourceProjectSupport.WithConversionResolution(armor, "3BA", CancellationToken.None);
        var first = await BodySlideSourceProjectSupport.ResolveAsync(scoped, "3BA", CancellationToken.None);
        var second = await BodySlideSourceProjectSupport.ResolveAsync(scoped with { BodySlideProjectNamespace = "piece" },
            "3BA", CancellationToken.None);
        Assert.Same(first, second);
        var otherBody = await BodySlideSourceProjectSupport.ResolveAsync(scoped, "HIMBO", CancellationToken.None);
        Assert.NotSame(first, otherBody);
        var next = await BodySlideSourceProjectSupport.ResolveAsync(
            BodySlideSourceProjectSupport.WithConversionResolution(armor, "3BA", CancellationToken.None),
            "3BA", CancellationToken.None);
        Assert.NotSame(first, next);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await BodySlideSourceProjectSupport.ResolveAsync(scoped, "3BA", cancellation.Token));
    }

    [Theory]
    [InlineData("3BA")]
    [InlineData("BHUNP")]
    public void BreastplateGenericCuesDoNotSelectCreatureProfile(string body)
    {
        Assert.False(SemanticAnchorCatalog.TryResolveBestProfile(body,
            ["BDE", "Armor", "breastplate", "NPC L Breast01", "NPC R Breast01", "NPC Spine", "L", "R", "a"],
            ["breasts", "chest"], out _, out _));
    }

    [Fact]
    public void ExplicitSprigganEvidenceStillSelectsSpriggan()
    {
        Assert.True(SemanticAnchorCatalog.TryResolveBestProfile("CUSTOM",
            ["meshes/armor/spriggan/branch_0.nif"], ["branches"], out var profile, out _));
        Assert.Equal("Spriggan", profile.Name);
    }

    [Fact]
    public void CatalogBonesAreNotObservedMeshEvidence()
    {
        var method = typeof(LocalExportService).GetMethod("BuildSemanticAnchorObservedTokens",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var armor = new ImportedArmor("breastplate.nif", ["breastplate.nif"], [], [], []);
        var tokens = (IReadOnlySet<string>)method.Invoke(null, [armor, "UBE", null])!;
        Assert.DoesNotContain("HDT TongueTip", tokens);
        Assert.DoesNotContain("VaginaDeep", tokens);
        Assert.DoesNotContain("BreastUpper", tokens);
    }
}
