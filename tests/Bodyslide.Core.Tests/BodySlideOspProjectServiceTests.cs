using System.Text;
using System.Xml.Linq;

namespace Bodyslide.Core.Tests;

public sealed class BodySlideOspProjectServiceTests
{
    [Fact]
    public void NormalizeSliderNames_RemovesControlChars_WhitespaceNoise_AndDuplicates()
    {
        var normalized = BodySlideOspProjectService.NormalizeSliderNames(
            [
                "  Bust  ",
                "Bust",
                "Waist\tSize",
                "Waist   Size",
                "Hip\u0000Up",
                " ",
                "\n"
            ]);

        Assert.Equal(3, normalized.Count);
        Assert.Contains("Bust", normalized);
        Assert.Contains("Waist Size", normalized);
        Assert.Contains("HipUp", normalized);
    }

    [Fact]
    public void NormalizeSliderNames_TruncatesUtf8NamesToBodySlideSafeLength()
    {
        var longName = new string('Å', 200);

        var normalized = BodySlideOspProjectService.NormalizeSliderNames([longName]);

        Assert.Single(normalized);
        var byteCount = Encoding.UTF8.GetByteCount(normalized[0]);
        Assert.True(byteCount <= 255, $"Expected <=255 UTF-8 bytes, got {byteCount}.");
        Assert.NotEmpty(normalized[0]);
    }

    [Theory]
    [InlineData(false, "default", "0.25")]
    [InlineData(true, "small", "0.1")]
    public void BuildOspXmlPreservesSupportedSliderControlSettings(bool generateWeights, string expectedAttribute, string expectedValue)
    {
        var target = new BodySlideMeshTarget(
            "Project", "Project", "source.nif", "meshes\\armor\\", "output_0.nif",
            generateWeights ? "output_1.nif" : null);
        var settings = new Dictionary<string, OspSliderControlSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["Fit"] = new(0.25, 0.1, 0.8, true)
        };

        var document = XDocument.Parse(BodySlideOspProjectService.BuildOspXml(
            ["Fit"], [], [target], "female", settings));
        var slider = Assert.Single(document.Descendants("Slider"));

        Assert.Equal(expectedValue, (string?)slider.Attribute(expectedAttribute));
        if (generateWeights)
        {
            Assert.Equal("0.8", (string?)slider.Attribute("big"));
        }
        Assert.Equal("true", (string?)slider.Attribute("invert"));
    }
}
