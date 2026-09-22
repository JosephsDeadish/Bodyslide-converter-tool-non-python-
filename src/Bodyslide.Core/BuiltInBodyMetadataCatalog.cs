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
    string SkeletonFramework,
    IReadOnlyList<string> ExpectedSemanticRegions,
    IReadOnlyList<string> ExpectedCollisionRegions,
    IReadOnlyList<string> ExpectedBilateralRegions,
    int MinimumPhysicsSlotCount,
    int MinimumPhysicsChainDepth,
    int MinimumPhysicsFamilyCount,
    string CollisionComplexity,
    bool HasExplicitExpectedSemanticRegions,
    bool HasExplicitExpectedCollisionRegions,
    bool HasExplicitExpectedBilateralRegions,
    bool HasExplicitMinimumPhysicsSlotCount,
    bool HasExplicitMinimumPhysicsChainDepth,
    bool HasExplicitMinimumPhysicsFamilyCount,
    bool HasExplicitCollisionComplexity)
{
    public bool HasExplicitSupportMetadata =>
        HasExplicitExpectedSemanticRegions &&
        HasExplicitExpectedCollisionRegions &&
        HasExplicitMinimumPhysicsSlotCount &&
        HasExplicitMinimumPhysicsChainDepth &&
        HasExplicitCollisionComplexity;

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
            string.IsNullOrWhiteSpace(dto.SkeletonFramework) ? "xpmsse" : dto.SkeletonFramework.Trim(),
            BodySupportMetadataHeuristics.NormalizeSupportRegionList(dto.ExpectedSemanticRegions) is { Count: > 0 } expectedSemanticRegions
                ? expectedSemanticRegions
                : BodySupportMetadataHeuristics.InferExpectedSemanticRegions(dto.SliderNames, dto.AvailablePhysicsBones, dto.TransformationField?.Keys),
            BodySupportMetadataHeuristics.NormalizeSupportRegionList(dto.ExpectedCollisionRegions) is { Count: > 0 } expectedCollisionRegions
                ? expectedCollisionRegions
                : BodySupportMetadataHeuristics.InferExpectedCollisionRegions(dto.AvailablePhysicsBones, dto.SliderNames, dto.PhysicsBoneSignatures),
            BodySupportMetadataHeuristics.NormalizeSupportRegionList(dto.ExpectedBilateralRegions) is { Count: > 0 } expectedBilateralRegions
                ? expectedBilateralRegions
                : BodySupportMetadataHeuristics.InferExpectedBilateralRegions(dto.AvailablePhysicsBones, dto.SliderNames, dto.ExpectedSemanticRegions, dto.ExpectedCollisionRegions),
            dto.MinimumPhysicsSlotCount > 0
                ? dto.MinimumPhysicsSlotCount
                : BodySupportMetadataHeuristics.CountPhysicsSlots(dto.AvailablePhysicsBones),
            dto.MinimumPhysicsChainDepth > 0
                ? dto.MinimumPhysicsChainDepth
                : BodySupportMetadataHeuristics.EstimatePhysicsChainDepth(dto.AvailablePhysicsBones, dto.PhysicsBoneSignatures),
            dto.MinimumPhysicsFamilyCount > 0
                ? dto.MinimumPhysicsFamilyCount
                : BodySupportMetadataHeuristics.CountPhysicsFamilies(dto.AvailablePhysicsBones, dto.PhysicsBoneSignatures),
            string.IsNullOrWhiteSpace(dto.CollisionComplexity)
                ? BodySupportMetadataHeuristics.InferCollisionComplexity(dto.AvailablePhysicsBones, dto.SliderNames, dto.PhysicsBoneSignatures)
                : dto.CollisionComplexity.Trim(),
            dto.ExpectedSemanticRegions is { Length: > 0 },
            dto.ExpectedCollisionRegions is { Length: > 0 },
            dto.ExpectedBilateralRegions is { Length: > 0 },
            dto.MinimumPhysicsSlotCount > 0,
            dto.MinimumPhysicsChainDepth > 0,
            dto.MinimumPhysicsFamilyCount > 0,
            !string.IsNullOrWhiteSpace(dto.CollisionComplexity));
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
        public string[]? ExpectedSemanticRegions { get; init; }
        public string[]? ExpectedCollisionRegions { get; init; }
        public string[]? ExpectedBilateralRegions { get; init; }
        public int MinimumPhysicsSlotCount { get; init; }
        public int MinimumPhysicsChainDepth { get; init; }
        public int MinimumPhysicsFamilyCount { get; init; }
        public string? CollisionComplexity { get; init; }
    }
}

internal static class BodySupportMetadataHeuristics
{
    public static IReadOnlyList<string> NormalizeSupportRegionList(IEnumerable<string>? values) =>
        values?
            .Select(NormalizeSupportRegion)
            .Where(static region => !string.IsNullOrWhiteSpace(region))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static region => region, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    public static IReadOnlyList<string> InferExpectedSemanticRegions(
        IEnumerable<string>? sliderNames,
        IEnumerable<string>? physicsBones,
        IEnumerable<string>? transformRegions = null)
    {
        return NormalizeSupportRegionList((sliderNames ?? [])
            .Concat(physicsBones ?? [])
            .Concat(transformRegions ?? []));
    }

