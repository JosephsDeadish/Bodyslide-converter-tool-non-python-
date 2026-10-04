using System.Text;

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
}
