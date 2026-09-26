namespace Bodyslide.Core;

public sealed record DesktopCustomBodyProfileTemplate(
    string Name,
    IReadOnlyList<string> DetectionTokens,
    int VertexCountMin,
    int VertexCountMax,
    IReadOnlyDictionary<string, double> TransformationField,
    IReadOnlyList<string> SliderNames,
    string BodyOutputPath,
    string Gender);

public static class DesktopCustomBodyProfileTemplateCatalog
{
    private static readonly string[] FallbackSliderNames =
    [
        "Belly",
        "Butt",
        "BreastsShape",
        "WaistWidth",
        "HipWidth"
    ];

    public static DesktopCustomBodyProfileTemplate Resolve(string? targetBody)
    {
        var canonicalName = BodyTypeCatalog.ResolveName(targetBody);
        if (string.IsNullOrWhiteSpace(canonicalName))
        {
            canonicalName = "CUSTOM";
        }

        BodyTypeCatalog.TryResolve(canonicalName, out var bodyInfo);
        var hasMetadata = BuiltInBodyMetadataCatalog.TryGet(canonicalName, out var metadata);
        var detectionTokens = bodyInfo?.DetectionTokens.Count > 0
            ? bodyInfo.DetectionTokens
            : [canonicalName];
        var transformationField = hasMetadata
            ? metadata.TransformationField
            : BuiltInBodyMetadataCatalog.CreateFallbackTransformationField();
        var sliderNames = hasMetadata && metadata.SliderNames.Count > 0
            ? metadata.SliderNames
            : FallbackSliderNames;
        var gender = BodyTypeCatalog.TryGetGender(canonicalName, out var resolvedGender)
            ? resolvedGender
            : "female";

        return new DesktopCustomBodyProfileTemplate(
            canonicalName,
            detectionTokens,
            bodyInfo?.VertexCountMin ?? 0,
            bodyInfo?.VertexCountMax ?? 0,
            transformationField.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            sliderNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            string.Equals(gender, "male", StringComparison.OrdinalIgnoreCase)
                ? @"meshes\actors\character\character assets male\"
                : @"meshes\actors\character\character assets\",
            gender);
    }
}
