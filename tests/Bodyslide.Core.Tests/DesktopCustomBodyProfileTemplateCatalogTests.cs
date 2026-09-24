namespace Bodyslide.Core.Tests;

public sealed class DesktopCustomBodyProfileTemplateCatalogTests
{
    [Fact]
    public void Resolve_UsesBuiltInMetadataForSamLight()
    {
        var template = DesktopCustomBodyProfileTemplateCatalog.Resolve("SAM Light");

        Assert.Equal("SAM Light", template.Name);
        Assert.Equal("male", template.Gender);
        Assert.Equal(@"meshes\actors\character\character assets male\", template.BodyOutputPath);
        Assert.Contains("Belly", template.SliderNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Thighs", template.SliderNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("belly", template.TransformationField.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("thighs", template.TransformationField.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(template.DetectionTokens);
    }

    [Fact]
    public void Resolve_UsesBuiltInMetadataForVanillaBeast()
    {
        var template = DesktopCustomBodyProfileTemplateCatalog.Resolve("Vanilla Beast");

        Assert.Equal("Vanilla Beast", template.Name);
        Assert.Equal("female", template.Gender);
        Assert.Equal(@"meshes\actors\character\character assets\", template.BodyOutputPath);
        Assert.Contains("TailBase", template.SliderNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Thighs", template.SliderNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("legs", template.TransformationField.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(template.DetectionTokens);
    }

    [Fact]
    public void Resolve_FallsBackForUnknownBody()
    {
        var template = DesktopCustomBodyProfileTemplateCatalog.Resolve("My Custom Body");

        Assert.Equal("My Custom Body", template.Name);
        Assert.Equal("female", template.Gender);
        Assert.Equal(@"meshes\actors\character\character assets\", template.BodyOutputPath);
        Assert.Equal(["My Custom Body"], template.DetectionTokens);
        Assert.Contains("Belly", template.SliderNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("butt", template.TransformationField.Keys, StringComparer.OrdinalIgnoreCase);
    }
}
