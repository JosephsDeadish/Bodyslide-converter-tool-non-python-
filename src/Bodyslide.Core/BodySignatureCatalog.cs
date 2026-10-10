namespace Bodyslide.Core;

internal sealed record BodySignatureTemplate(
    string Body,
    IReadOnlyList<string> MeshTokens,
    IReadOnlyList<string> TextureTokens,
    IReadOnlyList<string> PhysicsTokens,
    int VertexCountMin = 0,
    int VertexCountMax = 0,
    double HeightToWidthRatioMin = 0,
    double HeightToWidthRatioMax = 0,
    double DepthToWidthRatioMin = 0,
    double DepthToWidthRatioMax = 0,
    IReadOnlyList<string>? ReferenceTokens = null);

internal static class VanillaBodySignatureDatabase
{
    private static readonly Lazy<IReadOnlyList<BodySignatureTemplate>> TemplatesValue = new(() =>
        BuiltInBodyMetadataCatalog.All
            .Select(static metadata => metadata.ToSignatureTemplate())
            .OrderBy(static template => template.Body, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    public static IReadOnlyList<BodySignatureTemplate> Templates => TemplatesValue.Value;
}

internal static class ReferenceBodySignatureDatabase
{
    public static IReadOnlyList<string> GetTokens(string bodyName) =>
        BuiltInBodyMetadataCatalog.TryGet(bodyName, out var metadata)
            ? metadata.ReferenceTokens
            : [];
}