    public static IReadOnlyList<string> InferExpectedCollisionRegions(
        IEnumerable<string>? physicsBones,
        IEnumerable<string>? sliderNames,
        IEnumerable<string>? physicsBoneSignatures = null)
    {
        var physicsRegions = NormalizeSupportRegionList((physicsBones ?? []).Concat(physicsBoneSignatures ?? []));
        if (physicsRegions.Count > 0)
        {
            return physicsRegions;
        }

        return NormalizeSupportRegionList(sliderNames);
    }

    public static int CountPhysicsSlots(IEnumerable<string>? physicsBones)
    {
        var slots = NormalizeSupportRegionList(physicsBones);
        return Math.Max(slots.Count, 0);
    }

    public static int CountPhysicsFamilies(IEnumerable<string>? physicsBones, IEnumerable<string>? physicsBoneSignatures = null)
    {
        return NormalizeSupportRegionList((physicsBones ?? []).Concat(physicsBoneSignatures ?? []))
            .Count;
    }

    public static int EstimatePhysicsChainDepth(IEnumerable<string>? physicsBones, IEnumerable<string>? physicsBoneSignatures = null)
    {
        var tokens = (physicsBones ?? [])
            .Concat(physicsBoneSignatures ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (tokens.Length == 0)
        {
            return 0;
        }

        var depth = 1;
        foreach (var token in tokens)
        {
            var lower = token.Trim().ToLowerInvariant();
            if (lower.Contains("01", StringComparison.Ordinal) ||
                lower.Contains("mid", StringComparison.Ordinal) ||
                lower.Contains("upper", StringComparison.Ordinal) ||
                lower.Contains("root", StringComparison.Ordinal) ||
                lower.Contains("base", StringComparison.Ordinal))
            {
                depth = Math.Max(depth, 2);
            }

            if (lower.Contains("02", StringComparison.Ordinal) ||
                lower.Contains("03", StringComparison.Ordinal) ||
                lower.Contains("tip", StringComparison.Ordinal) ||
                lower.Contains("lower", StringComparison.Ordinal) ||
                lower.Contains("outer", StringComparison.Ordinal) ||
                lower.Contains("inner", StringComparison.Ordinal))
            {
                depth = Math.Max(depth, 3);
            }
        }

        return depth;
    }

    public static string InferCollisionComplexity(
        IEnumerable<string>? physicsBones,
        IEnumerable<string>? sliderNames,
        IEnumerable<string>? physicsBoneSignatures = null)
    {
        var collisionRegions = InferExpectedCollisionRegions(physicsBones, sliderNames, physicsBoneSignatures);
        var chainDepth = EstimatePhysicsChainDepth(physicsBones, physicsBoneSignatures);
        var slotCount = CountPhysicsSlots(physicsBones);
        if (collisionRegions.Count >= 6 || slotCount >= 6 || chainDepth >= 3)
        {
            return "extended";
        }

        if (collisionRegions.Count >= 3 || slotCount >= 3 || chainDepth >= 2)
        {
            return "standard";
        }

        return collisionRegions.Count > 0 || slotCount > 0 ? "minimal" : "none";
    }

    public static IReadOnlyList<string> InferExpectedBilateralRegions(
        IEnumerable<string>? physicsBones,
        IEnumerable<string>? sliderNames,
        IEnumerable<string>? semanticRegions = null,
        IEnumerable<string>? collisionRegions = null)
    {
        return NormalizeSupportRegionList((physicsBones ?? [])
                .Concat(sliderNames ?? [])
                .Concat(semanticRegions ?? [])
                .Concat(collisionRegions ?? []))
            .Where(static region => region is "breasts" or "butt" or "arms" or "feet" or "wing" or "fin" or "frill" or "horn" or "antenna" or "mandible")
            .ToArray();
    }

    public static string NormalizeSupportRegion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var v = value.Trim();
        if (CanonicalSupportRegions.TryGetValue(v, out var canonicalRegion))
        {
            return canonicalRegion;
        }

        return v switch
        {
            var text when text.Contains("breast", StringComparison.OrdinalIgnoreCase) || text.Contains("pec", StringComparison.OrdinalIgnoreCase) => "breasts",
            var text when text.Contains("belly", StringComparison.OrdinalIgnoreCase) || text.Contains("abdomen", StringComparison.OrdinalIgnoreCase) || text.Contains("waist", StringComparison.OrdinalIgnoreCase) => "belly",
            var text when text.Contains("butt", StringComparison.OrdinalIgnoreCase) || text.Contains("glute", StringComparison.OrdinalIgnoreCase) || text.Contains("hip", StringComparison.OrdinalIgnoreCase) => "butt",
            var text when text.Contains("thigh", StringComparison.OrdinalIgnoreCase) || text.Contains("upperleg", StringComparison.OrdinalIgnoreCase) => "thighs",
            var text when text.Contains("calf", StringComparison.OrdinalIgnoreCase) => "calves",
            var text when text.Contains("pelvis", StringComparison.OrdinalIgnoreCase) => "pelvis",
            var text when text.Contains("chest", StringComparison.OrdinalIgnoreCase) => "chest",
            var text when text.Contains("shoulder", StringComparison.OrdinalIgnoreCase) || text.Contains("clavicle", StringComparison.OrdinalIgnoreCase) => "shoulders",
            var text when text.Contains("arm", StringComparison.OrdinalIgnoreCase) && !text.Contains("arma", StringComparison.OrdinalIgnoreCase) => "arms",
            var text when text.Contains("jaw", StringComparison.OrdinalIgnoreCase) => "jaw",
            var text when text.Contains("tongue", StringComparison.OrdinalIgnoreCase) => "tongue",
            var text when text.Contains("throat", StringComparison.OrdinalIgnoreCase) => "throat",
            var text when text.Contains("mouth", StringComparison.OrdinalIgnoreCase) => "mouth",
            var text when text.Contains("vagina", StringComparison.OrdinalIgnoreCase) || text.Contains("labia", StringComparison.OrdinalIgnoreCase) => "vagina",
            var text when text.Contains("anus", StringComparison.OrdinalIgnoreCase) => "anus",
            var text when text.Contains("genital", StringComparison.OrdinalIgnoreCase) || text.Contains("shaft", StringComparison.OrdinalIgnoreCase) || text.Contains("glans", StringComparison.OrdinalIgnoreCase) || text.Contains("foreskin", StringComparison.OrdinalIgnoreCase) || text.Contains("sheath", StringComparison.OrdinalIgnoreCase) || text.Contains("scrot", StringComparison.OrdinalIgnoreCase) || text.Contains("knot", StringComparison.OrdinalIgnoreCase) || text.Contains("balls", StringComparison.OrdinalIgnoreCase) => "genitals",
            var text when text.Contains("tail", StringComparison.OrdinalIgnoreCase) => "tail",
            var text when text.Contains("paw", StringComparison.OrdinalIgnoreCase) || text.Contains("hoof", StringComparison.OrdinalIgnoreCase) || text.Contains("hock", StringComparison.OrdinalIgnoreCase) || text.Contains("foot", StringComparison.OrdinalIgnoreCase) || text.Contains("talon", StringComparison.OrdinalIgnoreCase) => "feet",
            var text when text.Contains("wing", StringComparison.OrdinalIgnoreCase) || text.Contains("feather", StringComparison.OrdinalIgnoreCase) => "wing",
            var text when text.Contains("fin", StringComparison.OrdinalIgnoreCase) => "fin",
            var text when text.Contains("frill", StringComparison.OrdinalIgnoreCase) => "frill",
            var text when text.Contains("antenna", StringComparison.OrdinalIgnoreCase) || text.Contains("feeler", StringComparison.OrdinalIgnoreCase) => "antenna",
            var text when text.Contains("mandible", StringComparison.OrdinalIgnoreCase) => "mandible",
            var text when text.Contains("horn", StringComparison.OrdinalIgnoreCase) => "horn",
            var text when text.Contains("branch", StringComparison.OrdinalIgnoreCase) || text.Contains("vine", StringComparison.OrdinalIgnoreCase) || text.Contains("tendril", StringComparison.OrdinalIgnoreCase) => "branch",
            var text when text.Contains("mane", StringComparison.OrdinalIgnoreCase) || text.Contains("forelock", StringComparison.OrdinalIgnoreCase) => "mane",
            _ => string.Empty
        };
    }

