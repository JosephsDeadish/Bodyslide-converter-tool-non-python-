using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record BuiltInBodyMetadata(
    string Name,
    IReadOnlyList<string> Aliases,
    string Gender,
    string DefaultPhysics,
    string SkeletonFoundation,
    string Notes,
    IReadOnlyList<string> AvailablePhysicsBones,
    IReadOnlyList<string> ReferenceTokens,
    IReadOnlyList<string> SliderNames,
    IReadOnlyDictionary<string, double> TransformationField,
    IReadOnlyList<string> DetectionTokens,
    IReadOnlyList<string> TextureTokens,
    IReadOnlyList<string> PhysicsTokens,
    int VertexCountMin,
    int VertexCountMax,
    double HeightToWidthRatioMin,
    double HeightToWidthRatioMax,
    double DepthToWidthRatioMin,
    double DepthToWidthRatioMax,
    IReadOnlyList<string> PhysicsBoneSignatures,
    string SkeletonFramework)
{
    public BodySignatureTemplate ToSignatureTemplate() =>
        new(
            Name,
            DetectionTokens,
            TextureTokens,
            PhysicsTokens,
            VertexCountMin,
            VertexCountMax,
            HeightToWidthRatioMin,
            HeightToWidthRatioMax,
            DepthToWidthRatioMin,
            DepthToWidthRatioMax,
            ReferenceTokens);
}

internal static class BuiltInBodyMetadataCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.built-in-bodies.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly IReadOnlyDictionary<string, double> FallbackTransformationField =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["chest"] = 1.02,
            ["waist"] = 0.99,
            ["pelvis"] = 1.02,
            ["legs"] = 1.01,
            ["shoulders"] = 1.00,
            ["breasts"] = 1.02,
            ["butt"] = 1.01,
            ["belly"] = 1.01,
            ["arms"] = 1.00,
            ["thighs"] = 1.01,
            ["calves"] = 1.01,
        };

    private static readonly IReadOnlyDictionary<string, string> RegionAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["hips"] = "pelvis",
            ["hip"] = "pelvis",
            ["torso"] = "chest",
            ["abdomen"] = "belly",
            ["glutes"] = "butt",
            ["leg"] = "legs",
            ["thigh"] = "thighs",
            ["calf"] = "calves",
            ["arm"] = "arms",
            ["shoulder"] = "shoulders",
            ["breast"] = "breasts"
        };

    private static readonly Lazy<IReadOnlyDictionary<string, BuiltInBodyMetadata>> Bodies = new(LoadBodies);
    private static readonly Lazy<IReadOnlyDictionary<string, string>> AliasMap = new(LoadAliasMap);

    public static IReadOnlyCollection<BuiltInBodyMetadata> All => Bodies.Value.Values.ToArray();

    public static bool TryGet(string bodyName, out BuiltInBodyMetadata metadata)
    {
        metadata = default!;
        return TryResolveCanonicalName(bodyName, out var canonicalName) &&
               Bodies.Value.TryGetValue(canonicalName, out metadata!);
    }

    public static bool TryResolveCanonicalName(string? bodyName, out string canonicalName)
    {
        canonicalName = string.Empty;
        if (string.IsNullOrWhiteSpace(bodyName))
        {
            return false;
        }

        var normalized = bodyName.Trim();
        if (Bodies.Value.ContainsKey(normalized))
        {
            canonicalName = normalized;
            return true;
        }

        if (AliasMap.Value.TryGetValue(normalized, out canonicalName!))
        {
            return true;
        }

        return AliasMap.Value.TryGetValue(Slugify(normalized), out canonicalName!);
    }

    public static IReadOnlyDictionary<string, double> CreateFallbackTransformationField() =>
        new Dictionary<string, double>(FallbackTransformationField, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, BuiltInBodyMetadata> LoadBodies()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded body metadata resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dtos = JsonSerializer.Deserialize<List<BuiltInBodyMetadataDto>>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Built-in body metadata could not be deserialized.");

        return dtos
            .Select(Normalize)
            .ToDictionary(static body => body.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> LoadAliasMap()
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var body in Bodies.Value.Values)
        {
            aliases[body.Name] = body.Name;
            aliases[Slugify(body.Name)] = body.Name;
            foreach (var alias in body.Aliases)
            {
                aliases[alias] = body.Name;
                aliases[Slugify(alias)] = body.Name;
            }
        }

        return aliases;
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        var lastWasSeparator = false;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static BuiltInBodyMetadata Normalize(BuiltInBodyMetadataDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new InvalidOperationException("Built-in body metadata contained an entry without a name.");
        }

        var name = dto.Name.Trim();
        var gender = string.Equals(dto.Gender, "male", StringComparison.OrdinalIgnoreCase) ? "male" : "female";
        var physics = string.IsNullOrWhiteSpace(dto.DefaultPhysics) ? "none" : dto.DefaultPhysics.Trim();

        return new BuiltInBodyMetadata(
            name,
            NormalizeStringList(dto.Aliases),
            gender,
            physics,
            string.IsNullOrWhiteSpace(dto.SkeletonFoundation) ? "XPMSSE" : dto.SkeletonFoundation.Trim(),
            dto.Notes?.Trim() ?? string.Empty,
            NormalizeStringList(dto.AvailablePhysicsBones),
            NormalizeStringList(dto.ReferenceTokens),
            NormalizeStringList(dto.SliderNames),
            NormalizeTransformationField(dto.TransformationField),
            NormalizeStringList(dto.DetectionTokens),
            NormalizeStringList(dto.TextureTokens),
            NormalizeStringList(dto.PhysicsTokens),
            Math.Max(0, dto.VertexCountMin),
            Math.Max(dto.VertexCountMin, dto.VertexCountMax),
            dto.HeightToWidthRatioMin,
            dto.HeightToWidthRatioMax,
            dto.DepthToWidthRatioMin,
            dto.DepthToWidthRatioMax,
            NormalizeStringList(dto.PhysicsBoneSignatures),
            string.IsNullOrWhiteSpace(dto.SkeletonFramework) ? "xpmsse" : dto.SkeletonFramework.Trim());
    }

    private static IReadOnlyDictionary<string, double> NormalizeTransformationField(Dictionary<string, double>? rawField)
    {
        var normalized = new Dictionary<string, double>(FallbackTransformationField, StringComparer.OrdinalIgnoreCase);
        if (rawField is null || rawField.Count == 0)
        {
            return normalized;
        }

        foreach (var (key, value) in rawField)
        {
            if (string.IsNullOrWhiteSpace(key) || double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                continue;
            }

            var normalizedKey = RegionAliases.TryGetValue(key.Trim(), out var alias)
                ? alias
                : key.Trim();
            normalized[normalizedKey] = Math.Round(Math.Clamp(value, 0.4, 2.5), 4);
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeStringList(IEnumerable<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private sealed class BuiltInBodyMetadataDto
    {
        public string? Name { get; init; }
        public string[]? Aliases { get; init; }
        public string? Gender { get; init; }
        public string? DefaultPhysics { get; init; }
        public string? SkeletonFoundation { get; init; }
        public string? Notes { get; init; }
        public string[]? AvailablePhysicsBones { get; init; }
        public string[]? ReferenceTokens { get; init; }
        public string[]? SliderNames { get; init; }
        public Dictionary<string, double>? TransformationField { get; init; }
        public string[]? DetectionTokens { get; init; }
        public string[]? TextureTokens { get; init; }
        public string[]? PhysicsTokens { get; init; }
        public int VertexCountMin { get; init; }
        public int VertexCountMax { get; init; }
        public double HeightToWidthRatioMin { get; init; }
        public double HeightToWidthRatioMax { get; init; }
        public double DepthToWidthRatioMin { get; init; }
        public double DepthToWidthRatioMax { get; init; }
        public string[]? PhysicsBoneSignatures { get; init; }
        public string? SkeletonFramework { get; init; }
    }
}
