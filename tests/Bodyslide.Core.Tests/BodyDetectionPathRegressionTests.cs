namespace Bodyslide.Core.Tests;

public sealed class BodyDetectionPathRegressionTests
{
    [Theory]
    [InlineData("C:\\neutral\\cbbe0000000000000000000000000000\\outfit_0.nif")]
    [InlineData("C:/neutral/cbbe0000000000000000000000000000/outfit_0.nif")]
    [InlineData("C:\\neutral\\cbbe0000000000000000000000000000.nif")]
    [InlineData("C:/neutral/cbbe0000000000000000000000000000.nif")]
    public async Task ForeignPathSeparatorsDoNotLeakOpaqueIdentifiers(string mesh)
    {
        var armor = new ImportedArmor(mesh, [mesh], [], [], []);

        var detection = await new SignatureBodyDetectionService().DetectAsync(armor, CancellationToken.None);
        var fallback = BodySlideSourceProjectSupport.InferFallbackSupport(armor, "MyFollower");

        Assert.Equal("CUSTOM", detection.Body);
        Assert.Null(fallback?.BodyName);
    }

    [Theory]
    [InlineData("3ba00000000000000000000000000000")]
    [InlineData("cbbe0000000000000000000000000000")]
    [InlineData("3ba00000-0000-0000-0000-000000000000")]
    [InlineData("cbbe0000-0000-0000-0000-000000000000")]
    [InlineData("{cbbe0000-0000-0000-0000-000000000000}")]
    public async Task OpaqueWorkspaceIdentifiersDoNotOverrideExplicitBodySlideSignals(string identifier)
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-body-path", Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, identifier);
        Directory.CreateDirectory(workspace);
        try
        {
            var mesh = Path.Combine(workspace, "outfit_0.nif");
            var groups = Path.Combine(workspace, "group.xml");
            await File.WriteAllTextAsync(mesh, "mesh");
            await File.WriteAllTextAsync(groups,
                "<SliderGroup name=\"UUNP Outfits\"><Member name=\"UUNP Slim Variant\" /></SliderGroup>");

            var result = await new SignatureBodyDetectionService().DetectAsync(
                new ImportedArmor(mesh, [mesh], [], [], [groups]), CancellationToken.None);

            Assert.Contains(result.Body, new[] { "UUNP", "UNP" }, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

    }

    [Theory]
    [InlineData("cbbe0000000000000000000000000000", null)]
    [InlineData("cbbe0000-0000-0000-0000-000000000000", null)]
    [InlineData("3ba00000000000000000000000000000", null)]
    [InlineData("CBBE", "CBBE")]
    public void FallbackBodySlideInferenceIgnoresOpaquePathEvidence(string directoryName, string? expectedBody)
    {
        var mesh = Path.Combine(Path.GetTempPath(), "slidesmith-body-path", directoryName, "outfit_0.nif");

        var inference = BodySlideSourceProjectSupport.InferFallbackSupport(
            new ImportedArmor(mesh, [mesh], [], [], []), "MyFollower");

        if (expectedBody is null)
        {
            Assert.Null(inference?.BodyName);
        }
        else
        {
            Assert.NotNull(inference?.BodyName);
            Assert.Contains(expectedBody, inference.BodyName, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("cbbe0000000000000000000000000000", "CUSTOM")]
    [InlineData("cbbe0000-0000-0000-0000-000000000000", "CUSTOM")]
    [InlineData("CBBE", "CBBE")]
    [InlineData("CBBE Curvy Outfit", "CBBE")]
    public async Task DirectoryInputsIgnoreOpaqueLeafNamesButRetainMeaningfulBodyNames(string directoryName, string expectedBody)
    {
        var root = Path.Combine(Path.GetTempPath(), "slidesmith-body-path", Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, directoryName);
        Directory.CreateDirectory(workspace);
        try
        {
            var mesh = Path.Combine(workspace, "outfit_0.nif");
            await File.WriteAllTextAsync(mesh, "mesh");

            var result = await new SignatureBodyDetectionService().DetectAsync(
                new ImportedArmor(workspace, [mesh], [], [], []), CancellationToken.None);

            Assert.Equal(expectedBody, result.Body);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