    private static readonly IReadOnlyDictionary<string, string> CanonicalSupportRegions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["breast"] = "breasts",
            ["breasts"] = "breasts",
            ["belly"] = "belly",
            ["butt"] = "butt",
            ["thigh"] = "thighs",
            ["thighs"] = "thighs",
            ["calf"] = "calves",
            ["calves"] = "calves",
            ["pelvis"] = "pelvis",
            ["chest"] = "chest",
            ["shoulder"] = "shoulders",
            ["shoulders"] = "shoulders",
            ["arm"] = "arms",
            ["arms"] = "arms",
            ["jaw"] = "jaw",
            ["tongue"] = "tongue",
            ["throat"] = "throat",
            ["mouth"] = "mouth",
            ["vagina"] = "vagina",
            ["anus"] = "anus",
            ["genital"] = "genitals",
            ["genitals"] = "genitals",
            ["tail"] = "tail",
            ["foot"] = "feet",
            ["feet"] = "feet",
            ["wing"] = "wing",
            ["fin"] = "fin",
            ["frill"] = "frill",
            ["antenna"] = "antenna",
            ["mandible"] = "mandible",
            ["horn"] = "horn",
            ["branch"] = "branch",
            ["mane"] = "mane"
        };

    private static IReadOnlyList<string>? NullIfEmpty(this IReadOnlyList<string> values) =>
        values.Count == 0 ? null : values;
}
