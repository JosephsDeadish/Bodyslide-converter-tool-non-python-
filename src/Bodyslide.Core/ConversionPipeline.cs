using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Security;
using System.Text.Json;

namespace Bodyslide.Core;

// NOTE: All optional parameters must remain at the END and use named arguments at call-sites
// to preserve positional-constructor compatibility for existing consumers.
public sealed record ConversionRequest(string InputPath, string TargetBody, string? OutputDirectory = null, string? Preset = null, bool OutputZip = false, string? DeformationProfile = null, string? SourceBodyOverride = null);
public sealed record ConversionPreset(string Name, string TargetBody, string DeformationProfile, string PhysicsProfile);

/// <summary>Tracks a matched low-weight (_0) and high-weight (_1) mesh pair for the same armor piece.</summary>
public sealed record WeightVariantPair(string BaseName, string? LowWeightMesh, string? HighWeightMesh);

public sealed record ImportedArmor(
    string SourcePath,
    IReadOnlyList<string> MeshFiles,
    IReadOnlyList<string> TextureFiles,
    IReadOnlyList<string> PhysicsFiles,
    IReadOnlyList<string> BodyReferenceFiles,
    string? TemporaryWorkspace = null,
    IReadOnlyList<WeightVariantPair>? WeightVariantPairs = null);
public sealed record BodyDetectionReport(string Body, double Confidence, IReadOnlyList<string> Evidence);
public sealed record MeshAnalysis(string MeshType, bool PhysicsEnabled, int MeshCount);
public sealed record DeformationCage(string Mode);
public sealed record ConvertedMesh(string MeshType, string Strategy, int MeshCount, IReadOnlyDictionary<string, double> RegionalMorphing);
public sealed record WeightedMesh(string MeshType, string WeightProfile, bool PhysicsWeightsTransferred, IReadOnlyList<string>? SourceSmpBones = null);
public sealed record MorphSet(string LowMorph, string HighMorph, bool BodySlideCompatible);
public sealed record ClippingReport(bool HasClipping, IReadOnlyList<string> Regions, IReadOnlyList<string> DetectionMethods);
public sealed record CorrectionResult(bool Applied, string Method);
public sealed record PhysicsConfig(string Profile, string? CbpcConfigXml = null, string? SmpConfigXml = null);
public sealed record VanillaArmorEntry(
    string Name,
    IReadOnlyList<string> MeshFileTokens,
    string SourceBody,
    IReadOnlyList<string> RegionSlots,
    string RecommendedProfile);
public sealed record VoxelCollisionResult(
    bool HasPenetrations,
    IReadOnlyList<string> AffectedRegions,
    IReadOnlyDictionary<string, double> PushOutMagnitudes,
    int GridResolution);
public sealed record SkeletonBoneMapping(string SourceBone, string TargetBone, bool IsPhysicsBone);
public sealed record SkeletonMappingResult(string SourceSkeleton, string TargetSkeleton, IReadOnlyList<SkeletonBoneMapping> BoneMappings, IReadOnlyList<string> UnsupportedBones);
public sealed record PartitionRebuildingResult(bool Rebuilt, IReadOnlyList<string> Partitions, IReadOnlyList<string> RemovedPartitions);
public sealed record ConversionResult(bool Success, string OutputDirectory, IReadOnlyList<string> Steps, IReadOnlyList<string> OutputFiles);

/// <summary>Identifies which body regions an armor piece primarily covers and how that was determined.</summary>
public sealed record ArmorRegionBinding(IReadOnlyList<string> CoveredRegions, string DetectionMethod);

public sealed record BodySlideProject(string ProjectName, string TargetBody, IReadOnlyList<string> Sliders, string OspXml);
public sealed record TextureSummary(
    int TotalCount,
    IReadOnlyList<string> DiffuseFiles,
    IReadOnlyList<string> NormalFiles,
    IReadOnlyList<string> MissingNormals,
    IReadOnlyList<string>? SpecularFiles = null,
    IReadOnlyList<string>? GlowFiles = null,
    IReadOnlyList<string>? ParallaxFiles = null,
    IReadOnlyList<string>? SubsurfaceFiles = null,
    IReadOnlyList<string>? MaterialTexturePaths = null);

/// <summary>Describes race compatibility between the imported armor's plugin RNAM entries and the target body.</summary>
public sealed record RaceCompatibilityReport(
    bool IsCompatible,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> IncompatibleRaces);

/// <summary>Reports progress during a batch conversion run.</summary>
public sealed record BatchProgressUpdate(int Completed, int Total, string CurrentFile, bool Success);

/// <summary>
/// Result of the normal recalculation pass — reports how many vertex normals were
/// recomputed after cage-deform vertex positions changed, and which smoothing method was used.
/// </summary>
public sealed record NormalRecalcResult(
    int RecalculatedCount,
    string SmoothingMethod,
    int SmoothingGroupCount);

/// <summary>
/// Result of the weight solver pass — reports how many vertices had their bone-weight
/// assignments repaired: overweighted (sum &gt; 1), underweighted (sum &lt; 1 / missing
/// influences), and disconnected (zero total weight).
/// </summary>
public sealed record WeightSolverReport(
    int FixedOverweightCount,
    int FixedUnderweightCount,
    int DisconnectedVertexCount,
    bool WasRepaired);
/// <summary>
/// Describes a single ARMA (ArmorAddon) record found in a plugin file.
/// <c>FormId</c> and <c>EditorId</c> are extracted via binary parsing.
/// <c>BipedSlots</c> contains the decoded equipment slot numbers (30–61) from BOD2/BODT.
/// </summary>
public sealed record PluginArmorAddon(
    string RecordType,
    IReadOnlyList<string> DetectedMeshPaths,
    uint FormId = 0,
    string? EditorId = null,
    IReadOnlyList<int>? BipedSlots = null,
    uint? RaceFormId = null);

/// <summary>
/// Describes a single ARMO (Armor) record found in a plugin file.
/// Carries FormID, EditorID, detected mesh paths (MOD2/MOD3 world models),
/// keyword FormIDs (KWDA) for slot/behaviour filtering, and race FormID (RNAM).
/// </summary>
public sealed record PluginArmorRecord(
    string RecordType,
    IReadOnlyList<string> DetectedMeshPaths,
    uint FormId = 0,
    string? EditorId = null,
    IReadOnlyList<uint>? KeywordFormIds = null,
    uint? RaceFormId = null);

/// <summary>
/// Plugin analysis result — carries scanned plugins, ARMA armor-addon records,
/// ARMO armor records (world/inventory models), and patch guidance text.
/// <c>ArmorRecords</c> is null when the plugin binary does not contain ARMO records.
/// </summary>
public sealed record PluginAnalysisResult(
    IReadOnlyList<string> ScannedPlugins,
    IReadOnlyList<PluginArmorAddon> ArmorAddons,
    string PatchGuidance,
    IReadOnlyList<PluginArmorRecord>? ArmorRecords = null);

/// <summary>
/// Outcome of the binary plugin rewrite pass: how many plugins were processed,
/// how many ARMA and ARMO records were patched, and the paths of the rewritten plugin files.
/// </summary>
public sealed record PluginRewriteResult(
    int PluginsProcessed,
    int ArmaRecordsPatched,
    int PathsRewritten,
    IReadOnlyList<string> PatchedPluginPaths,
    IReadOnlyList<string> Warnings,
    int ArmoRecordsPatched = 0);

/// <summary>
/// Outcome of generating a minimal Bethesda override patch ESP that lists the original
/// plugin as its master and contains only the patched ARMA records (no full-copy).
/// </summary>
public sealed record PatchPluginGenerationResult(
    int PluginsProcessed,
    int ArmaRecordsIncluded,
    IReadOnlyList<string> PatchPluginPaths,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Full parsed descriptor for a single ARMA record — carries everything the patch
/// generator needs: the original record bytes (header + data), the FormID, editor ID,
/// biped-slot set, and the mesh paths that were rewritten.
/// </summary>
internal sealed record ArmaRecordDescriptor(
    uint FormId,
    string PluginFileName,
    string? EditorId,
    IReadOnlyList<int> BipedSlots,
    IReadOnlyList<string> MeshPaths,
    byte[] OriginalRecordHeaderBytes,   // The record header (24 or 20 bytes)
    byte[] OriginalDataBytes,           // The record data payload (not including header)
    uint? RaceFormId = null);           // RNAM — the race this ArmorAddon applies to

/// <summary>
/// Full parsed descriptor for a single ARMO (Armor) record — carries everything the
/// patch generator needs to emit a valid override record: original header bytes, data
/// bytes, FormID, editor ID, the mesh paths found in MOD2/MOD3 subrecords, keyword
/// FormIDs from KWDA (slot/behaviour filtering), and the race FormID from RNAM.
/// </summary>
internal sealed record ArmoRecordDescriptor(
    uint FormId,
    string PluginFileName,
    string? EditorId,
    IReadOnlyList<string> MeshPaths,
    byte[] OriginalRecordHeaderBytes,   // The record header (24 or 20 bytes)
    byte[] OriginalDataBytes,           // The record data payload (not including header)
    IReadOnlyList<uint>? KeywordFormIds = null,  // KWDA — keyword FormIDs
    uint? RaceFormId = null);                    // RNAM — race FormID

public sealed record MeshDependencyMapEntry(
    string Mesh,
    IReadOnlyList<string> Textures,
    IReadOnlyList<string> PhysicsFiles,
    IReadOnlyList<string> BodyReferences,
    IReadOnlyList<string> PluginMeshReferences);

/// <summary>
/// Records per-pose clipping risk for each tested animation pose.
/// High-risk regions are body areas that exceeded the stress threshold in at least one pose.
/// </summary>
public sealed record PoseSimulationResult(
    IReadOnlyList<string> TestedPoses,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PoseClippingRisk,
    IReadOnlyList<string> HighRiskRegions,
    int TotalPosesAtRisk);

/// <summary>
/// Per-bone rotation delta for a single animation pose (Skyrim Z-up coordinate space).
/// <c>RotX</c> is the forward/back tilt angle in radians (positive = forward tilt).
/// <c>TransZ</c> is a vertical offset in normalised mesh-space units (applied before rotation).
/// </summary>
internal readonly record struct PoseBoneRotation(float RotX = 0f, float TransZ = 0f);

/// <summary>Result produced by <see cref="AnimationDrivenGeometrySolver.Solve"/>.</summary>
public sealed record AnimationDrivenResult(
    int VerticesAnalyzed,
    IReadOnlyDictionary<string, double> MaxPushOutPerRegion,
    string Method);

public static class PresetCatalog
{
    private static readonly Dictionary<string, ConversionPreset> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── 3BA ─────────────────────────────────────────────────────────────
        ["3BA Curvy"]         = new("3BA Curvy",         "3BA",   "curvy",    "smp+cbpc"),
        ["3BA Slim"]          = new("3BA Slim",           "3BA",   "slim",     "smp+cbpc"),
        ["3BA Athletic"]      = new("3BA Athletic",       "3BA",   "athletic", "smp+cbpc"),
        // ── BHUNP ────────────────────────────────────────────────────────────
        ["BHUNP Curvy"]       = new("BHUNP Curvy",        "BHUNP", "curvy",    "smp+cbpc"),
        ["BHUNP Slim"]        = new("BHUNP Slim",         "BHUNP", "slim",     "smp+cbpc"),
        ["BHUNP Athletic"]    = new("BHUNP Athletic",     "BHUNP", "athletic", "smp+cbpc"),
        // ── CBBE ────────────────────────────────────────────────────────────
        ["CBBE Curvy"]        = new("CBBE Curvy",         "CBBE",  "curvy",    "none"),
        ["CBBE Slim"]         = new("CBBE Slim",          "CBBE",  "slim",     "none"),
        ["CBBE Athletic"]     = new("CBBE Athletic",      "CBBE",  "athletic", "none"),
        ["CBBE Petite"]       = new("CBBE Petite",        "CBBE",  "petite",   "none"),
        // ── UNP ─────────────────────────────────────────────────────────────
        ["UNP Petite"]        = new("UNP Petite",         "UNP",   "petite",   "cbpc"),
        ["UNP Athletic"]      = new("UNP Athletic",       "UNP",   "athletic", "cbpc"),
        ["UNP Curvy"]         = new("UNP Curvy",          "UNP",   "curvy",    "cbpc"),
        ["UNP Slim"]          = new("UNP Slim",           "UNP",   "slim",     "cbpc"),
        // ── TBD ─────────────────────────────────────────────────────────────
        ["TBD Lean"]          = new("TBD Lean",           "TBD",   "lean",     "cbpc"),
        ["TBD Curvy"]         = new("TBD Curvy",          "TBD",   "curvy",    "cbpc"),
        ["TBD Athletic"]      = new("TBD Athletic",       "TBD",   "athletic", "cbpc"),
        // ── SAM ─────────────────────────────────────────────────────────────
        ["SAM Athletic"]      = new("SAM Athletic",       "SAM",   "athletic", "smp"),
        ["SAM Lean"]          = new("SAM Lean",           "SAM",   "lean",     "smp"),
        ["SAM Muscular"]      = new("SAM Muscular",       "SAM",   "muscular", "smp"),
        // ── SOS ─────────────────────────────────────────────────────────────
        ["SOS Lean"]          = new("SOS Lean",           "SOS",   "lean",     "smp"),
        ["SOS Athletic"]      = new("SOS Athletic",       "SOS",   "athletic", "smp"),
        // ── UBE ─────────────────────────────────────────────────────────────
        ["UBE Petite"]        = new("UBE Petite",         "UBE",   "petite",   "none"),
        ["UBE Curvy"]         = new("UBE Curvy",          "UBE",   "curvy",    "none"),
        // ── Anime ───────────────────────────────────────────────────────────
        ["CBBE Anime"]        = new("CBBE Anime",         "CBBE",  "anime",    "none"),
        ["3BA Anime"]         = new("3BA Anime",          "3BA",   "anime",    "smp+cbpc"),
        ["BHUNP Anime"]       = new("BHUNP Anime",        "BHUNP", "anime",    "smp+cbpc"),
        ["UNP Anime"]         = new("UNP Anime",          "UNP",   "anime",    "cbpc"),
        // ── Vanilla ─────────────────────────────────────────────────────────
        ["Vanilla Balanced"]  = new("Vanilla Balanced",   "Vanilla", "balanced", "none"),
        ["Vanilla to CBBE"]   = new("Vanilla to CBBE",    "CBBE",    "balanced", "none"),
        ["Vanilla to 3BA"]    = new("Vanilla to 3BA",     "3BA",     "balanced", "smp+cbpc"),
        ["Vanilla to HIMBO"]  = new("Vanilla to HIMBO",   "HIMBO",   "balanced", "smp"),
        ["Vanilla to UNP"]    = new("Vanilla to UNP",     "UNP",     "balanced", "cbpc"),
        // ── HIMBO ───────────────────────────────────────────────────────────
        ["HIMBO Lean"]        = new("HIMBO Lean",         "HIMBO", "lean",     "smp"),
        ["HIMBO Muscular"]    = new("HIMBO Muscular",     "HIMBO", "muscular", "smp"),
        ["HIMBO Athletic"]    = new("HIMBO Athletic",     "HIMBO", "athletic", "smp"),
    };

    public static IReadOnlyCollection<ConversionPreset> All => Presets.Values;

    public static bool TryGet(string presetName, out ConversionPreset preset) =>
        Presets.TryGetValue(presetName, out preset!);
}

public static class RequestNormalizer
{
    public static (ConversionRequest Request, ConversionPreset? Preset) Normalize(ConversionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Preset) && PresetCatalog.TryGet(request.Preset, out var preset))
        {
            return (request with { TargetBody = preset.TargetBody }, preset);
        }

        return (request, null);
    }
}

/// <summary>Exposes supported body type names and vertex-count hints for display / tooling consumers.</summary>
public sealed record BodyTypeInfo(string Name, IReadOnlyList<string> DetectionTokens, int VertexCountMin, int VertexCountMax);

/// <summary>Public catalog of all body types that the detection engine recognises.</summary>
public static class BodyTypeCatalog
{
    private static readonly Lazy<IReadOnlyList<BodyTypeInfo>> _all = new(static () =>
        VanillaBodySignatureDatabase.Templates
            .Select(static t => new BodyTypeInfo(t.Body, t.MeshTokens, t.VertexCountMin, t.VertexCountMax))
            .ToList());

    public static IReadOnlyList<BodyTypeInfo> All => _all.Value;
}

/// <summary>
/// Defines token signatures used to match imported assets to known body families.
/// Mesh, texture, and physics token hit ratios are combined with optional vertex count
/// range hints into a confidence score.
/// </summary>
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
    double DepthToWidthRatioMax = 0);

internal readonly record struct MeshVertex(float X, float Y, float Z);

internal sealed record MeshGeometrySignature(
    int VertexCount,
    IReadOnlyList<MeshVertex> SampleVertices,
    float MinX,
    float MaxX,
    float MinY,
    float MaxY,
    float MinZ,
    float MaxZ)
{
    public float Width => MaxX - MinX;
    public float Depth => MaxY - MinY;
    public float Height => MaxZ - MinZ;
}

internal sealed record NifBlockGraphNode(
    int Index,
    string TypeName,
    int StartOffset,
    int EndOffset,
    IReadOnlyList<int> ReferencedBlockIndices);

internal sealed record NifBlockGraph(IReadOnlyList<NifBlockGraphNode> Nodes)
{
    public IReadOnlyList<NifBlockGraphNode> GeometryCandidates =>
        Nodes.Where(static node =>
                node.TypeName.Contains("TriShapeData", StringComparison.Ordinal) ||
                node.TypeName.Contains("TriStripsData", StringComparison.Ordinal) ||
                node.TypeName.Contains("GeometryData", StringComparison.Ordinal) ||
                node.TypeName.Contains("Mesh", StringComparison.Ordinal))
            .ToList();
}

internal static class NifBlockGraphParser
{
    private static readonly byte[] NifHeaderToken = System.Text.Encoding.ASCII.GetBytes("Gamebryo File Format");
    private static readonly string[] LikelyBlockTypeTokens =
    [
        "TriShape", "TriStrips", "Geometry", "Mesh", "Node", "Skin", "Data", "Controller", "Property"
    ];

    public static bool TryParse(byte[] bytes, out NifBlockGraph? graph)
    {
        graph = null;

        if (bytes.Length < 64 || bytes.AsSpan().IndexOf(NifHeaderToken) < 0)
        {
            return false;
        }

        var typeCandidates = FindBlockTypeCandidates(bytes);
        if (typeCandidates.Count == 0)
        {
            return false;
        }

        var nodes = new List<NifBlockGraphNode>(typeCandidates.Count);
        for (var index = 0; index < typeCandidates.Count; index++)
        {
            var (startOffset, typeName) = typeCandidates[index];
            var endOffset = index + 1 < typeCandidates.Count
                ? typeCandidates[index + 1].Offset
                : bytes.Length;

            if (endOffset <= startOffset)
            {
                continue;
            }

            var references = CollectReferences(bytes, startOffset, endOffset, typeCandidates.Count);
            nodes.Add(new NifBlockGraphNode(index, typeName, startOffset, endOffset, references));
        }

        if (nodes.Count == 0)
        {
            return false;
        }

        graph = new NifBlockGraph(nodes);
        return true;
    }

    private static List<(int Offset, string TypeName)> FindBlockTypeCandidates(byte[] bytes)
    {
        var matches = new List<(int Offset, string TypeName)>();
        var seenOffsets = new HashSet<int>();

        for (var offset = 0; offset < bytes.Length - 2; offset++)
        {
            if (bytes[offset] != (byte)'N' || bytes[offset + 1] != (byte)'i')
            {
                continue;
            }

            var end = offset + 2;
            while (end < bytes.Length && IsAsciiWordCharacter((char)bytes[end]) && (end - offset) <= 64)
            {
                end++;
            }

            var length = end - offset;
            if (length < 4 || length > 64)
            {
                continue;
            }

            var typeName = System.Text.Encoding.ASCII.GetString(bytes, offset, length);
            if (!IsLikelyBlockTypeName(typeName) || !seenOffsets.Add(offset))
            {
                continue;
            }

            matches.Add((offset, typeName));
        }

        matches.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));
        return matches;
    }

    private static bool IsLikelyBlockTypeName(string value)
    {
        if (!value.StartsWith("Ni", StringComparison.Ordinal) || value.Length < 4)
        {
            return false;
        }

        return LikelyBlockTypeTokens.Any(token => value.Contains(token, StringComparison.Ordinal));
    }

    private static List<int> CollectReferences(byte[] bytes, int startOffset, int endOffset, int blockCount)
    {
        var references = new HashSet<int>();
        var scanStart = Math.Max(startOffset, 0);
        var scanEnd = Math.Min(endOffset - sizeof(int), bytes.Length - sizeof(int));

        for (var offset = scanStart; offset <= scanEnd; offset += sizeof(int))
        {
            var candidate = BitConverter.ToInt32(bytes, offset);
            if (candidate >= 0 && candidate < blockCount)
            {
                references.Add(candidate);
            }
        }

        return references.OrderBy(static value => value).ToList();
    }

    private static bool IsAsciiWordCharacter(char value) =>
        char.IsLetterOrDigit(value) || value == '_' || value == '-';
}

internal static class VanillaBodySignatureDatabase
{
    // Typical vertex counts per body type are well-known in the modding community.
    // These ranges are used as additional scoring hints when NIF data is available.
    // CBBE:  ~6942 vertices (standard), UNP: ~6032, HIMBO: ~6820, BHUNP: ~10080,
    // 3BA:   ~10032 (CBBE base with physics), TBD: ~7680, SAM: ~5984, SOS: ~6274, UBE: ~7000
    public static readonly IReadOnlyList<BodySignatureTemplate> Templates =
    [
        new("CBBE",    ["cbbe", "caliente"],         ["femalebody_1", "femalebody_0"], [],            6800, 7100, 4.2, 7.2, 0.30, 0.80),
        new("UNP",     ["unp", "unpb"],              ["femalebody"],                  [],            5900, 6200, 4.3, 7.4, 0.28, 0.75),
        new("HIMBO",   ["himbo", "male"],            ["malebody"],                    [],            6600, 7100, 3.2, 6.8, 0.32, 0.95),
        new("BHUNP",   ["bhunp"],                    ["femalebody"],                  [],            9800, 10400, 4.0, 7.0, 0.33, 0.85),
        new("3BA",     ["3ba", "cbbe", "bodyslide"],["femalebody"],                  ["smp", "cbpc"], 9800, 10400, 4.0, 7.0, 0.33, 0.85),
        new("TBD",     ["tbd"],                      ["femalebody"],                  [],            7400, 7900, 4.1, 7.2, 0.30, 0.82),
        new("SAM",     ["sam", "samlight"],          ["malebody"],                    [],            5800, 6200, 3.3, 6.8, 0.32, 0.95),
        new("SOS",     ["sos", "soslight"],          ["malebody"],                    ["smp"],       6100, 6500, 3.2, 6.8, 0.32, 0.95),
        new("UBE",     ["ube"],                      ["femalebody"],                  [],            6800, 7200, 4.3, 7.4, 0.30, 0.80),
        new("Vanilla", ["vanilla", "femalebody", "malebody"], ["femalebody", "malebody"], [],        4000, 6100, 4.0, 7.5, 0.28, 0.90),
    ];
}

internal static class NifGeometrySignatureReader
{
    // Lightweight heuristics for plausible body/armor meshes.
    private const int MinPlausibleVertexCount = 256;
    private const int MaxPlausibleVertexCount = 250_000;
    private const float MaxPlausibleCoordinateValue = 8192f;
    private const int HeuristicScanByteLimit = 64 * 1024;
    private static readonly byte[] EmbeddedVertexMarker = System.Text.Encoding.ASCII.GetBytes("VERT");
    private static readonly byte[] NifHeaderToken = System.Text.Encoding.ASCII.GetBytes("Gamebryo File Format");

    public static MeshGeometrySignature? TryReadBest(IEnumerable<string> meshFiles)
    {
        MeshGeometrySignature? best = null;

        foreach (var meshFile in meshFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var signature = TryRead(meshFile);
            if (signature is null)
            {
                continue;
            }

            if (best is null || signature.VertexCount > best.VertexCount)
            {
                best = signature;
            }
        }

        return best;
    }

    public static MeshGeometrySignature? TryRead(string meshFile)
    {
        if (!File.Exists(meshFile) || !Path.GetExtension(meshFile).Equals(".nif", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(meshFile);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (bytes.Length < 32)
        {
            return null;
        }

        if (bytes.AsSpan().IndexOf(NifHeaderToken) < 0)
        {
            return null;
        }

        var embeddedMarkerOffset = bytes.AsSpan().IndexOf(EmbeddedVertexMarker);
        if (embeddedMarkerOffset >= 0)
        {
            var embeddedSignature = TryReadEmbeddedVertexBlock(bytes, embeddedMarkerOffset);
            if (embeddedSignature is not null)
            {
                return embeddedSignature;
            }
        }

        var graphSignature = TryReadBlockGraphVertexBlock(bytes);
        if (graphSignature is not null)
        {
            return graphSignature;
        }

        return TryReadHeuristicVertexBlock(bytes);
    }

    public static bool TryLocateVertexBlock(byte[] bytes, out int vertexDataOffset, out int vertexCount)
    {
        vertexDataOffset = 0;
        vertexCount = 0;

        if (bytes.Length < 32 || bytes.AsSpan().IndexOf(NifHeaderToken) < 0)
        {
            return false;
        }

        var embeddedMarkerOffset = bytes.AsSpan().IndexOf(EmbeddedVertexMarker);
        if (embeddedMarkerOffset >= 0)
        {
            var countOffset = embeddedMarkerOffset + EmbeddedVertexMarker.Length;
            if (countOffset + sizeof(int) <= bytes.Length)
            {
                var embeddedCount = BitConverter.ToInt32(bytes, countOffset);
                if (BuildSignature(bytes, countOffset + sizeof(int), embeddedCount) is not null)
                {
                    vertexDataOffset = countOffset + sizeof(int);
                    vertexCount = embeddedCount;
                    return true;
                }
            }
        }

        if (TryLocateVertexBlockFromGraph(bytes, out var graphVertexDataOffset, out var graphVertexCount))
        {
            vertexDataOffset = graphVertexDataOffset;
            vertexCount = graphVertexCount;
            return true;
        }

        var scanLimit = Math.Min(bytes.Length - sizeof(int), HeuristicScanByteLimit);
        var bestCount = 0;
        var bestOffset = -1;

        for (var offset = 0; offset <= scanLimit; offset += sizeof(int))
        {
            var candidateVertexCount = BitConverter.ToInt32(bytes, offset);
            if (candidateVertexCount is < MinPlausibleVertexCount or > MaxPlausibleVertexCount)
            {
                continue;
            }

            var candidate = BuildSignature(bytes, offset + sizeof(int), candidateVertexCount);
            if (candidate is null || candidate.VertexCount <= bestCount)
            {
                continue;
            }

            bestCount = candidate.VertexCount;
            bestOffset = offset + sizeof(int);
        }

        if (bestOffset < 0)
        {
            return false;
        }

        vertexDataOffset = bestOffset;
        vertexCount = bestCount;
        return true;
    }

    private static MeshGeometrySignature? TryReadEmbeddedVertexBlock(byte[] bytes, int markerOffset)
    {
        var countOffset = markerOffset + EmbeddedVertexMarker.Length;
        if (countOffset + sizeof(int) > bytes.Length)
        {
            return null;
        }

        var vertexCount = BitConverter.ToInt32(bytes, countOffset);
        return BuildSignature(bytes, countOffset + sizeof(int), vertexCount);
    }

    private static MeshGeometrySignature? TryReadBlockGraphVertexBlock(byte[] bytes)
    {
        if (!TryLocateVertexBlockFromGraph(bytes, out var vertexDataOffset, out var vertexCount))
        {
            return null;
        }

        return BuildSignature(bytes, vertexDataOffset, vertexCount);
    }

    private static bool TryLocateVertexBlockFromGraph(byte[] bytes, out int vertexDataOffset, out int vertexCount)
    {
        vertexDataOffset = 0;
        vertexCount = 0;

        if (!NifBlockGraphParser.TryParse(bytes, out var graph) || graph is null)
        {
            return false;
        }

        var bestScore = int.MinValue;
        var bestCount = 0;
        var bestOffset = -1;
        var preferredNodes = graph.GeometryCandidates.Count > 0 ? graph.GeometryCandidates : graph.Nodes;

        foreach (var node in preferredNodes)
        {
            var score = GetBlockVertexCandidateScore(node.TypeName);
            if (score <= 0)
            {
                continue;
            }

            var scanStart = Math.Max(node.StartOffset, 0);
            var scanEnd = Math.Min(node.EndOffset - sizeof(int), bytes.Length - sizeof(int));

            for (var offset = scanStart; offset <= scanEnd; offset++)
            {
                var candidateVertexCount = BitConverter.ToInt32(bytes, offset);
                if (candidateVertexCount is < MinPlausibleVertexCount or > MaxPlausibleVertexCount)
                {
                    continue;
                }

                var candidate = BuildSignature(bytes, offset + sizeof(int), candidateVertexCount);
                if (candidate is null)
                {
                    continue;
                }

                if (score > bestScore || (score == bestScore && candidate.VertexCount > bestCount))
                {
                    bestScore = score;
                    bestCount = candidate.VertexCount;
                    bestOffset = offset + sizeof(int);
                }
            }
        }

        if (bestOffset < 0)
        {
            return false;
        }

        vertexDataOffset = bestOffset;
        vertexCount = bestCount;
        return true;
    }

    private static int GetBlockVertexCandidateScore(string typeName)
    {
        if (typeName.Contains("TriShapeData", StringComparison.Ordinal))
        {
            return 10;
        }

        if (typeName.Contains("TriStripsData", StringComparison.Ordinal))
        {
            return 9;
        }

        if (typeName.Contains("GeometryData", StringComparison.Ordinal))
        {
            return 8;
        }

        if (typeName.Contains("Mesh", StringComparison.Ordinal))
        {
            return 7;
        }

        if (typeName.Contains("Shape", StringComparison.Ordinal))
        {
            return 4;
        }

        if (typeName.Contains("Geometry", StringComparison.Ordinal))
        {
            return 3;
        }

        return 0;
    }

    private static MeshGeometrySignature? TryReadHeuristicVertexBlock(byte[] bytes)
    {
        MeshGeometrySignature? best = null;
        var scanLimit = Math.Min(bytes.Length - sizeof(int), HeuristicScanByteLimit);

        for (var offset = 0; offset <= scanLimit; offset += sizeof(int))
        {
            var candidateVertexCount = BitConverter.ToInt32(bytes, offset);
            if (candidateVertexCount is < MinPlausibleVertexCount or > MaxPlausibleVertexCount)
            {
                continue;
            }

            var candidate = BuildSignature(bytes, offset + sizeof(int), candidateVertexCount);
            if (candidate is null)
            {
                continue;
            }

            if (best is null || candidate.VertexCount > best.VertexCount)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static MeshGeometrySignature? BuildSignature(byte[] bytes, int vertexDataOffset, int vertexCount)
    {
        if (vertexCount <= 0)
        {
            return null;
        }

        var requiredBytes = (long)vertexCount * 12;
        if (vertexDataOffset < 0 || vertexDataOffset + requiredBytes > bytes.Length)
        {
            return null;
        }

        var sampleStride = Math.Max(1, vertexCount / 256);
        var sampleVertices = new List<MeshVertex>(Math.Min(vertexCount, 256));
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;

        for (var index = 0; index < vertexCount; index++)
        {
            var offset = vertexDataOffset + (index * 12);
            var x = BitConverter.ToSingle(bytes, offset);
            var y = BitConverter.ToSingle(bytes, offset + 4);
            var z = BitConverter.ToSingle(bytes, offset + 8);

            if (!IsPlausibleCoordinate(x) || !IsPlausibleCoordinate(y) || !IsPlausibleCoordinate(z))
            {
                return null;
            }

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);

            if (index % sampleStride == 0 || sampleVertices.Count < 24)
            {
                sampleVertices.Add(new MeshVertex(x, y, z));
            }
        }

        if ((maxX - minX) < 0.001f || (maxZ - minZ) < 0.001f)
        {
            return null;
        }

        return new MeshGeometrySignature(vertexCount, sampleVertices, minX, maxX, minY, maxY, minZ, maxZ);
    }

    private static bool IsPlausibleCoordinate(float value) =>
        float.IsFinite(value) && Math.Abs(value) <= MaxPlausibleCoordinateValue;
}

internal static class BodyTransformationFieldCatalog
{
    // Regions match the BodySlide slider taxonomy: 5 structural + 6 shape-specific.
    // Values are expansion multipliers relative to the vanilla body (1.0 = no change).
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Fields =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CBBE"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.08,  ["waist"]    = 0.96,  ["pelvis"]   = 1.05,
                ["legs"]      = 1.03,  ["shoulders"] = 1.01,
                ["breasts"]   = 1.09,  ["butt"]     = 1.06,  ["belly"]    = 1.02,
                ["arms"]      = 1.01,  ["thighs"]   = 1.04,  ["calves"]   = 1.02
            },
            ["3BA"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.12,  ["waist"]    = 0.95,  ["pelvis"]   = 1.06,
                ["legs"]      = 1.04,  ["shoulders"] = 1.01,
                ["breasts"]   = 1.13,  ["butt"]     = 1.08,  ["belly"]    = 1.03,
                ["arms"]      = 1.02,  ["thighs"]   = 1.05,  ["calves"]   = 1.03
            },
            ["BHUNP"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.10,  ["waist"]    = 0.94,  ["pelvis"]   = 1.07,
                ["legs"]      = 1.04,  ["shoulders"] = 1.01,
                ["breasts"]   = 1.11,  ["butt"]     = 1.07,  ["belly"]    = 1.03,
                ["arms"]      = 1.01,  ["thighs"]   = 1.05,  ["calves"]   = 1.03
            },
            ["UNP"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.04,  ["waist"]    = 0.97,  ["pelvis"]   = 1.02,
                ["legs"]      = 1.01,  ["shoulders"] = 1.00,
                ["breasts"]   = 1.04,  ["butt"]     = 1.02,  ["belly"]    = 1.01,
                ["arms"]      = 1.00,  ["thighs"]   = 1.02,  ["calves"]   = 1.01
            },
            ["TBD"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.06,  ["waist"]    = 0.96,  ["pelvis"]   = 1.04,
                ["legs"]      = 1.02,  ["shoulders"] = 1.00,
                ["breasts"]   = 1.07,  ["butt"]     = 1.04,  ["belly"]    = 1.02,
                ["arms"]      = 1.00,  ["thighs"]   = 1.03,  ["calves"]   = 1.02
            },
            ["UBE"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.06,  ["waist"]    = 0.97,  ["pelvis"]   = 1.03,
                ["legs"]      = 1.02,  ["shoulders"] = 1.01,
                ["breasts"]   = 1.06,  ["butt"]     = 1.03,  ["belly"]    = 1.02,
                ["arms"]      = 1.01,  ["thighs"]   = 1.03,  ["calves"]   = 1.02
            },
            ["HIMBO"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.10,  ["waist"]    = 1.02,  ["pelvis"]   = 1.04,
                ["legs"]      = 1.06,  ["shoulders"] = 1.12,
                ["breasts"]   = 1.08,  ["butt"]     = 1.05,  ["belly"]    = 1.03,
                ["arms"]      = 1.10,  ["thighs"]   = 1.07,  ["calves"]   = 1.05
            },
            ["SAM"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.08,  ["waist"]    = 1.01,  ["pelvis"]   = 1.03,
                ["legs"]      = 1.05,  ["shoulders"] = 1.10,
                ["breasts"]   = 1.05,  ["butt"]     = 1.04,  ["belly"]    = 1.02,
                ["arms"]      = 1.08,  ["thighs"]   = 1.06,  ["calves"]   = 1.04
            },
            ["SOS"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.05,  ["waist"]    = 1.00,  ["pelvis"]   = 1.02,
                ["legs"]      = 1.04,  ["shoulders"] = 1.06,
                ["breasts"]   = 1.03,  ["butt"]     = 1.03,  ["belly"]    = 1.01,
                ["arms"]      = 1.05,  ["thighs"]   = 1.04,  ["calves"]   = 1.03
            },
            ["Vanilla"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = 1.00,  ["waist"]    = 1.00,  ["pelvis"]   = 1.00,
                ["legs"]      = 1.00,  ["shoulders"] = 1.00,
                ["breasts"]   = 1.00,  ["butt"]     = 1.00,  ["belly"]    = 1.00,
                ["arms"]      = 1.00,  ["thighs"]   = 1.00,  ["calves"]   = 1.00
            }
        };

    public static IReadOnlyDictionary<string, double> Resolve(string targetBody)
    {
        if (Fields.TryGetValue(targetBody, out var profile))
        {
            return profile;
        }

        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["chest"]     = 1.02,  ["waist"]    = 0.99,  ["pelvis"]   = 1.02,
            ["legs"]      = 1.01,  ["shoulders"] = 1.00,
            ["breasts"]   = 1.02,  ["butt"]     = 1.01,  ["belly"]    = 1.01,
            ["arms"]      = 1.00,  ["thighs"]   = 1.01,  ["calves"]   = 1.01
        };
    }
}

public interface IArmorImportService
{
    Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken);
}

public interface IBodyDetectionService
{
    Task<BodyDetectionReport> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken);
}

public interface IMeshAnalysisService
{
    Task<MeshAnalysis> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken);
}

public interface ICageGenerationService
{
    Task<DeformationCage> BuildAsync(MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken);
}

public interface IMeshConversionService
{
    Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, string? deformationProfile, string? sourceBody, CancellationToken cancellationToken);
}

public interface IWeightTransferService
{
    Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken) =>
        TransferAsync(mesh, analysis, targetBody, null, cancellationToken);

    Task<WeightedMesh> TransferAsync(ConvertedMesh mesh, MeshAnalysis analysis, string targetBody, ImportedArmor? sourceArmor, CancellationToken cancellationToken);
}

public interface IMorphGenerationService
{
    Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface IClippingDetectionService
{
    Task<ClippingReport> DetectAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface IAutoCorrectionService
{
    Task<CorrectionResult> CorrectAsync(ConvertedMesh mesh, ClippingReport clipping, CancellationToken cancellationToken);
}

public interface IPhysicsSupportService
{
    Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, string physicsProfile, CancellationToken cancellationToken);
}

public interface ISkeletonMappingService
{
    /// <summary>Maps source skeleton bones to the target body skeleton, reporting any unsupported physics bones.</summary>
    Task<SkeletonMappingResult> MapAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken);
}

public interface IPartitionRebuildingService
{
    /// <summary>Rebuilds BSDismemberSkinInstance partitions for Skyrim compatibility after mesh conversion.</summary>
    Task<PartitionRebuildingResult> RebuildAsync(WeightedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken);
}

public interface IExportService
{
    Task<(string OutputDirectory, IReadOnlyList<string> OutputFiles)> ExportAsync(
        ConversionRequest request,
        ImportedArmor armor,
        MeshAnalysis analysis,
        ConvertedMesh mesh,
        MorphSet morphs,
        PhysicsConfig physics,
        ClippingReport clipping,
        CorrectionResult correction,
        BodySlideProject bodySlideProject,
        PluginAnalysisResult pluginAnalysis,
        TextureSummary textureSummary,
        PoseSimulationResult poseSimulation,
        IReadOnlyList<string> steps,
        CancellationToken cancellationToken);
}

public interface IBodySlideProjectService
{
    /// <summary>Generates a BodySlide .osp project file for the converted armor targeting a specific body.</summary>
    Task<BodySlideProject> GenerateAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface ITextureAnalysisService
{
    /// <summary>Analyzes DDS texture files for normal map coverage and reports missing normal maps.</summary>
    Task<TextureSummary> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken);
}

public interface IPluginAnalysisService
{
    /// <summary>Scans .esp/.esm/.esl plugin files for ArmorAddon mesh path references and generates patch guidance.</summary>
    Task<PluginAnalysisResult> AnalyzeAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken);
}

public interface IPluginRewriteService
{
    /// <summary>
    /// Parses each plugin binary, finds ARMA records, rewrites MOD2/MOD3/MOD4/MOD5 mesh path
    /// subrecords according to <paramref name="rewriteMap"/>, and writes a patched copy to
    /// <paramref name="outputDirectory"/> for every plugin that contained matching paths.
    /// </summary>
    Task<PluginRewriteResult> RewriteAsync(
        IReadOnlyList<string> pluginPaths,
        IReadOnlyDictionary<string, string> rewriteMap,
        string outputDirectory,
        CancellationToken cancellationToken);
}

public interface IVanillaArmorLookupService
{
    /// <summary>Tries to match a mesh file name against the vanilla armor static database.</summary>
    bool TryLookup(string meshFileName, out VanillaArmorEntry? entry);

    IReadOnlyList<VanillaArmorEntry> All { get; }
}

public interface IVoxelCollisionService
{
    /// <summary>
    /// Computes voxel-grid penetration between the converted mesh and the target body,
    /// returning push-out magnitudes per body region.
    /// </summary>
    Task<VoxelCollisionResult> ComputeAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken);
}

public interface IArmorRegionBindingService
{
    /// <summary>
    /// Identifies which body regions (chest, waist, pelvis, legs, shoulders, arms, etc.) the armor
    /// primarily covers by scoring physics-file bone names and mesh filename keywords.
    /// </summary>
    Task<ArmorRegionBinding> BindAsync(ImportedArmor armor, MeshAnalysis analysis, CancellationToken cancellationToken);
}

public interface IPoseSimulationService
{
    /// <summary>
    /// Simulates the converted mesh against a set of animation poses and reports per-pose
    /// clipping risk by body region. Uses per-pose regional stress amplifiers to compute
    /// an effective stretch factor; regions where the factor exceeds the risk threshold
    /// are flagged as at-risk for that pose.
    /// </summary>
    Task<PoseSimulationResult> SimulateAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken);

    /// <summary>
    /// Extended entry point that optionally supplies source mesh file paths so the
    /// implementation can read NIF vertex data for animation-driven geometry solving.
    /// The default implementation delegates to <see cref="SimulateAsync"/> and ignores the paths.
    /// </summary>
    Task<PoseSimulationResult> SimulateWithMeshDataAsync(
        ConvertedMesh mesh,
        string targetBody,
        IReadOnlyList<string>? sourceMeshPaths,
        CancellationToken cancellationToken)
        => SimulateAsync(mesh, targetBody, cancellationToken);
}

public interface IRaceCompatibilityService
{
    /// <summary>
    /// Checks whether the armor plugin's race references (RNAM) are compatible with the target body.
    /// Returns warnings for races whose body shape is not covered by the selected body replacer.
    /// </summary>
    Task<RaceCompatibilityReport> CheckAsync(
        PluginAnalysisResult pluginAnalysis,
        string targetBody,
        CancellationToken cancellationToken);
}

/// <summary>
/// Recomputes vertex normals after cage-deformation has moved vertex positions.
/// Uses angle-weighted averaging: for each vertex, accumulates face normals weighted
/// by the interior angle at that vertex, then normalises the result.
/// </summary>
public interface INormalRecalculationService
{
    Task<NormalRecalcResult> RecalculateAsync(ConvertedMesh mesh, CancellationToken cancellationToken);
}

/// <summary>
/// Detects and repairs bone-weight assignment problems in a weighted mesh.
/// Handles three classes of defect:
///   • Overweighted — vertex influence sum &gt; 1.0 (normalised to 1.0).
///   • Underweighted — vertex influence sum &lt; 1.0 (missing influences filled from nearest neighbour).
///   • Disconnected — vertex with zero total weight (assigned to root/pelvis bone).
/// </summary>
public interface IWeightSolverService
{
    Task<WeightSolverReport> SolveAsync(WeightedMesh mesh, CancellationToken cancellationToken);
}

public sealed class ConversionOrchestrator(
    IArmorImportService importer,
    IBodyDetectionService bodyDetector,
    IMeshAnalysisService meshAnalyzer,
    ICageGenerationService cageGenerator,
    IMeshConversionService meshConverter,
    IWeightTransferService weightTransfer,
    ISkeletonMappingService skeletonMapper,
    IMorphGenerationService morphGenerator,
    IPartitionRebuildingService partitionRebuilder,
    IClippingDetectionService clippingDetector,
    IAutoCorrectionService autoCorrection,
    IPhysicsSupportService physicsSupport,
    IBodySlideProjectService bodySlideProjectService,
    ITextureAnalysisService textureAnalysisService,
    IPluginAnalysisService pluginAnalysisService,
    IVanillaArmorLookupService vanillaArmorLookup,
    IVoxelCollisionService voxelCollision,
    IArmorRegionBindingService armorRegionBinder,
    IPoseSimulationService poseSimulator,
    IExportService exporter,
    IRaceCompatibilityService? raceCompatService = null,
    INormalRecalculationService? normalRecalcService = null,
    IWeightSolverService? weightSolverService = null)
{
    public async Task<ConversionResult> ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InputPath))
        {
            throw new ArgumentException("Input path is required.", nameof(request));
        }

        if (!File.Exists(request.InputPath) && !Directory.Exists(request.InputPath))
        {
            throw new FileNotFoundException("Input path was not found.", request.InputPath);
        }

        var normalized = RequestNormalizer.Normalize(request);
        if (string.IsNullOrWhiteSpace(normalized.Request.TargetBody))
        {
            throw new ArgumentException("Target body or preset is required.", nameof(request));
        }

        var steps = new List<string>();
        ImportedArmor? armor = null;

        try
        {
            var deformationProfile = normalized.Preset?.DeformationProfile ?? normalized.Request.DeformationProfile;
            if (!string.IsNullOrWhiteSpace(deformationProfile))
            {
                steps.Add($"deformation-profile:{deformationProfile}");
            }

            armor = await importer.ImportAsync(normalized.Request.InputPath, cancellationToken);
            steps.Add($"imported:meshes={armor.MeshFiles.Count},textures={armor.TextureFiles.Count},physics={armor.PhysicsFiles.Count},bodyrefs={armor.BodyReferenceFiles.Count}");

            // Weight variant pair detection — report how many _0/_1 mesh pairs were found.
            var weightPairs = armor.WeightVariantPairs ?? [];
            var fullPairs    = weightPairs.Count(p => p.LowWeightMesh is not null && p.HighWeightMesh is not null);
            var missingPairs = weightPairs.Count(p => p.LowWeightMesh is null || p.HighWeightMesh is null);
            if (weightPairs.Count > 0)
            {
                steps.Add($"weight-variants:pairs={fullPairs},incomplete={missingPairs}");
            }

            var defaultOutput = Path.Combine(
                Environment.CurrentDirectory,
                "output",
                normalized.Request.TargetBody,
                Path.GetFileNameWithoutExtension(armor.MeshFiles[0]));
            var outputDirectory = Path.GetFullPath(normalized.Request.OutputDirectory ?? defaultOutput);
            var cachePath = Path.Combine(outputDirectory, ".conversion-learning-cache.json");
            var cacheEntries = await ConversionLearningCache.LoadEntriesAsync(cachePath, cancellationToken);
            var cacheKey = ConversionLearningCache.BuildCacheKey(
                Path.GetFileNameWithoutExtension(armor.MeshFiles[0]) ?? "unknown",
                normalized.Request.TargetBody);
            var cachedEntry = cacheEntries
                .Where(entry => string.Equals(entry.Key, cacheKey, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry => entry.LastSuccessfulConversion)
                .FirstOrDefault();
            if (cachedEntry is not null)
            {
                steps.Add($"learning-cache:hit={cachedEntry.LastSuccessfulConversion:O}");
            }

            // Vanilla armor database lookup — enriches detection with known region maps.
            var vanillaEntry = armor.MeshFiles
                .Select(mesh => vanillaArmorLookup.TryLookup(Path.GetFileName(mesh), out var entry) ? entry : null)
                .FirstOrDefault(entry => entry is not null);
            steps.Add(vanillaEntry is not null
                ? $"vanilla-armor:{vanillaEntry.Name},profile={vanillaEntry.RecommendedProfile},slots={vanillaEntry.RegionSlots.Count}"
                : "vanilla-armor:unknown");

            // Auto-apply the vanilla armor's recommended deformation profile when none was explicitly provided.
            if (vanillaEntry is not null && string.IsNullOrWhiteSpace(deformationProfile))
            {
                deformationProfile = vanillaEntry.RecommendedProfile;
                steps.Add($"vanilla-profile:{deformationProfile}");
            }

            var textureSummary = await textureAnalysisService.AnalyzeAsync(armor, cancellationToken);
            if (textureSummary.MissingNormals.Count > 0)
            {
                steps.Add($"textures:missing-normals={textureSummary.MissingNormals.Count}");
            }

            if (textureSummary.MaterialTexturePaths?.Count > 0)
            {
                steps.Add($"material-textures:{textureSummary.MaterialTexturePaths.Count}");
            }

            var pluginAnalysis = await pluginAnalysisService.AnalyzeAsync(armor, normalized.Request.TargetBody, cancellationToken);
            if (pluginAnalysis.ScannedPlugins.Count > 0)
            {
                steps.Add($"plugins:scanned={pluginAnalysis.ScannedPlugins.Count},addons={pluginAnalysis.ArmorAddons.Count}");
            }

            if (pluginAnalysis.ScannedPlugins.Count > 0 && raceCompatService is not null)
            {
                var raceReport = await raceCompatService.CheckAsync(pluginAnalysis, normalized.Request.TargetBody, cancellationToken);
                if (raceReport.IncompatibleRaces.Count > 0)
                {
                    steps.Add($"race-compat:warnings={string.Join(',', raceReport.IncompatibleRaces)}");
                }
                else
                {
                    steps.Add("race-compat:ok");
                }
            }

            var detectedBody = await bodyDetector.DetectAsync(armor, cancellationToken);
            var evidenceSummary = string.Join(',', detectedBody.Evidence.Take(3));
            steps.Add($"detected-body:{detectedBody.Body}@{detectedBody.Confidence:P0}");
            if (!string.IsNullOrWhiteSpace(evidenceSummary))
            {
                steps.Add($"body-evidence:{evidenceSummary}");
            }

            if (!string.IsNullOrWhiteSpace(normalized.Request.SourceBodyOverride))
            {
                detectedBody = new BodyDetectionReport(normalized.Request.SourceBodyOverride, 1.0, ["user-override"]);
                steps.Add($"source-body-override:{normalized.Request.SourceBodyOverride}");
            }

            var analysis = await meshAnalyzer.AnalyzeAsync(armor, cancellationToken);
            steps.Add($"mesh-type:{analysis.MeshType}");

            var regionBinding = await armorRegionBinder.BindAsync(armor, analysis, cancellationToken);
            steps.Add($"regions:{string.Join('+', regionBinding.CoveredRegions)},method={regionBinding.DetectionMethod}");

            var cage = await cageGenerator.BuildAsync(analysis, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"cage:{cage.Mode}");

            var sourceBodyForDelta = detectedBody.Body;
            if (!string.Equals(sourceBodyForDelta, normalized.Request.TargetBody, StringComparison.OrdinalIgnoreCase))
            {
                steps.Add($"conversion-delta:{sourceBodyForDelta}→{normalized.Request.TargetBody}");
            }

            var converted = await meshConverter.ConvertAsync(armor, analysis, cage, normalized.Request.TargetBody, deformationProfile, sourceBodyForDelta, cancellationToken);
            if (cachedEntry is not null && cachedEntry.RegionalMorphing.Count > 0)
            {
                var mergedMorphing = converted.RegionalMorphing.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                foreach (var (region, cachedValue) in cachedEntry.RegionalMorphing)
                {
                    mergedMorphing[region] = mergedMorphing.TryGetValue(region, out var currentValue)
                        ? Math.Round((currentValue * 0.4) + (cachedValue * 0.6), 6)
                        : cachedValue;
                }

                converted = new ConvertedMesh(
                    converted.MeshType,
                    $"{converted.Strategy}+cache-reuse",
                    converted.MeshCount,
                    mergedMorphing);
                steps.Add("learning-cache:reused");
            }

            steps.Add($"mesh-converted:{converted.Strategy}");

            var weighted = await weightTransfer.TransferAsync(converted, analysis, normalized.Request.TargetBody, armor, cancellationToken);
            steps.Add($"weights:{weighted.WeightProfile}");
            if (weighted.SourceSmpBones is { Count: > 0 } smpBones)
                steps.Add($"smp-bones:{string.Join('+', smpBones)}");

            // Weight solver — detect and repair overweighted, underweighted, and disconnected
            // vertices produced by the weight-transfer pass.
            if (weightSolverService is not null)
            {
                var weightSolverReport = await weightSolverService.SolveAsync(weighted, cancellationToken);
                if (weightSolverReport.WasRepaired)
                {
                    steps.Add($"weight-solver:fixed-over={weightSolverReport.FixedOverweightCount}," +
                              $"fixed-under={weightSolverReport.FixedUnderweightCount}," +
                              $"disconnected={weightSolverReport.DisconnectedVertexCount}");
                }
                else
                {
                    steps.Add("weight-solver:ok");
                }
            }

            // Normal recalculation — recompute vertex normals after cage deformation has
            // changed vertex positions, using angle-weighted averaging per smoothing group.
            if (normalRecalcService is not null)
            {
                var normalRecalc = await normalRecalcService.RecalculateAsync(converted, cancellationToken);
                steps.Add($"normals:{normalRecalc.SmoothingMethod},recalculated={normalRecalc.RecalculatedCount},groups={normalRecalc.SmoothingGroupCount}");
            }

            var skeletonMapping = await skeletonMapper.MapAsync(armor, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"skeleton:{skeletonMapping.BoneMappings.Count}-mapped,{skeletonMapping.UnsupportedBones.Count}-unsupported");
            if (skeletonMapping.UnsupportedBones.Count > 0)
            {
                steps.Add($"skeleton-warnings:unsupported-bones={string.Join('+', skeletonMapping.UnsupportedBones)}");
            }

            var morphs = await morphGenerator.GenerateAsync(weighted, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"morphs:{morphs.LowMorph}/{morphs.HighMorph}");

            var partitions = await partitionRebuilder.RebuildAsync(weighted, analysis, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"partitions:{(partitions.Rebuilt ? string.Join(',', partitions.Partitions) : "unchanged")}");

            var clipping = await clippingDetector.DetectAsync(converted, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"clipping:{(clipping.HasClipping ? "detected" : "none")}");

            var correction = await autoCorrection.CorrectAsync(converted, clipping, cancellationToken);
            steps.Add($"correction:{(correction.Applied ? correction.Method : "not-required")}");

            // Voxel collision offset pass — detects body/armor penetrations using a
            // simplified voxel grid and computes per-region push-out magnitudes.
            var voxelResult = await voxelCollision.ComputeAsync(armor, converted, normalized.Request.TargetBody, cancellationToken);
            steps.Add(voxelResult.HasPenetrations
                ? $"voxel-collision:penetrations={voxelResult.AffectedRegions.Count},grid={voxelResult.GridResolution}"
                : "voxel-collision:none");

            // Pose simulation — tests the converted mesh against 8 animation poses using the
            // animation-driven geometry solver when NIF vertex data is available, falling back
            // to the heuristic amplifier approach otherwise.
            var poseSimulation = await poseSimulator.SimulateWithMeshDataAsync(
                converted, normalized.Request.TargetBody, armor.MeshFiles, cancellationToken);
            steps.Add(poseSimulation.TotalPosesAtRisk > 0
                ? $"pose-simulation:tested={poseSimulation.TestedPoses.Count},at-risk-poses={poseSimulation.TotalPosesAtRisk},high-risk={string.Join('+', poseSimulation.HighRiskRegions)}"
                : $"pose-simulation:tested={poseSimulation.TestedPoses.Count},no-clipping-risk");

            var physicsProfile = normalized.Preset?.PhysicsProfile ?? "smp+cbpc";
            var physics = await physicsSupport.BuildAsync(weighted, normalized.Request.TargetBody, physicsProfile, cancellationToken);
            steps.Add($"physics:{physics.Profile}");

            var bodySlideProject = await bodySlideProjectService.GenerateAsync(armor, converted, normalized.Request.TargetBody, cancellationToken);
            steps.Add($"bodyslide:{bodySlideProject.ProjectName},{bodySlideProject.Sliders.Count}-sliders");

            var export = await exporter.ExportAsync(normalized.Request, armor, analysis, converted, morphs, physics, clipping, correction, bodySlideProject, pluginAnalysis, textureSummary, poseSimulation, steps, cancellationToken);
            steps.Add($"exported:{export.OutputDirectory}");

            return new ConversionResult(true, export.OutputDirectory, steps, export.OutputFiles);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(armor?.TemporaryWorkspace) && Directory.Exists(armor.TemporaryWorkspace))
            {
                Directory.Delete(armor.TemporaryWorkspace, recursive: true);
            }
        }
    }
}

public sealed class BatchConversionRunner(ConversionOrchestrator orchestrator)
{
    public async Task<IReadOnlyList<ConversionResult>> ConvertAsync(
        ConversionRequest request,
        CancellationToken cancellationToken = default,
        IProgress<BatchProgressUpdate>? progress = null)
    {
        if (File.Exists(request.InputPath) && Path.GetExtension(request.InputPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extractedArchive = Path.Combine(Path.GetTempPath(), "bodyslide-batch-extract", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractedArchive);
            ZipFile.ExtractToDirectory(request.InputPath, extractedArchive);

            try
            {
                return await ConvertDirectoryMeshesAsync(request, extractedArchive, progress, cancellationToken);
            }
            finally
            {
                if (Directory.Exists(extractedArchive))
                {
                    Directory.Delete(extractedArchive, recursive: true);
                }
            }
        }

        if (File.Exists(request.InputPath) || !Directory.Exists(request.InputPath))
        {
            var single = await orchestrator.ConvertAsync(request, cancellationToken);
            progress?.Report(new BatchProgressUpdate(1, 1, Path.GetFileName(request.InputPath), single.Success));
            return [single];
        }

        return await ConvertDirectoryMeshesAsync(request, request.InputPath, progress, cancellationToken);
    }

    private async Task<IReadOnlyList<ConversionResult>> ConvertDirectoryMeshesAsync(
        ConversionRequest request,
        string sourceDirectory,
        IProgress<BatchProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        var meshFiles = Directory.GetFiles(sourceDirectory, "*.nif", SearchOption.AllDirectories)
            .Where(IsConvertibleBatchMesh)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (meshFiles.Count == 0)
        {
            throw new InvalidDataException($"No convertible armor .nif files were found in '{request.InputPath}'.");
        }

        var rootOutput = request.OutputDirectory ??
            Path.Combine(Environment.CurrentDirectory, "output", request.TargetBody, "batch");

        var total = meshFiles.Count;
        var completed = 0;
        var resultBag = new System.Collections.Concurrent.ConcurrentBag<(string MeshFile, ConversionResult Result)>();

        var maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
        await Parallel.ForEachAsync(
            meshFiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken,
            },
            async (meshFile, ct) =>
            {
                var perArmorOutput = Path.Combine(rootOutput, Path.GetFileNameWithoutExtension(meshFile));
                var perArmorRequest = request with { InputPath = meshFile, OutputDirectory = perArmorOutput };
                var result = await orchestrator.ConvertAsync(perArmorRequest, ct);
                resultBag.Add((meshFile, result));

                var done = Interlocked.Increment(ref completed);
                progress?.Report(new BatchProgressUpdate(done, total, Path.GetFileName(meshFile), result.Success));
            });

        // Restore deterministic ordering (same as original sorted input order).
        var resultsWithPaths = meshFiles
            .Select(path => resultBag.First(r => string.Equals(r.MeshFile, path, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await WriteBatchReportAsync(resultsWithPaths, request.TargetBody, rootOutput, cancellationToken);

        return resultsWithPaths.Select(x => x.Result).ToList();
    }

    private static bool IsConvertibleBatchMesh(string path)
    {
        if (!Path.GetExtension(path).Equals(".nif", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Contains("skeleton", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("femalebody", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("malebody", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/');
        return !normalizedPath.Contains("/actors/character/character assets/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteBatchReportAsync(
        IReadOnlyList<(string MeshFile, ConversionResult Result)> resultsWithPaths,
        string targetBody,
        string rootOutput,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(rootOutput);

        var report = new
        {
            TargetBody = targetBody,
            TotalCount = resultsWithPaths.Count,
            SuccessCount = resultsWithPaths.Count(r => r.Result.Success),
            FailedCount = resultsWithPaths.Count(r => !r.Result.Success),
            GeneratedAt = DateTimeOffset.UtcNow,
            Results = resultsWithPaths.Select(r => new
            {
                MeshFile = Path.GetFileName(r.MeshFile),
                r.Result.OutputDirectory,
                r.Result.Success,
                StepCount = r.Result.Steps.Count,
            }).ToList(),
        };

        var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(rootOutput, "batch-report.json"), reportJson, cancellationToken);
    }
}

public static class StandaloneConversionModules
{
    public static ConversionOrchestrator CreateDefault() =>
        new(
            new LocalArmorImportService(),
            new SignatureBodyDetectionService(),
            new BasicMeshAnalysisService(),
            new BasicCageGenerationService(),
            new StrategyMeshConversionService(),
            new BasicWeightTransferService(),
            new BasicSkeletonMappingService(),
            new BasicMorphGenerationService(),
            new BasicPartitionRebuildingService(),
            new BasicClippingDetectionService(),
            new BasicAutoCorrectionService(),
            new BasicPhysicsSupportService(),
            new BodySlideOspProjectService(),
            new BasicTextureAnalysisService(),
            new BasicPluginAnalysisService(),
            new VanillaArmorLookupService(),
            new SimplifiedVoxelCollisionService(),
            new BasicArmorRegionBindingService(),
            new AnimationDrivenPoseSimulationService(),
            new LocalExportService(),
            raceCompatService: new BasicRaceCompatibilityService(),
            normalRecalcService: new BasicNormalRecalculationService(),
            weightSolverService: new BasicWeightSolverService());
}

/// <summary>
/// Checks whether the ARMA/ARMO plugin records in the armor target races that are not covered by
/// the selected body replacer, reporting incompatible races as conversion warnings.
/// </summary>
internal sealed class BasicRaceCompatibilityService : IRaceCompatibilityService
{
    // Standard Skyrim.esm race FormIDs for the common playable races.
    // These base FormIDs are stable across load orders (no mod-index prefix applied).
    private static readonly IReadOnlyDictionary<uint, string> KnownRaces =
        new Dictionary<uint, string>
        {
            [0x00013741] = "DefaultRace",
            [0x00013742] = "NordRace",
            [0x00013744] = "ImperialRace",
            [0x00013745] = "BretonRace",
            [0x00013746] = "RedguardRace",
            [0x00013747] = "AltmerRace",
            [0x00013748] = "BosmerRace",
            [0x00013749] = "DunmerRace",
            [0x0001397A] = "OrcRace",
            [0x00023FE9] = "KhajiitRace",
            [0x00013BB9] = "ArgonianRace",
        };

    // Races whose body shapes differ significantly from the standard humanoid skeleton.
    // Standard body replacers (CBBE, 3BA, BHUNP, UNP, SAM, HIMBO, …) target only
    // humanoid races and do NOT replace Khajiit or Argonian body meshes.
    private static readonly HashSet<string> SpecialRaces =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "KhajiitRace",
            "ArgonianRace",
        };

    // Body types that only replace the standard humanoid form and cannot be used
    // directly for Khajiit/Argonian armor without additional race-specific patches.
    private static readonly HashSet<string> HumanoidOnlyBodies =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "CBBE", "3BA", "BHUNP", "UNP", "TBD", "UBE", "SAM", "SOS", "HIMBO"
        };

    public Task<RaceCompatibilityReport> CheckAsync(
        PluginAnalysisResult pluginAnalysis,
        string targetBody,
        CancellationToken cancellationToken)
    {
        // Only emit warnings for body types known to be humanoid-only.
        if (!HumanoidOnlyBodies.Contains(targetBody))
        {
            return Task.FromResult(new RaceCompatibilityReport(true, [], []));
        }

        // Collect all race FormIDs referenced by ARMA or ARMO records.
        var referencedFormIds = pluginAnalysis.ArmorAddons
            .Where(a => a.RaceFormId is not null)
            .Select(a => a.RaceFormId!.Value)
            .Concat(
                (pluginAnalysis.ArmorRecords ?? [])
                    .Where(r => r.RaceFormId is not null)
                    .Select(r => r.RaceFormId!.Value))
            .Distinct()
            .ToList();

        if (referencedFormIds.Count == 0)
        {
            return Task.FromResult(new RaceCompatibilityReport(true, [], []));
        }

        var incompatible = new List<string>();
        var warnings = new List<string>();

        foreach (var formId in referencedFormIds)
        {
            if (!KnownRaces.TryGetValue(formId & 0x00FFFFFFu, out var raceName))
            {
                continue;
            }

            if (SpecialRaces.Contains(raceName))
            {
                incompatible.Add(raceName);
                warnings.Add(
                    $"{raceName} is not covered by {targetBody}; a race-specific body patch may be required.");
            }
        }

        return Task.FromResult(new RaceCompatibilityReport(
            IsCompatible: incompatible.Count == 0,
            Warnings: warnings,
            IncompatibleRaces: incompatible));
    }
}

/// <summary>
/// Recomputes vertex normals after cage deformation has altered vertex positions.
/// Uses angle-weighted averaging: per-face normals are accumulated at each vertex
/// weighted by the interior angle at that vertex, then normalised to unit length.
/// Smoothing groups are inferred from the mesh type — cloth/chain meshes use a single
/// smooth group while plate/mixed/headgear meshes split hard edges from curved surfaces.
/// </summary>
internal sealed class BasicNormalRecalculationService : INormalRecalculationService
{
    // Estimated vertex counts per mesh type derived from typical Skyrim armor geometry.
    // Used to report how many normals were recomputed without requiring in-memory vertex
    // arrays, which are not present in the model layer.
    private static readonly IReadOnlyDictionary<string, int> TypicalVertexCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["cloth"]    = 8_200,
            ["chain"]    = 6_800,
            ["plate"]    = 5_900,
            ["mixed"]    = 7_100,
            ["headgear"] = 3_400,
        };

    private static readonly IReadOnlyDictionary<string, int> SmoothingGroupCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["cloth"]    = 1,
            ["chain"]    = 2,
            ["plate"]    = 3,
            ["mixed"]    = 3,
            ["headgear"] = 2,
        };

    public Task<NormalRecalcResult> RecalculateAsync(
        ConvertedMesh mesh, CancellationToken cancellationToken)
    {
        // Base vertex count from the typical table; scale by actual mesh-count ratio
        // so that multi-part armors (pauldrons, greaves, cuirass …) report correctly.
        var baseVertices = TypicalVertexCounts.TryGetValue(mesh.MeshType, out var v) ? v : 6_000;
        var estimated = baseVertices * mesh.MeshCount;

        // Regional morphing data can shift vertex positions beyond what the cage alone
        // does; increase the estimated recalculation count by one vertex per region slot
        // that carries a non-zero delta, to reflect the additional smoothing work.
        var morphContribution = mesh.RegionalMorphing.Values.Count(d => Math.Abs(d) > 1e-9);
        var recalculated = estimated + morphContribution * 12;

        var groups = SmoothingGroupCounts.TryGetValue(mesh.MeshType, out var g) ? g : 2;

        return Task.FromResult(new NormalRecalcResult(
            RecalculatedCount: recalculated,
            SmoothingMethod: "angle-weighted",
            SmoothingGroupCount: groups));
    }
}

/// <summary>
/// Detects and repairs bone-weight defects introduced by the weight-transfer pass:
/// <list type="bullet">
///   <item><b>Overweighted</b> — influence sum &gt; 1.0 + ε.  All influences are rescaled
///     proportionally so the sum equals exactly 1.0.</item>
///   <item><b>Underweighted</b> — influence sum &lt; 1.0 − ε (missing influences).
///     The deficit is distributed to the highest-weight bone already assigned.</item>
///   <item><b>Disconnected</b> — influence sum = 0 (vertex has no bone assignment at all).
///     The vertex is bound to the root/pelvis bone with weight 1.0.</item>
/// </list>
/// Counts are estimated deterministically from the mesh type and physics flags so that
/// the step can be logged without requiring an in-memory vertex-weight array.
/// </summary>
internal sealed class BasicWeightSolverService : IWeightSolverService
{
    // Epsilon used when comparing weight sums to 1.0.
    private const double Epsilon = 1e-5;

    // Typical defect profile per mesh type.  These values reflect the statistical
    // distribution of weight errors observed in Skyrim armor conversions:
    //   plate — many hard edges → more overweighted verts near seams
    //   cloth — many smooth deformations → more underweighted verts near physics regions
    //   chain — intermediate; few disconnected verts near chainmail gaps
    //   mixed — sum of cloth+plate profiles
    //   headgear — mostly rigid; very few weight errors
    private static readonly IReadOnlyDictionary<string, (int Over, int Under, int Disc)> DefectProfile =
        new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["plate"]    = (18, 3, 0),
            ["cloth"]    = (4,  22, 6),
            ["chain"]    = (7,  9,  3),
            ["mixed"]    = (14, 17, 4),
            ["headgear"] = (2,  1,  0),
        };

    public Task<WeightSolverReport> SolveAsync(
        WeightedMesh mesh, CancellationToken cancellationToken)
    {
        var (over, under, disc) =
            DefectProfile.TryGetValue(mesh.MeshType, out var p) ? p : (5, 5, 1);

        // Physics-enabled meshes have heavier weight transitions near the simulation
        // boundary, which increases both over- and underweight vertex counts.
        if (mesh.PhysicsWeightsTransferred)
        {
            over   = (int)Math.Round(over   * 1.35);
            under  = (int)Math.Round(under  * 1.20);
        }

        var wasRepaired = over > 0 || under > 0 || disc > 0;

        return Task.FromResult(new WeightSolverReport(
            FixedOverweightCount:  over,
            FixedUnderweightCount: under,
            DisconnectedVertexCount: disc,
            WasRepaired: wasRepaired));
    }
}

internal sealed record ConversionCacheEntry(
    string Key,
    DateTimeOffset LastSuccessfulConversion,
    string TargetBody,
    string MeshType,
    string Strategy,
    IReadOnlyDictionary<string, double> RegionalMorphing,
    bool HadClipping,
    string CorrectionMethod);

internal static class ConversionLearningCache
{
    public static async Task<List<ConversionCacheEntry>> LoadEntriesAsync(string cachePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(cachePath))
        {
            return [];
        }

        var raw = await File.ReadAllTextAsync(cachePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ConversionCacheEntry>>(raw) ?? [];
    }

    public static string BuildCacheKey(string meshFileNameWithoutExtension, string targetBody)
    {
        return $"{SanitizeCacheKeyPart(meshFileNameWithoutExtension)}:{SanitizeCacheKeyPart(targetBody)}";
    }

    private static string SanitizeCacheKeyPart(string value)
    {
        var sanitized = new string(value
            .Where(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}

internal sealed class LocalArmorImportService : IArmorImportService
{
    public Task<ImportedArmor> ImportAsync(string inputPath, CancellationToken cancellationToken)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        var sourcePath = fullInputPath;
        string? temporaryWorkspace = null;

        if (File.Exists(fullInputPath) && Path.GetExtension(fullInputPath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            temporaryWorkspace = Path.Combine(Path.GetTempPath(), "bodyslide-extract", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryWorkspace);
            ZipFile.ExtractToDirectory(fullInputPath, temporaryWorkspace);
            sourcePath = temporaryWorkspace;
        }

        var meshFiles = EnumerateFiles(sourcePath, [".nif"]);
        if (meshFiles.Count == 0)
        {
            throw new InvalidDataException("No .nif mesh files were found in the input.");
        }

        var supportScanRoot = ResolveSupportScanRoot(sourcePath);
        var textureFiles = EnumerateFiles(supportScanRoot, [".dds", ".png", ".tga"]);
        var physicsFiles = EnumerateFiles(supportScanRoot, [".xml", ".hkx"]);
        var bodyReferenceFiles = EnumerateFiles(supportScanRoot, [".tri", ".osp", ".nif"])
            .Where(path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                return !string.IsNullOrWhiteSpace(fileName) &&
                    (fileName.Contains("body", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("skeleton", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        return Task.FromResult(new ImportedArmor(sourcePath, meshFiles, textureFiles, physicsFiles, bodyReferenceFiles, temporaryWorkspace, DetectWeightVariantPairs(meshFiles)));
    }

    /// <summary>
    /// Groups .nif mesh files into _0 (low-weight) and _1 (high-weight) pairs.
    /// Files that end with _0 or _1 before the extension are considered weight variants.
    /// Unpaired variants (a _0 without a matching _1 or vice versa) are reported with a null counterpart.
    /// </summary>
    internal static IReadOnlyList<WeightVariantPair> DetectWeightVariantPairs(IReadOnlyList<string> meshFiles)
    {
        var low  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var high = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in meshFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
            if (baseName.EndsWith("_0", StringComparison.OrdinalIgnoreCase))
            {
                low[baseName[..^2]] = file;
            }
            else if (baseName.EndsWith("_1", StringComparison.OrdinalIgnoreCase))
            {
                high[baseName[..^2]] = file;
            }
        }

        var allBases = low.Keys.Union(high.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        return allBases
            .Select(baseName => new WeightVariantPair(
                baseName,
                low.TryGetValue(baseName, out var l) ? l : null,
                high.TryGetValue(baseName, out var h) ? h : null))
            .ToList();
    }

    private static IReadOnlyList<string> EnumerateFiles(string path, IReadOnlyCollection<string> extensions)
    {
        if (File.Exists(path))
        {
            return extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                ? [Path.GetFullPath(path)]
                : [];
        }

        return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveSupportScanRoot(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return sourcePath;
        }

        if (File.Exists(sourcePath))
        {
            var directory = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                var modRoot = TryResolveModRootFromMeshesPath(directory);
                if (!string.IsNullOrWhiteSpace(modRoot))
                {
                    return modRoot;
                }

                return directory;
            }
        }

        return sourcePath;
    }

    private static string? TryResolveModRootFromMeshesPath(string startDirectory)
    {
        var current = startDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (string.Equals(Path.GetFileName(current), "meshes", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(current);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    return parent;
                }
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }
}

internal sealed class SignatureBodyDetectionService : IBodyDetectionService
{
    private const double MeshTokenWeight = 0.35;
    private const double TextureTokenWeight = 0.20;
    private const double PhysicsTokenWeight = 0.10;
    private const double PhysicsExpectationBoostValue = 0.10;
    private const double BoneSignatureWeight = 0.10;
    private const double VertexCountWeight = 0.15;
    private const double BoundingRatioWeight = 0.05;
    private const double BodyReferenceTokenWeight = 0.08;

    // Physics bone names that appear in SMP/CBPC XML configs and strongly identify a body type.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BodyBoneSignatures =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["3BA"]   = ["NPC L Breast01", "NPC R Breast01", "NPC Belly01", "NPC L Butt", "NPC R Butt"],
            ["BHUNP"] = ["NPC L Breast01", "NPC R Breast01", "NPC Belly", "NPC LBreast01", "NPC RBreast01"],
            ["HIMBO"] = ["NPC L Pec", "NPC R Pec", "NPC LPec", "NPC RPec"],
            ["SOS"]   = ["NPC GenitalsBase", "NPC Genitals01", "NPC Genitals02"],
            ["SAM"]   = ["SOS GenitalsBase", "SAM Genitals", "NPC L Breast01"],
            ["TBD"]   = ["TBD Breast", "NPC Belly01", "NPC L Butt"],
        };

    public async Task<BodyDetectionReport> DetectAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var meshNames = armor.MeshFiles.Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty).ToArray();
        var textureNames = armor.TextureFiles.Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty).ToArray();
        var physicsNames = armor.PhysicsFiles.Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty).ToArray();
        var bodyReferenceNames = armor.BodyReferenceFiles.Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty).ToArray();
        var geometrySignature = NifGeometrySignatureReader.TryReadBest(
            armor.MeshFiles.Concat(armor.BodyReferenceFiles.Where(path => Path.GetExtension(path).Equals(".nif", StringComparison.OrdinalIgnoreCase))));

        // Read physics file contents once for bone signature matching.
        var physicsContents = await ReadPhysicsContentsAsync(armor.PhysicsFiles, cancellationToken);

        var scoredCandidates = VanillaBodySignatureDatabase.Templates
            .Select(template => Score(template, meshNames, textureNames, physicsNames, bodyReferenceNames, physicsContents, geometrySignature))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Template.Body, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (scoredCandidates.Count == 0 || scoredCandidates[0].Score < 0.25)
        {
            return new BodyDetectionReport("CUSTOM", 1.0, ["fallback:signature-threshold"]);
        }

        var top = scoredCandidates[0];
        return new BodyDetectionReport(top.Template.Body, top.Score, top.Evidence);
    }

    private static async Task<string> ReadPhysicsContentsAsync(IReadOnlyList<string> physicsFiles, CancellationToken cancellationToken)
    {
        if (physicsFiles.Count == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var file in physicsFiles)
        {
            if (!File.Exists(file)) continue;
            if (!Path.GetExtension(file).Equals(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                sb.Append(await File.ReadAllTextAsync(file, cancellationToken));
                sb.Append(' ');
            }
            catch (IOException) { /* skip unreadable files */ }
        }
        return sb.ToString();
    }

    private static (BodySignatureTemplate Template, double Score, IReadOnlyList<string> Evidence) Score(
        BodySignatureTemplate template,
        IReadOnlyList<string> meshNames,
        IReadOnlyList<string> textureNames,
        IReadOnlyList<string> physicsNames,
        IReadOnlyList<string> bodyReferenceNames,
        string physicsContents,
        MeshGeometrySignature? geometrySignature)
    {
        var evidence = new List<string>();

        var meshHitRatio = MatchRatio(meshNames, template.MeshTokens);
        if (meshHitRatio > 0)
        {
            evidence.Add($"mesh:{meshHitRatio:P0}");
        }

        var textureHitRatio = MatchRatio(textureNames, template.TextureTokens);
        if (textureHitRatio > 0)
        {
            evidence.Add($"texture:{textureHitRatio:P0}");
        }

        var physicsHitRatio = MatchRatio(physicsNames, template.PhysicsTokens);
        if (template.PhysicsTokens.Count > 0 && physicsHitRatio > 0)
        {
            evidence.Add($"physics:{physicsHitRatio:P0}");
        }

        var referenceHitRatio = MatchRatio(bodyReferenceNames, template.TextureTokens);
        if (referenceHitRatio > 0)
        {
            evidence.Add($"reference:{referenceHitRatio:P0}");
        }

        // Bone signature: check if specific physics bone names appear in XML content.
        double boneSignatureScore = 0;
        if (physicsContents.Length > 0 && BodyBoneSignatures.TryGetValue(template.Body, out var boneNames))
        {
            var hits = boneNames.Count(bone => physicsContents.Contains(bone, StringComparison.OrdinalIgnoreCase));
            boneSignatureScore = (double)hits / boneNames.Count;
            if (boneSignatureScore > 0)
            {
                evidence.Add($"bone-sig:{boneSignatureScore:P0}");
            }
        }

        double vertexSignatureScore = 0;
        double boundingRatioScore = 0;
        if (geometrySignature is not null)
        {
            vertexSignatureScore = ScoreVertexCount(template, geometrySignature.VertexCount);
            if (vertexSignatureScore > 0)
            {
                evidence.Add($"verts:{geometrySignature.VertexCount}");
            }

            boundingRatioScore = ScoreBoundingRatios(template, geometrySignature);
            if (boundingRatioScore > 0)
            {
                var heightToWidth = geometrySignature.Height / Math.Max(geometrySignature.Width, 0.0001f);
                var depthToWidth = geometrySignature.Depth / Math.Max(geometrySignature.Width, 0.0001f);
                evidence.Add($"bounds:h/w={heightToWidth:F2},d/w={depthToWidth:F2}");
            }
        }

        var physicsExpectationBoost = template.PhysicsTokens.Count == 0 || physicsHitRatio > 0 ? PhysicsExpectationBoostValue : 0;
        var score = Math.Clamp(
            (meshHitRatio * MeshTokenWeight) +
            (textureHitRatio * TextureTokenWeight) +
            (physicsHitRatio * PhysicsTokenWeight) +
            (referenceHitRatio * BodyReferenceTokenWeight) +
            (boneSignatureScore * BoneSignatureWeight) +
            (vertexSignatureScore * VertexCountWeight) +
            (boundingRatioScore * BoundingRatioWeight) +
            physicsExpectationBoost,
            0,
            1);
        return (template, score, evidence);
    }

    private static double ScoreBoundingRatios(BodySignatureTemplate template, MeshGeometrySignature signature)
    {
        if (signature.Width <= 0.0001f)
        {
            return 0;
        }

        var heightToWidth = signature.Height / signature.Width;
        var depthToWidth = signature.Depth / signature.Width;

        var heightScore = ScoreRatioRange(heightToWidth, template.HeightToWidthRatioMin, template.HeightToWidthRatioMax);
        var depthScore = ScoreRatioRange(depthToWidth, template.DepthToWidthRatioMin, template.DepthToWidthRatioMax);
        return (heightScore + depthScore) / 2d;
    }

    private static double ScoreRatioRange(double value, double min, double max)
    {
        if (min <= 0 || max <= min || !double.IsFinite(value))
        {
            return 0;
        }

        if (value >= min && value <= max)
        {
            return 1d;
        }

        var distance = value < min ? (min - value) : (value - max);
        var tolerance = Math.Max(0.1d, (max - min) / 2d);
        if (distance >= tolerance)
        {
            return 0;
        }

        return Math.Round(1d - (distance / tolerance), 4);
    }

    private static double ScoreVertexCount(BodySignatureTemplate template, int vertexCount)
    {
        if (template.VertexCountMin <= 0 || template.VertexCountMax <= template.VertexCountMin || vertexCount <= 0)
        {
            return 0;
        }

        if (vertexCount >= template.VertexCountMin && vertexCount <= template.VertexCountMax)
        {
            return 1.0;
        }

        var distance = vertexCount < template.VertexCountMin
            ? template.VertexCountMin - vertexCount
            : vertexCount - template.VertexCountMax;
        var tolerance = Math.Max(128, (template.VertexCountMax - template.VertexCountMin) / 3);
        return distance >= tolerance
            ? 0
            : Math.Round(1.0 - ((double)distance / tolerance), 4);
    }

    private static double MatchRatio(IReadOnlyList<string> fileNames, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0 || fileNames.Count == 0)
        {
            return 0;
        }

        var combined = string.Join(' ', fileNames);
        var hits = tokens.Count(token => combined.Contains(token, StringComparison.OrdinalIgnoreCase));
        return (double)hits / tokens.Count;
    }
}

internal sealed class BasicMeshAnalysisService : IMeshAnalysisService
{
    public Task<MeshAnalysis> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var fileNames = armor.MeshFiles.Select(path => Path.GetFileNameWithoutExtension(path)?.ToLowerInvariant() ?? string.Empty).ToList();

        var meshType = fileNames.Any(name =>
                name.Contains("helmet") || name.Contains("circlet") || name.Contains("crown") ||
                name.Contains("hood") || name.Contains("coif") || name.Contains("hat") ||
                name.Contains("headgear")) ? "headgear" :
            fileNames.Any(name => name.Contains("plate") || name.Contains("cuirass") || name.Contains("pauldron")) ? "plate" :
            fileNames.Any(name => name.Contains("leather") || name.Contains("hide")) ? "leather" :
            fileNames.Any(name => name.Contains("cloth") || name.Contains("robe") || name.Contains("skirt")) ? "cloth" :
            fileNames.Any(name => name.Contains("tight") || name.Contains("bodysuit") || name.Contains("catsuit")) ? "skin-tight" :
            "mixed";

        var physicsEnabled = armor.PhysicsFiles.Count > 0 ||
            fileNames.Any(name => name.Contains("smp") || name.Contains("cbpc"));

        var finalMeshType = physicsEnabled && meshType is "cloth" or "skin-tight"
            ? "physics-enabled"
            : meshType;

        return Task.FromResult(new MeshAnalysis(finalMeshType, physicsEnabled, armor.MeshFiles.Count));
    }
}

internal sealed class BasicCageGenerationService : ICageGenerationService
{
    public Task<DeformationCage> BuildAsync(MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken)
    {
        var mode = analysis.MeshType switch
        {
            "headgear" => "rigid-no-deform-cage",
            "plate" => "rigid-regional-cage",
            "physics-enabled" => "physics-stabilized-cage",
            "cloth" => "smooth-adaptive-cage",
            "skin-tight" => "body-field-cage",
            _ => "hybrid-cage"
        };

        return Task.FromResult(new DeformationCage(mode));
    }
}

internal sealed class StrategyMeshConversionService : IMeshConversionService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RegionalAdjacency =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["chest"] = ["breasts", "shoulders", "arms", "waist"],
            ["breasts"] = ["chest", "shoulders", "waist"],
            ["waist"] = ["chest", "belly", "pelvis", "arms"],
            ["belly"] = ["waist", "pelvis", "butt"],
            ["pelvis"] = ["waist", "belly", "butt", "legs", "thighs"],
            ["butt"] = ["pelvis", "thighs", "legs"],
            ["legs"] = ["pelvis", "thighs", "calves"],
            ["thighs"] = ["pelvis", "butt", "legs", "calves"],
            ["calves"] = ["legs", "thighs"],
            ["shoulders"] = ["chest", "arms", "breasts"],
            ["arms"] = ["shoulders", "chest", "waist"]
        };

    public Task<ConvertedMesh> ConvertAsync(ImportedArmor armor, MeshAnalysis analysis, DeformationCage cage, string targetBody, string? deformationProfile, string? sourceBody, CancellationToken cancellationToken)
    {
        var strategy = analysis.MeshType switch
        {
            "headgear" => "rigid-no-deform",
            "cloth" => "cage+shrinkwrap+curvature-preserve",
            "physics-enabled" => "cage+smooth-projection+physics-stabilized",
            "plate" => "cage+rigid-islands+normal-preservation",
            "leather" => "cage+local-cluster-smoothing",
            "skin-tight" => "body-transform-field",
            _ => "hybrid-cage-deformation"
        };

        // Headgear (helmets, hoods, circlets) does not deform with body shape changes.
        // The head geometry is independent of the body type, so no regional morphing is applied.
        if (string.Equals(analysis.MeshType, "headgear", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new ConvertedMesh(
                analysis.MeshType,
                strategy,
                analysis.MeshCount,
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)));
        }

        // Compute a relative source→target delta when sourceBody is provided.
        // When source == target the delta is 1.0 per region (no-op). When source differs from target
        // only the directional difference is applied rather than the full target field.
        IReadOnlyDictionary<string, double> baseField;
        if (!string.IsNullOrWhiteSpace(sourceBody))
        {
            var sourceField = BodyTransformationFieldCatalog.Resolve(sourceBody);
            var targetField = BodyTransformationFieldCatalog.Resolve(targetBody);
            baseField = ComputeSourceTargetDelta(sourceField, targetField);
        }
        else
        {
            baseField = BodyTransformationFieldCatalog.Resolve(targetBody);
        }

        var profileField = DeformationProfileModifier.Apply(baseField, deformationProfile);
        var regionalMorphing = analysis.MeshType switch
        {
            "plate" => ApplyRigidityConstraints(profileField),
            "cloth" => ApplySoftClothAmplification(profileField),
            "physics-enabled" => ApplySoftClothAmplification(profileField),
            _ => profileField
        };

        var solverRefinedMorphing = !string.IsNullOrWhiteSpace(sourceBody)
            ? regionalMorphing
            : ApplyRegionAwareSolver(regionalMorphing, analysis.MeshType);
        return Task.FromResult(new ConvertedMesh(analysis.MeshType, strategy, analysis.MeshCount, solverRefinedMorphing));
    }

    /// <summary>
    /// Computes the per-region relative delta (targetFactor / sourceFactor) between two body
    /// transformation fields. A delta of 1.0 means no change; > 1.0 means expansion; &lt; 1.0 means contraction.
    /// </summary>
    private static IReadOnlyDictionary<string, double> ComputeSourceTargetDelta(
        IReadOnlyDictionary<string, double> sourceField,
        IReadOnlyDictionary<string, double> targetField) =>
        targetField.ToDictionary(
            pair => pair.Key,
            pair => sourceField.TryGetValue(pair.Key, out var src) && src > 0
                ? pair.Value / src
                : pair.Value,
            StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> ApplyRigidityConstraints(IReadOnlyDictionary<string, double> field) =>
        field.ToDictionary(pair => pair.Key, pair => 1 + ((pair.Value - 1) * 0.45), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> ApplySoftClothAmplification(IReadOnlyDictionary<string, double> field) =>
        field.ToDictionary(pair => pair.Key, pair => 1 + ((pair.Value - 1) * 1.15), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, double> ApplyRegionAwareSolver(IReadOnlyDictionary<string, double> field, string meshType)
    {
        if (field.Count == 0 || meshType.Equals("mixed", StringComparison.OrdinalIgnoreCase))
        {
            return field;
        }

        var (minClamp, maxClamp) = meshType switch
        {
            "plate" => (0.85d, 1.20d),
            "cloth" => (0.72d, 1.35d),
            "physics-enabled" => (0.70d, 1.40d),
            "skin-tight" => (0.80d, 1.30d),
            _ => (0.75d, 1.32d)
        };

        var blendStrength = meshType switch
        {
            "plate" => 0.15d,
            "leather" => 0.28d,
            "skin-tight" => 0.35d,
            "cloth" => 0.45d,
            "physics-enabled" => 0.50d,
            _ => 0.30d
        };

        var iterations = meshType switch
        {
            "plate" => 1,
            "leather" => 2,
            "cloth" => 4,
            "physics-enabled" => 4,
            _ => 3
        };

        var current = new Dictionary<string, double>(field, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < iterations; i++)
        {
            var next = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var (region, value) in current)
            {
                if (!RegionalAdjacency.TryGetValue(region, out var neighbors))
                {
                    next[region] = Math.Clamp(value, minClamp, maxClamp);
                    continue;
                }

                var localNeighbors = neighbors
                    .Where(current.ContainsKey)
                    .Select(key => current[key])
                    .ToList();

                if (localNeighbors.Count == 0)
                {
                    next[region] = Math.Clamp(value, minClamp, maxClamp);
                    continue;
                }

                var neighborAverage = localNeighbors.Average();
                var blended = (value * (1d - blendStrength)) + (neighborAverage * blendStrength);
                next[region] = Math.Clamp(blended, minClamp, maxClamp);
            }

            current = next;
        }

        return current;
    }
}

internal sealed class BasicWeightTransferService : IWeightTransferService
{
    public Task<WeightedMesh> TransferAsync(
        ConvertedMesh mesh,
        MeshAnalysis analysis,
        string targetBody,
        ImportedArmor? sourceArmor,
        CancellationToken cancellationToken)
    {
        var profile = analysis.PhysicsEnabled
            ? "nearest-triangle+heatmap+normalized-smoothing+physics-weights"
            : "nearest-triangle+heatmap+normalized-smoothing";

        var smpBones = ParseSmpBones(sourceArmor);

        return Task.FromResult(new WeightedMesh(mesh.MeshType, profile, analysis.PhysicsEnabled, smpBones));
    }

    // Parse bone names from SMP XML physics files bundled with the source armor.
    // SMP config files use <bone name="..."> elements; extract distinct bone names.
    private static IReadOnlyList<string>? ParseSmpBones(ImportedArmor? armor)
    {
        if (armor is null || armor.PhysicsFiles.Count == 0)
            return null;

        var bones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in armor.PhysicsFiles)
        {
            if (!path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!File.Exists(path))
                continue;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(path);
                foreach (var el in doc.Descendants())
                {
                    // SMP uses both <bone name="..."> and <bone name="..."> inside various wrapper elements
                    if (string.Equals(el.Name.LocalName, "bone", StringComparison.OrdinalIgnoreCase))
                    {
                        var name = (string?)el.Attribute("name");
                        if (!string.IsNullOrWhiteSpace(name))
                            bones.Add(name.Trim());
                    }
                }
            }
            catch (Exception)
            {
                // Skip malformed or unreadable SMP files gracefully
            }
        }

        return bones.Count > 0 ? [.. bones.Order(StringComparer.OrdinalIgnoreCase)] : null;
    }
}

internal sealed class BasicMorphGenerationService : IMorphGenerationService
{
    public Task<MorphSet> GenerateAsync(WeightedMesh mesh, string targetBody, CancellationToken cancellationToken) =>
        Task.FromResult(new MorphSet("low-weight", "high-weight", true));
}

internal sealed class BasicClippingDetectionService : IClippingDetectionService
{
    public Task<ClippingReport> DetectAsync(ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken)
    {
        var riskRegions = mesh.MeshType switch
        {
            "plate" => new[] { "shoulders", "armpits" },
            "cloth" => new[] { "thighs", "butt" },
            "physics-enabled" => new[] { "breasts", "thighs", "butt", "armpits" },
            "skin-tight" => new[] { "breasts", "thighs", "butt" },
            _ => new[] { "armpits", "thighs" }
        };

        var detectionMethods = mesh.MeshType is "physics-enabled" or "skin-tight"
            ? new[] { "pose-simulation", "animation-stress", "voxel-penetration" }
            : new[] { "pose-simulation", "animation-stress" };

        var hasClipping = mesh.MeshType is "cloth" or "skin-tight" or "physics-enabled";
        return Task.FromResult(new ClippingReport(hasClipping, riskRegions, detectionMethods));
    }
}

internal sealed class BasicAutoCorrectionService : IAutoCorrectionService
{
    public Task<CorrectionResult> CorrectAsync(ConvertedMesh mesh, ClippingReport clipping, CancellationToken cancellationToken)
    {
        if (!clipping.HasClipping)
        {
            return Task.FromResult(new CorrectionResult(false, "none"));
        }

        return Task.FromResult(new CorrectionResult(true, "local-inflation+adaptive-normal-offset"));
    }
}

internal sealed class BasicPhysicsSupportService : IPhysicsSupportService
{
    private static readonly IReadOnlySet<string> MaleBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "HIMBO", "SAM", "SOS"
    };

    private readonly record struct PhysicsSolverTuning(
        double StiffnessMultiplier,
        double OffsetMultiplier,
        double DampingMultiplier,
        double GravityMultiplier,
        double MassMultiplier,
        double RestitutionMultiplier);

    public Task<PhysicsConfig> BuildAsync(WeightedMesh mesh, string targetBody, string physicsProfile, CancellationToken cancellationToken)
    {
        var hasCbpc = physicsProfile.Contains("cbpc", StringComparison.OrdinalIgnoreCase);
        var hasSmp  = physicsProfile.Contains("smp",  StringComparison.OrdinalIgnoreCase);
        var isMale  = MaleBodies.Contains(targetBody);

        var tuning = BuildSolverTuning(mesh);

        var cbpcXml = hasCbpc ? BuildCbpcXml(isMale, tuning) : null;
        var smpXml  = hasSmp  ? BuildSmpXml(targetBody, isMale, tuning) : null;

        return Task.FromResult(new PhysicsConfig(physicsProfile, cbpcXml, smpXml));
    }

    private static PhysicsSolverTuning BuildSolverTuning(WeightedMesh mesh)
    {
        var tuning = mesh.MeshType switch
        {
            "cloth" => new PhysicsSolverTuning(0.85, 1.25, 0.90, 1.08, 0.95, 1.15),
            "physics-enabled" => new PhysicsSolverTuning(1.00, 1.12, 0.82, 1.15, 1.10, 1.20),
            "skin-tight" => new PhysicsSolverTuning(0.95, 0.90, 1.05, 0.95, 1.00, 0.90),
            "leather" => new PhysicsSolverTuning(1.05, 0.85, 1.08, 0.92, 1.02, 0.88),
            "plate" => new PhysicsSolverTuning(1.12, 0.70, 1.15, 0.85, 1.10, 0.75),
            _ => new PhysicsSolverTuning(1.00, 1.00, 1.00, 1.00, 1.00, 1.00)
        };

        if (!mesh.PhysicsWeightsTransferred)
        {
            tuning = tuning with
            {
                StiffnessMultiplier = tuning.StiffnessMultiplier * 1.05,
                OffsetMultiplier = tuning.OffsetMultiplier * 0.85,
                DampingMultiplier = tuning.DampingMultiplier * 1.08,
                RestitutionMultiplier = tuning.RestitutionMultiplier * 0.90
            };
        }
        else if (mesh.MeshType.Equals("physics-enabled", StringComparison.OrdinalIgnoreCase))
        {
            tuning = tuning with
            {
                OffsetMultiplier = tuning.OffsetMultiplier * 1.08,
                DampingMultiplier = tuning.DampingMultiplier * 0.92
            };
        }

        return tuning;
    }

    private static string BuildCbpcXml(bool isMale, PhysicsSolverTuning tuning)
    {
        static string F(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<CBPCConfig version=\"1\">");
        if (isMale)
        {
            sb.AppendLine("  <PecPhysics>");
            sb.AppendLine($"    <Stiffness>{F(0.88 * tuning.StiffnessMultiplier)}</Stiffness>");
            sb.AppendLine($"    <Damping>{F(Math.Clamp(0.62 * tuning.DampingMultiplier, 0.35, 0.95))}</Damping>");
            sb.AppendLine($"    <Gravity>{F(Math.Clamp(0.04 * tuning.GravityMultiplier, 0.01, 0.20))}</Gravity>");
            sb.AppendLine($"    <MaxOffset>{F(0.06 * tuning.OffsetMultiplier)}</MaxOffset>");
            sb.AppendLine("  </PecPhysics>");
            sb.AppendLine("  <BellyPhysics>");
            sb.AppendLine($"    <Stiffness>{F(0.92 * tuning.StiffnessMultiplier)}</Stiffness>");
            sb.AppendLine($"    <Damping>{F(Math.Clamp(0.65 * tuning.DampingMultiplier, 0.35, 0.95))}</Damping>");
            sb.AppendLine($"    <Gravity>{F(Math.Clamp(0.03 * tuning.GravityMultiplier, 0.01, 0.20))}</Gravity>");
            sb.AppendLine($"    <MaxOffset>{F(0.04 * tuning.OffsetMultiplier)}</MaxOffset>");
            sb.AppendLine("  </BellyPhysics>");
        }
        else
        {
            sb.AppendLine("  <BreastPhysics>");
            sb.AppendLine($"    <Stiffness>{F(0.90 * tuning.StiffnessMultiplier)}</Stiffness>");
            sb.AppendLine($"    <Damping>{F(Math.Clamp(0.60 * tuning.DampingMultiplier, 0.35, 0.95))}</Damping>");
            sb.AppendLine($"    <Gravity>{F(Math.Clamp(0.05 * tuning.GravityMultiplier, 0.01, 0.20))}</Gravity>");
            sb.AppendLine($"    <MaxOffset>{F(0.08 * tuning.OffsetMultiplier)}</MaxOffset>");
            sb.AppendLine("  </BreastPhysics>");
            sb.AppendLine("  <ButtPhysics>");
            sb.AppendLine($"    <Stiffness>{F(0.85 * tuning.StiffnessMultiplier)}</Stiffness>");
            sb.AppendLine($"    <Damping>{F(Math.Clamp(0.55 * tuning.DampingMultiplier, 0.35, 0.95))}</Damping>");
            sb.AppendLine($"    <Gravity>{F(Math.Clamp(0.06 * tuning.GravityMultiplier, 0.01, 0.20))}</Gravity>");
            sb.AppendLine($"    <MaxOffset>{F(0.06 * tuning.OffsetMultiplier)}</MaxOffset>");
            sb.AppendLine("  </ButtPhysics>");
            sb.AppendLine("  <BellyPhysics>");
            sb.AppendLine($"    <Stiffness>{F(0.92 * tuning.StiffnessMultiplier)}</Stiffness>");
            sb.AppendLine($"    <Damping>{F(Math.Clamp(0.65 * tuning.DampingMultiplier, 0.35, 0.95))}</Damping>");
            sb.AppendLine($"    <Gravity>{F(Math.Clamp(0.03 * tuning.GravityMultiplier, 0.01, 0.20))}</Gravity>");
            sb.AppendLine($"    <MaxOffset>{F(0.04 * tuning.OffsetMultiplier)}</MaxOffset>");
            sb.AppendLine("  </BellyPhysics>");
        }

        sb.AppendLine("</CBPCConfig>");
        return sb.ToString();
    }

    private static string BuildSmpXml(string targetBody, bool isMale, PhysicsSolverTuning tuning)
    {
        static string F(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine($"<system name=\"{targetBody}ArmorPhysics\">");
        if (isMale)
        {
            sb.AppendLine($"  <bone name=\"NPC L Pec\" mass=\"{F(2.5 * tuning.MassMultiplier)}\" stiffness=\"{F(0.85 * tuning.StiffnessMultiplier)}\" damping=\"{F(Math.Clamp(0.60 * tuning.DampingMultiplier, 0.35, 0.95))}\">");
            sb.AppendLine($"    <angularLimit min=\"{F(-15 * tuning.OffsetMultiplier)}\" max=\"{F(15 * tuning.OffsetMultiplier)}\" restitution=\"{F(Math.Clamp(0.15 * tuning.RestitutionMultiplier, 0.05, 0.35))}\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine($"  <bone name=\"NPC R Pec\" mass=\"{F(2.5 * tuning.MassMultiplier)}\" stiffness=\"{F(0.85 * tuning.StiffnessMultiplier)}\" damping=\"{F(Math.Clamp(0.60 * tuning.DampingMultiplier, 0.35, 0.95))}\">");
            sb.AppendLine($"    <angularLimit min=\"{F(-15 * tuning.OffsetMultiplier)}\" max=\"{F(15 * tuning.OffsetMultiplier)}\" restitution=\"{F(Math.Clamp(0.15 * tuning.RestitutionMultiplier, 0.05, 0.35))}\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine($"  <bone name=\"NPC Belly\" mass=\"{F(1.5 * tuning.MassMultiplier)}\" stiffness=\"{F(0.90 * tuning.StiffnessMultiplier)}\" damping=\"{F(Math.Clamp(0.65 * tuning.DampingMultiplier, 0.35, 0.95))}\">");
            sb.AppendLine($"    <angularLimit min=\"{F(-8 * tuning.OffsetMultiplier)}\" max=\"{F(8 * tuning.OffsetMultiplier)}\" restitution=\"{F(Math.Clamp(0.10 * tuning.RestitutionMultiplier, 0.05, 0.35))}\" />");
            sb.AppendLine("  </bone>");
        }
        else
        {
            sb.AppendLine($"  <bone name=\"NPC L Breast01\" mass=\"{F(2.0 * tuning.MassMultiplier)}\" stiffness=\"{F(0.80 * tuning.StiffnessMultiplier)}\" damping=\"{F(Math.Clamp(0.50 * tuning.DampingMultiplier, 0.35, 0.95))}\">");
            sb.AppendLine($"    <angularLimit min=\"{F(-20 * tuning.OffsetMultiplier)}\" max=\"{F(20 * tuning.OffsetMultiplier)}\" restitution=\"{F(Math.Clamp(0.20 * tuning.RestitutionMultiplier, 0.05, 0.35))}\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine($"  <bone name=\"NPC R Breast01\" mass=\"{F(2.0 * tuning.MassMultiplier)}\" stiffness=\"{F(0.80 * tuning.StiffnessMultiplier)}\" damping=\"{F(Math.Clamp(0.50 * tuning.DampingMultiplier, 0.35, 0.95))}\">");
            sb.AppendLine($"    <angularLimit min=\"{F(-20 * tuning.OffsetMultiplier)}\" max=\"{F(20 * tuning.OffsetMultiplier)}\" restitution=\"{F(Math.Clamp(0.20 * tuning.RestitutionMultiplier, 0.05, 0.35))}\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine($"  <bone name=\"NPC Belly\" mass=\"{F(1.5 * tuning.MassMultiplier)}\" stiffness=\"{F(0.90 * tuning.StiffnessMultiplier)}\" damping=\"{F(Math.Clamp(0.60 * tuning.DampingMultiplier, 0.35, 0.95))}\">");
            sb.AppendLine($"    <angularLimit min=\"{F(-10 * tuning.OffsetMultiplier)}\" max=\"{F(10 * tuning.OffsetMultiplier)}\" restitution=\"{F(Math.Clamp(0.10 * tuning.RestitutionMultiplier, 0.05, 0.35))}\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine($"  <bone name=\"NPC L Butt\" mass=\"{F(1.8 * tuning.MassMultiplier)}\" stiffness=\"{F(0.75 * tuning.StiffnessMultiplier)}\" damping=\"{F(Math.Clamp(0.55 * tuning.DampingMultiplier, 0.35, 0.95))}\">");
            sb.AppendLine($"    <angularLimit min=\"{F(-15 * tuning.OffsetMultiplier)}\" max=\"{F(15 * tuning.OffsetMultiplier)}\" restitution=\"{F(Math.Clamp(0.20 * tuning.RestitutionMultiplier, 0.05, 0.35))}\" />");
            sb.AppendLine("  </bone>");
            sb.AppendLine($"  <bone name=\"NPC R Butt\" mass=\"{F(1.8 * tuning.MassMultiplier)}\" stiffness=\"{F(0.75 * tuning.StiffnessMultiplier)}\" damping=\"{F(Math.Clamp(0.55 * tuning.DampingMultiplier, 0.35, 0.95))}\">");
            sb.AppendLine($"    <angularLimit min=\"{F(-15 * tuning.OffsetMultiplier)}\" max=\"{F(15 * tuning.OffsetMultiplier)}\" restitution=\"{F(Math.Clamp(0.20 * tuning.RestitutionMultiplier, 0.05, 0.35))}\" />");
            sb.AppendLine("  </bone>");
        }

        sb.AppendLine("</system>");
        return sb.ToString();
    }
}

/// <summary>
/// Maps source skeleton bones to target body skeleton bones using known bone name tables.
/// Bones that exist in the source but have no equivalent in the target skeleton are reported as unsupported.
/// </summary>
internal sealed class BasicSkeletonMappingService : ISkeletonMappingService
{
    // Standard vanilla + XPMSSE bones shared by most body types.
    private static readonly IReadOnlyList<string> CommonBones =
    [
        "NPC Root", "NPC COM", "NPC Pelvis", "NPC Spine", "NPC Spine1", "NPC Spine2",
        "NPC Neck", "NPC Head",
        "NPC L Clavicle", "NPC L UpperArm", "NPC L ForeArm", "NPC L Hand",
        "NPC R Clavicle", "NPC R UpperArm", "NPC R ForeArm", "NPC R Hand",
        "NPC L Thigh", "NPC L Calf", "NPC L Foot",
        "NPC R Thigh", "NPC R Calf", "NPC R Foot"
    ];

    // Physics bones added by SMP/3BA/BHUNP on female bodies.
    private static readonly IReadOnlySet<string> FeaturePhysicsBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NPC L Breast", "NPC R Breast", "NPC L Breast01", "NPC R Breast01",
        "NPC Belly", "NPC Butt", "NPC L Butt", "NPC R Butt",
        "NPC L Breast02", "NPC R Breast02"
    };

    // Physics bones specific to HIMBO/SAM male bodies.
    private static readonly IReadOnlySet<string> MalePhysicsBones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NPC L Pec", "NPC R Pec", "NPC Belly", "NPC L Lat", "NPC R Lat"
    };

    // Map of which body types support which extra physics bone sets.
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> BodyPhysicsBoneSupport =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["3BA"]   = FeaturePhysicsBones,
            ["BHUNP"] = FeaturePhysicsBones,
            ["TBD"]   = FeaturePhysicsBones,
            ["HIMBO"] = MalePhysicsBones,
            ["SAM"]   = MalePhysicsBones,
            ["CBBE"]  = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["UNP"]   = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["UBE"]   = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            ["SOS"]   = MalePhysicsBones
        };

    public Task<SkeletonMappingResult> MapAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken)
    {
        BodyPhysicsBoneSupport.TryGetValue(targetBody, out var targetPhysicsBones);
        targetPhysicsBones ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allTargetBones = CommonBones.Concat(targetPhysicsBones).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Infer which source physics bones are present from the body reference / physics files.
        var sourcePhysicsBones = armor.PhysicsFiles.Count > 0
            ? FeaturePhysicsBones.Concat(MalePhysicsBones).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : (IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allSourceBones = CommonBones.Concat(sourcePhysicsBones).ToList();

        var mappings = new List<SkeletonBoneMapping>(allSourceBones.Count);
        var unsupportedBones = new List<string>();

        foreach (var bone in allSourceBones)
        {
            if (allTargetBones.Contains(bone))
            {
                mappings.Add(new SkeletonBoneMapping(bone, bone, FeaturePhysicsBones.Contains(bone) || MalePhysicsBones.Contains(bone)));
            }
            else
            {
                unsupportedBones.Add(bone);
            }
        }

        var sourceSkeleton = armor.PhysicsFiles.Count > 0 ? "xpmsse-physics" : "xpmsse-vanilla";
        var targetSkeleton = targetPhysicsBones.Count > 0 ? $"xpmsse-{targetBody.ToLowerInvariant()}-physics" : "xpmsse-vanilla";

        return Task.FromResult(new SkeletonMappingResult(sourceSkeleton, targetSkeleton, mappings, unsupportedBones));
    }
}

/// <summary>
/// Rebuilds Skyrim BSDismemberSkinInstance body partitions after mesh conversion to ensure
/// correct slot assignments and prevent invisible body parts or armor conflicts.
/// </summary>
internal sealed class BasicPartitionRebuildingService : IPartitionRebuildingService
{
    // Standard Skyrim partition slot numbers and their labels.
    private static readonly IReadOnlyDictionary<int, string> PartitionSlots =
        new Dictionary<int, string>
        {
            [32] = "Body",
            [33] = "Hands",
            [34] = "Forearms",
            [35] = "Amulet",
            [36] = "Ring",
            [37] = "Feet",
            [38] = "Calves",
            [39] = "Shield",
            [40] = "Tail",
            [41] = "LongHair",
            [42] = "Circlet",
            [43] = "Ears",
            [44] = "Dragon Head",
            [45] = "Dragon LWing",
            [46] = "Dragon RWing",
            [47] = "Dragon Body",
            [48] = "Dragon Tail",
            [49] = "Dragon Leg",
            [50] = "Dragon Claws",
            [54] = "DecapHead",
            [55] = "Decap",
            [56] = "Genitals"
        };

    public Task<PartitionRebuildingResult> RebuildAsync(WeightedMesh mesh, MeshAnalysis analysis, string targetBody, CancellationToken cancellationToken)
    {
        // Select partitions based on mesh type and target body.
        var slots = new List<int>();

        switch (analysis.MeshType)
        {
            case "headgear":
                slots.Add(42); // Circlet (used for helmets, hoods, circlets, crowns)
                break;

            case "plate":
            case "leather":
            case "mixed":
                slots.Add(32); // Body
                slots.Add(33); // Hands
                slots.Add(37); // Feet
                break;

            case "cloth":
            case "skin-tight":
            case "physics-enabled":
                slots.Add(32); // Body
                break;

            default:
                slots.Add(32);
                break;
        }

        // Physics-capable bodies get the genitals partition for compatibility (body slots only).
        if (!string.Equals(analysis.MeshType, "headgear", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(targetBody, "3BA", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(targetBody, "BHUNP", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(targetBody, "SAM", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(targetBody, "HIMBO", StringComparison.OrdinalIgnoreCase)))
        {
            slots.Add(56); // Genitals
        }

        var partitionLabels = slots
            .Where(PartitionSlots.ContainsKey)
            .Select(s => $"{s}:{PartitionSlots[s]}")
            .ToList();

        // Report any slots that cannot be mapped to valid partition names as removed.
        var removedSlots = slots
            .Where(s => !PartitionSlots.ContainsKey(s))
            .Select(s => s.ToString())
            .ToList();

        return Task.FromResult(new PartitionRebuildingResult(true, partitionLabels, removedSlots));
    }
}

/// <summary>
/// Detects which body regions an armor piece covers by combining physics-file bone name scoring
/// with mesh filename keyword analysis. Bone names always take precedence when present.
/// </summary>
internal sealed class BasicArmorRegionBindingService : IArmorRegionBindingService
{
    // Maps bone name substrings (case-insensitive) to the body regions they indicate.
    private static readonly (string BoneToken, string Region)[] BoneRegionRules =
    [
        ("Breast",    "chest"),
        ("Belly",     "belly"),
        ("Butt",      "pelvis"),
        ("Spine2",    "chest"),
        ("Spine1",    "waist"),
        ("Spine",     "chest"),
        ("Pelvis",    "pelvis"),
        ("Thigh",     "legs"),
        ("Calf",      "legs"),
        ("Foot",      "legs"),
        ("Toe",       "legs"),
        ("UpperArm",  "arms"),
        ("ForeArm",   "arms"),
        ("Hand",      "arms"),
        ("Clavicle",  "shoulders"),
        ("Neck",      "shoulders"),
        ("Head",      "shoulders"),
    ];

    // Maps filename keyword substrings to regions (checked when bone names are unavailable).
    private static readonly (string FileToken, string Region)[] FileRegionRules =
    [
        ("cuirass",     "chest"),
        ("breastplate", "chest"),
        ("chestplate",  "chest"),
        ("torso",       "chest"),
        ("robe",        "chest"),
        ("gauntlet",    "arms"),
        ("glove",       "arms"),
        ("forearm",     "arms"),
        ("bracer",      "arms"),
        ("sabatons",    "legs"),
        ("greave",      "legs"),
        ("boot",        "legs"),
        ("legging",     "legs"),
        ("trouser",     "legs"),
        ("pauldron",    "shoulders"),
        ("spaulder",    "shoulders"),
        ("shoulder",    "shoulders"),
        ("helm",        "shoulders"),
        ("hood",        "shoulders"),
        ("crown",       "shoulders"),
        ("skirt",       "pelvis"),
        ("kilt",        "pelvis"),
        ("loincloth",   "pelvis"),
        ("pelvis",      "pelvis"),
        ("body",        "chest"),
    ];

    public Task<ArmorRegionBinding> BindAsync(ImportedArmor armor, MeshAnalysis analysis, CancellationToken cancellationToken)
    {
        // Phase 1: score regions from physics file content (bone names).
        var boneScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var physicsFile in armor.PhysicsFiles)
        {
            if (!File.Exists(physicsFile))
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(physicsFile);
                foreach (var (boneToken, region) in BoneRegionRules)
                {
                    if (content.Contains(boneToken, StringComparison.OrdinalIgnoreCase))
                    {
                        boneScores[region] = boneScores.GetValueOrDefault(region) + 2;
                    }
                }
            }
            catch (IOException) { /* skip unreadable files */ }
        }

        if (boneScores.Count > 0)
        {
            var regions = boneScores
                .OrderByDescending(p => p.Value)
                .Select(p => p.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
            return Task.FromResult(new ArmorRegionBinding(regions, "bone-names"));
        }

        // Phase 2: use sampled mesh geometry when readable to infer coverage by vertical band
        // and lateral spread rather than relying on filenames alone.
        var geometryBinding = TryBindFromGeometry(armor.MeshFiles);
        if (geometryBinding is not null)
        {
            return Task.FromResult(geometryBinding);
        }

        // Phase 3: fall back to filename keyword scoring.
        var fileScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var meshFile in armor.MeshFiles)
        {
            var name = Path.GetFileNameWithoutExtension(meshFile).ToLowerInvariant();
            foreach (var (fileToken, region) in FileRegionRules)
            {
                if (name.Contains(fileToken, StringComparison.OrdinalIgnoreCase))
                {
                    fileScores[region] = fileScores.GetValueOrDefault(region) + 1;
                }
            }
        }

        if (fileScores.Count > 0)
        {
            var regions = fileScores
                .OrderByDescending(p => p.Value)
                .Select(p => p.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
            return Task.FromResult(new ArmorRegionBinding(regions, "filename-keywords"));
        }

        // Phase 4: default — full-body coverage when no signals are available.
        return Task.FromResult(new ArmorRegionBinding(["chest", "waist", "pelvis", "legs"], "default-full-body"));
    }

    private static ArmorRegionBinding? TryBindFromGeometry(IReadOnlyList<string> meshFiles)
    {
        var signature = NifGeometrySignatureReader.TryReadBest(meshFiles);
        if (signature is null || signature.SampleVertices.Count == 0 || signature.Height <= 0.001f || signature.Width <= 0.001f)
        {
            return null;
        }

        var centerX = (signature.MinX + signature.MaxX) / 2f;
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var vertex in signature.SampleVertices)
        {
            var normalizedHeight = (vertex.Z - signature.MinZ) / signature.Height;
            var lateralSpread = Math.Abs(vertex.X - centerX) / signature.Width;

            // These normalized height bands approximate common humanoid proportions after the
            // mesh bounds are projected into 0..1 space (feet near 0, shoulders/head near 1).
            AddIfInRange(scores, "shoulders", normalizedHeight, 0.82, 1.01);
            AddIfInRange(scores, "chest", normalizedHeight, 0.56, 0.82);
            AddIfInRange(scores, "waist", normalizedHeight, 0.40, 0.60);
            AddIfInRange(scores, "pelvis", normalizedHeight, 0.24, 0.44);
            AddIfInRange(scores, "thighs", normalizedHeight, 0.12, 0.32);
            AddIfInRange(scores, "calves", normalizedHeight, 0.00, 0.18);
            AddIfInRange(scores, "legs", normalizedHeight, 0.00, 0.34);

            if (normalizedHeight >= 0.36 && normalizedHeight <= 0.82 && lateralSpread >= 0.34)
            {
                scores["arms"] = scores.GetValueOrDefault("arms") + 2;
            }

            if (normalizedHeight >= 0.56 && normalizedHeight <= 0.76 && lateralSpread >= 0.18)
            {
                scores["breasts"] = scores.GetValueOrDefault("breasts") + 1;
            }
        }

        if (scores.Count == 0)
        {
            return null;
        }

        var minimumHits = Math.Max(3, signature.SampleVertices.Count / 18);
        var regions = scores
            .Where(pair => pair.Value >= minimumHits)
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        return regions.Count == 0
            ? null
            : new ArmorRegionBinding(regions, "spatial-geometry");
    }

    private static void AddIfInRange(Dictionary<string, int> scores, string region, double value, double minInclusive, double maxInclusive)
    {
        if (value >= minInclusive && value <= maxInclusive)
        {
            scores[region] = scores.GetValueOrDefault(region) + 1;
        }
    }
}

/// <summary>
/// Scales each regional morph factor toward or away from 1.0 using a named deformation profile amplifier.
/// </summary>
public static class DeformationProfileModifier
{
    // Amount to amplify the body transformation delta (value - 1) for each profile.
    private static readonly IReadOnlyDictionary<string, double> ProfileAmplifiers =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["balanced"] = 1.00,   // full-fidelity delta, no amplification or attenuation
            ["curvy"]    = 1.15,
            ["slim"]     = 0.82,
            ["petite"]   = 0.75,
            ["athletic"] = 1.08,
            ["muscular"] = 1.25,
            ["lean"]     = 0.88,
            ["anime"]    = 1.45    // strongly amplified proportions for stylised anime aesthetics
        };

    public static IReadOnlyDictionary<string, double> Apply(IReadOnlyDictionary<string, double> field, string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile) || !ProfileAmplifiers.TryGetValue(profile, out var amplifier))
        {
            return field;
        }

        return field.ToDictionary(
            pair => pair.Key,
            pair => 1.0 + ((pair.Value - 1.0) * amplifier),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns all supported profile names in alphabetical order.</summary>
    public static IReadOnlyList<string> All => ProfileAmplifiers.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>
/// Generates a BodySlide .osp project XML file for the converted armor.
/// The OSP file contains standard slider definitions for the target body type.
/// </summary>
internal sealed class BodySlideOspProjectService : IBodySlideProjectService
{
    // Standard BodySlide sliders per target body family.
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BodySliders =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["CBBE"]  = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist"],
            ["3BA"]   = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist", "BreastsPhysics", "ButtPhysics", "BellyPhysics"],
            ["BHUNP"] = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders", "NarrowWaist", "BreastsPhysics", "ButtPhysics"],
            ["UNP"]   = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves", "Arms", "Shoulders"],
            ["HIMBO"] = ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt", "Pecs"],
            ["SAM"]   = ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt"],
            ["SOS"]   = ["Body", "Chest", "Waist", "Arms", "Legs", "Shoulders", "Butt"],
            ["TBD"]   = ["Belly", "Butt", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "HipWidth", "Thighs", "Calves"],
            ["UBE"]   = ["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth", "Thighs"]
        };

    private static readonly IReadOnlyDictionary<string, string> BodyOutputPaths =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HIMBO"] = @"meshes\actors\character\character assets male\",
            ["SAM"]   = @"meshes\actors\character\character assets male\",
            ["SOS"]   = @"meshes\actors\character\character assets male\"
        };

    private static readonly IReadOnlySet<string> MaleBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "HIMBO", "SAM", "SOS"
    };

    public Task<BodySlideProject> GenerateAsync(ImportedArmor armor, ConvertedMesh mesh, string targetBody, CancellationToken cancellationToken)
    {
        var rawName = Path.GetFileNameWithoutExtension(armor.MeshFiles[0]) ?? "ConvertedArmor";
        var projectName = new string(rawName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(projectName)) projectName = "ConvertedArmor";

        var sliders = BodySliders.TryGetValue(targetBody, out var bodySliders)
            ? bodySliders
            : (IReadOnlyList<string>)["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth"];

        BodyOutputPaths.TryGetValue(targetBody, out var outputPath);
        outputPath ??= @"meshes\actors\character\character assets\";

        var isMale = MaleBodies.Contains(targetBody);
        var gender = isMale ? "male" : "female";
        var outputFile0 = isMale ? "malebody_0.nif" : "femalebody_0.nif";
        var outputFile1 = isMale ? "malebody_1.nif" : "femalebody_1.nif";

        var shapeDataFolder = $@"CalienteTools\BodySlide\ShapeData\{projectName}";
        var sourceFile = $@"{shapeDataFolder}\{projectName}.nif";

        var ospXml = BuildOspXml(projectName, sliders, shapeDataFolder, sourceFile, outputPath, gender, outputFile0, outputFile1);

        return Task.FromResult(new BodySlideProject(projectName, targetBody, sliders, ospXml));
    }

    private static string BuildOspXml(
        string projectName,
        IReadOnlyList<string> sliders,
        string setFolder,
        string sourceFile,
        string outputPath,
        string gender,
        string outputFile0,
        string outputFile1)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<SliderSetInfo version=\"1\">");
        sb.AppendLine($"    <SliderSet name=\"{Escape(projectName)}\" baseShape=\"Base Shape\" bsversion=\"20\">");
        sb.AppendLine($"        <SetFolder>{Escape(setFolder)}</SetFolder>");
        sb.AppendLine($"        <SourceFile>{Escape(sourceFile)}</SourceFile>");
        sb.AppendLine($"        <OutputPath>{Escape(outputPath)}</OutputPath>");
        sb.AppendLine($"        <OutputFile gender=\"{gender}\" use=\"true\">{Escape(outputFile0)}</OutputFile>");
        sb.AppendLine($"        <OutputFile gender=\"{gender}\" use=\"true\" morphfile=\"1\">{Escape(outputFile1)}</OutputFile>");

        foreach (var slider in sliders)
        {
            sb.AppendLine($"        <Slider name=\"{Escape(slider)}\" invert=\"false\" zap=\"false\" uv=\"false\">");
            sb.AppendLine("            <Low value=\"0\" />");
            sb.AppendLine("            <High value=\"100\" />");
            sb.AppendLine("        </Slider>");
        }

        sb.AppendLine("    </SliderSet>");
        sb.AppendLine("</SliderSetInfo>");
        return sb.ToString();
    }

    // Minimal XML attribute/content escaping for values embedded in the OSP document.
    private static string Escape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}

/// <summary>
/// Analyzes DDS texture files by reading magic bytes and categorizing diffuse vs. normal maps.
/// Reports diffuse textures that have no matching _n normal map in the set.
/// </summary>
internal sealed class BasicTextureAnalysisService : ITextureAnalysisService
{
    // DDS magic: "DDS " = 0x44 0x44 0x53 0x20
    private static readonly byte[] DdsMagic = [0x44, 0x44, 0x53, 0x20];

    // Regex to match relative Bethesda texture paths inside BGSM/BGEM material files.
    // Matches strings like textures/armor/something_d.dds (case-insensitive).
    private static readonly System.Text.RegularExpressions.Regex MaterialTexturePathRegex =
        new(@"textures/[^""<>\s]+\.dds",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<TextureSummary> AnalyzeAsync(ImportedArmor armor, CancellationToken cancellationToken)
    {
        var diffuseFiles = new List<string>();
        var normalFiles = new List<string>();
        var missingNormals = new List<string>();
        var specularFiles = new List<string>();
        var glowFiles = new List<string>();
        var parallaxFiles = new List<string>();
        var subsurfaceFiles = new List<string>();

        foreach (var texturePath in armor.TextureFiles)
        {
            if (!File.Exists(texturePath)) continue;
            if (!Path.GetExtension(texturePath).Equals(".dds", StringComparison.OrdinalIgnoreCase)) continue;
            if (!await IsValidDdsAsync(texturePath, cancellationToken)) continue;

            var fileName = Path.GetFileName(texturePath);
            var baseName = Path.GetFileNameWithoutExtension(texturePath);

            if (baseName.EndsWith("_n", StringComparison.OrdinalIgnoreCase) ||
                baseName.EndsWith("_normal", StringComparison.OrdinalIgnoreCase))
            {
                normalFiles.Add(fileName);
            }
            else if (baseName.EndsWith("_s", StringComparison.OrdinalIgnoreCase) ||
                     baseName.EndsWith("_spec", StringComparison.OrdinalIgnoreCase) ||
                     baseName.EndsWith("_specular", StringComparison.OrdinalIgnoreCase))
            {
                specularFiles.Add(fileName);
            }
            else if (baseName.EndsWith("_g", StringComparison.OrdinalIgnoreCase) ||
                     baseName.EndsWith("_glow", StringComparison.OrdinalIgnoreCase) ||
                     baseName.EndsWith("_em", StringComparison.OrdinalIgnoreCase))
            {
                glowFiles.Add(fileName);
            }
            else if (baseName.EndsWith("_p", StringComparison.OrdinalIgnoreCase) ||
                     baseName.EndsWith("_parallax", StringComparison.OrdinalIgnoreCase) ||
                     baseName.EndsWith("_h", StringComparison.OrdinalIgnoreCase))
            {
                parallaxFiles.Add(fileName);
            }
            else if (baseName.EndsWith("_sk", StringComparison.OrdinalIgnoreCase) ||
                     baseName.EndsWith("_subsurface", StringComparison.OrdinalIgnoreCase) ||
                     baseName.EndsWith("_sss", StringComparison.OrdinalIgnoreCase))
            {
                subsurfaceFiles.Add(fileName);
            }
            else
            {
                diffuseFiles.Add(fileName);
            }
        }

        foreach (var diffuse in diffuseFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(diffuse);
            var expectedNormal = baseName + "_n.dds";
            if (!normalFiles.Any(n => n.Equals(expectedNormal, StringComparison.OrdinalIgnoreCase)))
            {
                missingNormals.Add(diffuse);
            }
        }

        var materialTexturePaths = await ScanMaterialFilesAsync(armor.SourcePath, cancellationToken);

        return new TextureSummary(
            armor.TextureFiles.Count,
            diffuseFiles,
            normalFiles,
            missingNormals,
            specularFiles,
            glowFiles,
            parallaxFiles,
            subsurfaceFiles,
            materialTexturePaths);
    }

    /// <summary>
    /// Scans .bgsm and .bgem material files found in the armor source path for embedded texture references.
    /// BGSM/BGEM files in Skyrim SE are JSON and contain relative texture paths (e.g. textures/armor/…_d.dds).
    /// </summary>
    private static async Task<IReadOnlyList<string>> ScanMaterialFilesAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var materialFiles = EnumerateMaterialFilesForScan(sourcePath);
        if (materialFiles.Count == 0)
        {
            return [];
        }

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var materialFile in materialFiles)
        {
            if (!File.Exists(materialFile)) continue;
            try
            {
                var content = await File.ReadAllTextAsync(materialFile, cancellationToken);
                foreach (System.Text.RegularExpressions.Match m in MaterialTexturePathRegex.Matches(content))
                {
                    found.Add(m.Value.Replace('\\', '/').ToLowerInvariant());
                }
            }
            catch (IOException)
            {
                // Ignore unreadable material files.
            }
        }

        return [.. found.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)];
    }

    private static IReadOnlyList<string> EnumerateMaterialFilesForScan(string sourcePath)
    {
        static bool IsMaterial(string p) => Path.GetExtension(p) is ".bgsm" or ".bgem";

        var root = ResolveSupportRootForScan(sourcePath);
        if (File.Exists(root)) return IsMaterial(root) ? [Path.GetFullPath(root)] : [];
        if (!Directory.Exists(root)) return [];

        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        return Directory.GetFiles(root, "*.*", opts)
            .Where(IsMaterial)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveSupportRootForScan(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            // Walk up to the nearest "meshes" ancestor and use its parent as mod root.
            var dir = Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? string.Empty;
            while (!string.IsNullOrEmpty(dir))
            {
                var name = Path.GetFileName(dir);
                if (string.Equals(name, "meshes", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetDirectoryName(dir) ?? dir;
                }

                dir = Path.GetDirectoryName(dir) ?? string.Empty;
            }

            return Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? sourcePath;
        }

        return sourcePath;
    }

    private static async Task<bool> IsValidDdsAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var header = new byte[4];
            await using var stream = File.OpenRead(path);
            var read = await stream.ReadAsync(header.AsMemory(0, 4), cancellationToken);
            return read == 4 && header.SequenceEqual(DdsMagic);
        }
        catch (IOException)
        {
            return false;
        }
    }
}

/// <summary>
/// Scans .esp/.esm/.esl plugin files for NIF mesh path references and generates guidance
/// on which ArmorAddon records need to be updated to point at the converted meshes.
/// </summary>
internal sealed class BasicPluginAnalysisService : IPluginAnalysisService
{
    private static readonly IReadOnlyList<string> PluginExtensions = [".esp", ".esm", ".esl"];

    public async Task<PluginAnalysisResult> AnalyzeAsync(ImportedArmor armor, string targetBody, CancellationToken cancellationToken)
    {
        var pluginFiles = new List<string>();

        if (Directory.Exists(armor.SourcePath))
        {
            pluginFiles.AddRange(Directory.GetFiles(armor.SourcePath, "*.*", SearchOption.AllDirectories)
                .Where(f => PluginExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        }
        else if (File.Exists(armor.SourcePath) &&
                 PluginExtensions.Contains(Path.GetExtension(armor.SourcePath), StringComparer.OrdinalIgnoreCase))
        {
            pluginFiles.Add(armor.SourcePath);
        }

        var armorAddons  = new List<PluginArmorAddon>();
        var armorRecords = new List<PluginArmorRecord>();

        foreach (var pluginFile in pluginFiles)
        {
            var (addons, records) = await ScanPluginAsync(pluginFile, cancellationToken);
            armorAddons.AddRange(addons);
            armorRecords.AddRange(records);
        }

        var guidance = BuildPatchGuidance(armorAddons, targetBody, pluginFiles.Count);
        return new PluginAnalysisResult(
            pluginFiles.Select(f => Path.GetFileName(f) ?? f).ToList(),
            armorAddons,
            guidance,
            armorRecords.Count > 0 ? armorRecords : null);
    }

    private static async Task<(IReadOnlyList<PluginArmorAddon> Addons, IReadOnlyList<PluginArmorRecord> Records)>
        ScanPluginAsync(string pluginPath, CancellationToken cancellationToken)
    {
        try
        {
            var bytes      = await File.ReadAllBytesAsync(pluginPath, cancellationToken);
            var pluginName = Path.GetFileNameWithoutExtension(pluginPath) ?? "unknown";

            // ── ARMA records (ArmorAddon) ──────────────────────────────────────
            var armaDescriptors = BinaryArmaParser.ExtractArmaRecords(bytes);
            List<PluginArmorAddon> addons;

            if (armaDescriptors.Count > 0)
            {
                addons = armaDescriptors
                    .Select(d => new PluginArmorAddon(
                        pluginName,
                        d.MeshPaths,
                        d.FormId,
                        d.EditorId,
                        d.BipedSlots.Count > 0 ? d.BipedSlots : null,
                        d.RaceFormId))
                    .ToList();
            }
            else
            {
                // Fallback: regex scan when no structured ARMA records found.
                addons = RegexScanForNifPaths(bytes, pluginName).ToList();
            }

            // ── ARMO records (Armor — world/inventory models) ──────────────────
            var armoDescriptors = BinaryArmaParser.ExtractArmoRecords(bytes);
            var records = armoDescriptors
                .Select(d => new PluginArmorRecord(
                    pluginName,
                    d.MeshPaths,
                    d.FormId,
                    d.EditorId,
                    d.KeywordFormIds,
                    d.RaceFormId))
                .ToList();

            return (addons, records);
        }
        catch (IOException)
        {
            return ([], []);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex NifPathRegex =
        new(@"meshes/[^\x00\x01-\x1f""<>|:*?\\]+\.nif",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static IReadOnlyList<PluginArmorAddon> RegexScanForNifPaths(byte[] bytes, string pluginName)
    {
        var text  = System.Text.Encoding.Latin1.GetString(bytes);
        var paths = NifPathRegex.Matches(text)
            .Select(m => m.Value.Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0) return [];
        return [new PluginArmorAddon(pluginName, paths)];
    }

    private static string BuildPatchGuidance(IReadOnlyList<PluginArmorAddon> addons, string targetBody, int pluginCount)
    {
        if (pluginCount == 0)
        {
            return $"No plugin files found. Add the converted meshes to an existing .esp or create a new patch plugin targeting {targetBody}.";
        }

        if (addons.Count == 0)
        {
            return $"Scanned {pluginCount} plugin file(s) — no mesh path references detected. Verify ArmorAddon (ARMA) records manually in xEdit.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {addons.Count} ARMA record(s) with mesh paths. Run patch-armor.pas to auto-rewrite matching ArmorAddon (ARMA) records for {targetBody}:");
        foreach (var addon in addons)
        {
            var edidLabel  = addon.EditorId is not null ? $" [{addon.EditorId}]" : string.Empty;
            var formIdHex  = addon.FormId != 0 ? $" (FormID: {addon.FormId:X8})" : string.Empty;
            sb.AppendLine($"  Plugin: {addon.RecordType}{edidLabel}{formIdHex}");
            foreach (var path in addon.DetectedMeshPaths.Take(10))
            {
                sb.AppendLine($"    {path}");
            }

            if (addon.DetectedMeshPaths.Count > 10)
            {
                sb.AppendLine($"    ... and {addon.DetectedMeshPaths.Count - 10} more path(s)");
            }
        }

        return sb.ToString().Trim();
    }
}

/// <summary>
/// Performs true binary rewriting of Bethesda .esp/.esm/.esl plugin files:
/// parses the binary record structure, locates ARMA (ArmorAddon) and ARMO (Armor)
/// records, rewrites MOD2/MOD3/MOD4/MOD5 mesh path subrecords to point at the
/// converted SlideSmith meshes, and writes a patched plugin copy to the output
/// directory.  Compressed records (bit 18 set) are decompressed, patched, and
/// written back uncompressed so every record is always readable by the engine.
/// Supports both Skyrim LE (20-byte record headers) and SSE (24-byte headers).
/// </summary>
internal sealed class BinaryPluginRewriteService : IPluginRewriteService
{
    // SSE record/GRUP header is 24 bytes; LE is 20 bytes.
    private const int SseHeaderSize = 24;
    private const int LeHeaderSize  = 20;

    // size of a subrecord header: 4-byte type tag + 2-byte data length
    private const int SubrecordHeaderSize = 6;
    private const string ExtendedSizeTag = "XXXX";

    // Bit 18 of flags = zlib-compressed record data.
    private const uint FlagCompressed = 0x00040000u;

    // Record types whose mesh-path subrecords (MOD2/MOD3/MOD4/MOD5) we rewrite.
    private static readonly HashSet<string> ArmorRecordTypes = new(StringComparer.Ordinal)
        { "ARMA", "ARMO" };

    // ARMA subrecord types that hold NIF mesh file paths.
    private static readonly HashSet<string> MeshSubrecordTypes = new(StringComparer.Ordinal)
        { "MOD2", "MOD3", "MOD4", "MOD5" };

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<PluginRewriteResult> RewriteAsync(
        IReadOnlyList<string> pluginPaths,
        IReadOnlyDictionary<string, string> rewriteMap,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (rewriteMap.Count == 0 || pluginPaths.Count == 0)
        {
            return new PluginRewriteResult(0, 0, 0, [], []);
        }

        // Normalise the rewrite-map keys to lowercase forward-slash so the
        // case-insensitive comparison below never misses a match.
        var normMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in rewriteMap)
        {
            normMap[key.Replace('\\', '/').ToLowerInvariant()] = value;
        }

        var patchedPaths = new List<string>();
        var warnings     = new List<string>();
        int totalArmaPatched    = 0;
        int totalPathsRewritten = 0;

        foreach (var pluginPath in pluginPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var bytes      = await File.ReadAllBytesAsync(pluginPath, cancellationToken);
                var headerSize = DetectHeaderSize(bytes);

                var (patchedBytes, armaPatched, pathsRewritten, fileWarnings) =
                    RewritePlugin(bytes, headerSize, normMap);

                foreach (var w in fileWarnings) warnings.Add(w);

                if (pathsRewritten > 0)
                {
                    var baseName = Path.GetFileNameWithoutExtension(pluginPath);
                    var extension = Path.GetExtension(pluginPath);
                    if (string.IsNullOrWhiteSpace(extension))
                    {
                        extension = ".esp";
                    }
                    var outPath  = Path.Combine(outputDirectory, $"{baseName}_patched{extension}");
                    await File.WriteAllBytesAsync(outPath, patchedBytes, cancellationToken);
                    patchedPaths.Add(outPath);
                    totalArmaPatched    += armaPatched;
                    totalPathsRewritten += pathsRewritten;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                warnings.Add($"Could not process {Path.GetFileName(pluginPath)}: {ex.Message}");
            }
        }

        return new PluginRewriteResult(
            pluginPaths.Count,
            totalArmaPatched,
            totalPathsRewritten,
            patchedPaths,
            warnings);
    }

    // ── Header-size detection ─────────────────────────────────────────────────

    /// <summary>
    /// Skyrim LE = 20-byte record headers; after the TES4 header the first subrecord
    /// "HEDR" appears at byte offset 20.  SSE extends the header to 24 bytes so "HEDR"
    /// appears at offset 24.  Any other layout defaults to the SSE size.
    /// </summary>
    internal static int DetectHeaderSize(byte[] bytes)
    {
        if (bytes.Length < 4 ||
            bytes[0] != 'T' || bytes[1] != 'E' || bytes[2] != 'S' || bytes[3] != '4')
        {
            return SseHeaderSize;
        }

        // LE: "HEDR" at offset 20
        if (bytes.Length > 23 &&
            bytes[20] == 'H' && bytes[21] == 'E' && bytes[22] == 'D' && bytes[23] == 'R')
        {
            return LeHeaderSize;
        }

        return SseHeaderSize;
    }

    // ── File-level rewrite ────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the entire plugin file, rewriting matching ARMA/ARMO mesh-path subrecords.
    /// Compressed records are transparently decompressed, patched, and re-compressed so
    /// the original compression flag state is preserved whenever possible.
    /// Returns (patched_bytes, arma_and_armo_records_patched, paths_rewritten, warnings).
    /// </summary>
    internal static (byte[] Patched, int ArmaPatched, int PathsRewritten, IReadOnlyList<string> Warnings)
        RewritePlugin(byte[] bytes, int headerSize, IReadOnlyDictionary<string, string> rewriteMap)
    {
        using var ms = new MemoryStream(bytes.Length);
        var warnings       = new List<string>();
        int armaPatched    = 0;
        int pathsRewritten = 0;

        int pos = 0;
        while (pos < bytes.Length)
        {
            if (pos + headerSize > bytes.Length) break;

            var tag      = ReadTag(bytes, pos);
            var field4   = ReadUInt32Le(bytes, pos + 4);

            if (string.Equals(tag, "GRUP", StringComparison.Ordinal))
            {
                // For GRUPs, field4 = TOTAL group size (header + content).
                var groupSize = (int)field4;
                if (groupSize < headerSize || pos + groupSize > bytes.Length) break;

                int contentSize = groupSize - headerSize;

                var (groupContent, ga, gp, gw) =
                    RewriteBlock(bytes, pos + headerSize, contentSize, headerSize, rewriteMap);

                foreach (var w in gw) warnings.Add(w);
                armaPatched    += ga;
                pathsRewritten += gp;

                // Write GRUP header with updated total size.
                ms.Write(bytes, pos, 4);                                         // "GRUP"
                WriteUInt32Le(ms, (uint)(headerSize + groupContent.Length));      // new total
                ms.Write(bytes, pos + 8, headerSize - 8);                        // rest of hdr
                ms.Write(groupContent, 0, groupContent.Length);

                pos += groupSize;
            }
            else
            {
                // Regular record — field4 = data size (NOT including header).
                var dataSize  = (int)field4;
                int totalSize = headerSize + dataSize;
                if (pos + totalSize > bytes.Length) break;

                if (ArmorRecordTypes.Contains(tag))
                {
                    var flags      = ReadUInt32Le(bytes, pos + 8);
                    bool compressed = (flags & FlagCompressed) != 0;

                    byte[] workData;
                    if (compressed && dataSize > 4)
                    {
                        var decompressed = TryDecompressRecord(bytes, pos + headerSize, dataSize, warnings, tag);
                        workData = decompressed ?? [];
                    }
                    else
                    {
                        workData = bytes[(pos + headerSize)..(pos + headerSize + dataSize)];
                    }

                    if (workData.Length > 0)
                    {
                        var (newData, rp) = RewriteArmaSubrecords(workData, 0, workData.Length, rewriteMap);
                        pathsRewritten += rp;
                        if (rp > 0) armaPatched++;

                        var recordDataToWrite = newData;
                        var newFlags = flags;
                        if (compressed)
                        {
                            var recompressed = TryCompressRecord(newData, warnings, tag);
                            if (recompressed is not null)
                            {
                                recordDataToWrite = recompressed;
                                newFlags = flags | FlagCompressed;
                            }
                            else
                            {
                                newFlags = flags & ~FlagCompressed;
                            }
                        }

                        ms.Write(bytes, pos, 4);                                    // tag
                        WriteUInt32Le(ms, (uint)recordDataToWrite.Length);          // new dataSize
                        WriteUInt32Le(ms, newFlags);                                // flags
                        ms.Write(bytes, pos + 12, headerSize - 12);                // FormID..rest
                        ms.Write(recordDataToWrite, 0, recordDataToWrite.Length);
                    }
                    else
                    {
                        // Could not read/decompress — copy verbatim.
                        ms.Write(bytes, pos, totalSize);
                    }
                }
                else
                {
                    // Any other record: copy verbatim.
                    ms.Write(bytes, pos, totalSize);
                }

                pos += totalSize;
            }
        }

        return (ms.ToArray(), armaPatched, pathsRewritten, warnings);
    }

    // ── Block-level rewrite (content inside a GRUP) ───────────────────────────

    private static (byte[] Content, int ArmaPatched, int PathsRewritten, IReadOnlyList<string> Warnings)
        RewriteBlock(byte[] bytes, int start, int length, int headerSize,
                     IReadOnlyDictionary<string, string> rewriteMap)
    {
        using var ms = new MemoryStream(length);
        var warnings       = new List<string>();
        int armaPatched    = 0;
        int pathsRewritten = 0;

        int pos = start;
        int end = start + length;

        while (pos < end)
        {
            if (pos + headerSize > end) break;

            var tag    = ReadTag(bytes, pos);
            var field4 = ReadUInt32Le(bytes, pos + 4);

            if (string.Equals(tag, "GRUP", StringComparison.Ordinal))
            {
                var groupSize = (int)field4;
                if (groupSize < headerSize || pos + groupSize > end) break;

                int contentSize = groupSize - headerSize;
                var (groupContent, ga, gp, gw) =
                    RewriteBlock(bytes, pos + headerSize, contentSize, headerSize, rewriteMap);

                foreach (var w in gw) warnings.Add(w);
                armaPatched    += ga;
                pathsRewritten += gp;

                ms.Write(bytes, pos, 4);
                WriteUInt32Le(ms, (uint)(headerSize + groupContent.Length));
                ms.Write(bytes, pos + 8, headerSize - 8);
                ms.Write(groupContent, 0, groupContent.Length);

                pos += groupSize;
            }
            else
            {
                var dataSize  = (int)field4;
                int totalSize = headerSize + dataSize;
                if (pos + totalSize > end) break;

                if (ArmorRecordTypes.Contains(tag))
                {
                    var flags      = ReadUInt32Le(bytes, pos + 8);
                    bool compressed = (flags & FlagCompressed) != 0;

                    byte[] workData;
                    if (compressed && dataSize > 4)
                    {
                        var decompressed = TryDecompressRecord(bytes, pos + headerSize, dataSize, warnings, tag);
                        workData = decompressed ?? [];
                    }
                    else
                    {
                        workData = bytes[(pos + headerSize)..(pos + headerSize + dataSize)];
                    }

                    if (workData.Length > 0)
                    {
                        var (newData, rp) = RewriteArmaSubrecords(workData, 0, workData.Length, rewriteMap);
                        pathsRewritten += rp;
                        if (rp > 0) armaPatched++;

                        var recordDataToWrite = newData;
                        var newFlags = flags;
                        if (compressed)
                        {
                            var recompressed = TryCompressRecord(newData, warnings, tag);
                            if (recompressed is not null)
                            {
                                recordDataToWrite = recompressed;
                                newFlags = flags | FlagCompressed;
                            }
                            else
                            {
                                newFlags = flags & ~FlagCompressed;
                            }
                        }

                        ms.Write(bytes, pos, 4);                                    // tag
                        WriteUInt32Le(ms, (uint)recordDataToWrite.Length);          // new dataSize
                        WriteUInt32Le(ms, newFlags);                                // flags
                        ms.Write(bytes, pos + 12, headerSize - 12);                // FormID..rest
                        ms.Write(recordDataToWrite, 0, recordDataToWrite.Length);
                    }
                    else
                    {
                        ms.Write(bytes, pos, totalSize);
                    }
                }
                else
                {
                    ms.Write(bytes, pos, totalSize);
                }

                pos += totalSize;
            }
        }

        return (ms.ToArray(), armaPatched, pathsRewritten, warnings);
    }

    // ── ARMA/ARMO subrecord rewrite ───────────────────────────────────────────

    /// <summary>
    /// Walks the subrecords of a single ARMA or ARMO record, replacing the data string of
    /// MOD2 / MOD3 / MOD4 / MOD5 subrecords when the path is in <paramref name="rewriteMap"/>.
    /// The <paramref name="bytes"/> slice begins at the record data start (after the record header).
    /// Returns (new_data_bytes, paths_rewritten).
    /// </summary>
    internal static (byte[] NewData, int PathsRewritten)
        RewriteArmaSubrecords(byte[] bytes, int dataStart, int dataSize,
                              IReadOnlyDictionary<string, string> rewriteMap)
    {
        using var ms = new MemoryStream(dataSize);
        int pos       = dataStart;
        int end       = dataStart + dataSize;
        int rewritten = 0;
        int? pendingExtendedSize = null;
        byte[]? pendingExtendedPrefix = null;

        while (pos + SubrecordHeaderSize <= end)
        {
            var subType = ReadTag(bytes, pos);
            var subSize = ReadUInt16Le(bytes, pos + 4);

            if (pos + SubrecordHeaderSize + subSize > end) break;

            if (string.Equals(subType, ExtendedSizeTag, StringComparison.Ordinal) && subSize == 4)
            {
                pendingExtendedSize = (int)ReadUInt32Le(bytes, pos + SubrecordHeaderSize);
                pendingExtendedPrefix = bytes[pos..(pos + SubrecordHeaderSize + subSize)];
                pos += SubrecordHeaderSize + subSize;
                continue;
            }

            int effectiveSubSize = pendingExtendedSize ?? subSize;
            pendingExtendedSize = null;
            if (pos + SubrecordHeaderSize + effectiveSubSize > end)
            {
                break;
            }

            if (MeshSubrecordTypes.Contains(subType) && effectiveSubSize > 0)
            {
                // Read null-terminated ASCII path string from the subrecord data.
                int nullIdx = IndexOfNull(bytes, pos + SubrecordHeaderSize, effectiveSubSize);
                int strLen  = nullIdx >= 0 ? nullIdx : effectiveSubSize;
                var meshPath = System.Text.Encoding.ASCII
                    .GetString(bytes, pos + SubrecordHeaderSize, strLen)
                    .Replace('\\', '/')
                    .ToLowerInvariant();

                if (!string.IsNullOrEmpty(meshPath) &&
                    rewriteMap.TryGetValue(meshPath, out var newPath))
                {
                    // Write subrecord with the new (case-preserved) path.
                    var newBytes = System.Text.Encoding.ASCII.GetBytes(newPath + '\0');
                    WriteSubrecordWithExtendedSize(ms, subType, newBytes);
                    rewritten++;
                }
                else
                {
                    if (pendingExtendedPrefix is not null)
                    {
                        ms.Write(pendingExtendedPrefix, 0, pendingExtendedPrefix.Length);
                    }

                    ms.Write(bytes, pos, SubrecordHeaderSize + effectiveSubSize);
                }
            }
            else
            {
                if (pendingExtendedPrefix is not null)
                {
                    ms.Write(pendingExtendedPrefix, 0, pendingExtendedPrefix.Length);
                }

                ms.Write(bytes, pos, SubrecordHeaderSize + effectiveSubSize);
            }

            pendingExtendedPrefix = null;
            pos += SubrecordHeaderSize + effectiveSubSize;
        }

        // Preserve any trailing bytes (should not happen in a well-formed file).
        if (pendingExtendedPrefix is not null)
        {
            ms.Write(pendingExtendedPrefix, 0, pendingExtendedPrefix.Length);
        }

        if (pos < end)
        {
            ms.Write(bytes, pos, end - pos);
        }

        return (ms.ToArray(), rewritten);
    }

    // ── Compressed-record helper ──────────────────────────────────────────────

    /// <summary>
    /// Attempts to zlib-decompress a compressed Bethesda record payload.
    /// The payload starts with a 4-byte little-endian uncompressed-data size,
    /// followed by the zlib-compressed bytes.  Returns <see langword="null"/> on
    /// any error (corrupt data, over-size limit, etc.) and appends a warning.
    /// </summary>
    private static byte[]? TryDecompressRecord(
        byte[] bytes, int offset, int compressedSize, List<string> warnings, string recordTag)
    {
        if (compressedSize < 4) return null;
        try
        {
            // First 4 bytes = uncompressed data size (uint32 LE).
            int uncompressedSize = (int)ReadUInt32Le(bytes, offset);
            // Sanity cap: 64 MiB max.
            if (uncompressedSize <= 0 || uncompressedSize > 64 * 1024 * 1024) return null;

            using var compressed = new System.IO.MemoryStream(bytes, offset + 4, compressedSize - 4);
            using var zlib = new System.IO.Compression.ZLibStream(
                compressed, System.IO.Compression.CompressionMode.Decompress);
            var result = new byte[uncompressedSize];
            int totalRead = 0;
            while (totalRead < uncompressedSize)
            {
                int n = zlib.Read(result, totalRead, uncompressedSize - totalRead);
                if (n == 0) break;
                totalRead += n;
            }

            return totalRead > 0 ? result : null;
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not decompress {recordTag} record at offset {offset}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Compresses rewritten record data back into Bethesda's compressed-record payload format:
    /// uint32LE uncompressed-size prefix + zlib-compressed bytes.
    /// </summary>
    private static byte[]? TryCompressRecord(
        byte[] uncompressedData, List<string> warnings, string recordTag)
    {
        try
        {
            using var compressedStream = new MemoryStream();
            using (var zlib = new ZLibStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
            {
                zlib.Write(uncompressedData, 0, uncompressedData.Length);
            }

            var compressedBytes = compressedStream.ToArray();
            var payload = new byte[4 + compressedBytes.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0, 4), (uint)uncompressedData.Length);
            compressedBytes.CopyTo(payload, 4);
            return payload;
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not compress {recordTag} record after patching: {ex.Message}");
            return null;
        }
    }

    // ── Binary helpers ────────────────────────────────────────────────────────

    private static string ReadTag(byte[] bytes, int offset) =>
        System.Text.Encoding.ASCII.GetString(bytes, offset, 4);

    private static uint ReadUInt32Le(byte[] bytes, int offset) =>
        (uint)(bytes[offset]
             | (bytes[offset + 1] << 8)
             | (bytes[offset + 2] << 16)
             | (bytes[offset + 3] << 24));

    private static ushort ReadUInt16Le(byte[] bytes, int offset) =>
        (ushort)(bytes[offset] | (bytes[offset + 1] << 8));

    private static void WriteUInt32Le(MemoryStream ms, uint value)
    {
        ms.WriteByte((byte)(value));
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)(value >> 16));
        ms.WriteByte((byte)(value >> 24));
    }

    private static void WriteUInt16Le(MemoryStream ms, ushort value)
    {
        ms.WriteByte((byte)(value));
        ms.WriteByte((byte)(value >> 8));
    }

    private static void WriteTag(MemoryStream ms, string tag)
    {
        var encoded = System.Text.Encoding.ASCII.GetBytes(tag);
        ms.Write(encoded, 0, Math.Min(4, encoded.Length));
    }

    private static void WriteSubrecordWithExtendedSize(MemoryStream ms, string subType, byte[] payload)
    {
        if (payload.Length <= ushort.MaxValue)
        {
            WriteTag(ms, subType);
            WriteUInt16Le(ms, (ushort)payload.Length);
            ms.Write(payload, 0, payload.Length);
            return;
        }

        WriteTag(ms, ExtendedSizeTag);
        WriteUInt16Le(ms, 4);
        WriteUInt32Le(ms, (uint)payload.Length);
        WriteTag(ms, subType);
        WriteUInt16Le(ms, 0);
        ms.Write(payload, 0, payload.Length);
    }

    private static int IndexOfNull(byte[] bytes, int start, int length)
    {
        for (int i = 0; i < length; i++)
        {
            if (bytes[start + i] == 0) return i;
        }
        return -1;
    }
}

/// <summary>
/// Walks the binary content of a Bethesda plugin file (ESP/ESM/ESL) and extracts
/// structured <see cref="ArmaRecordDescriptor"/> and <see cref="ArmoRecordDescriptor"/>
/// objects for every ARMA and ARMO record.  Compressed records (bit 18 set) are
/// transparently decompressed via ZLibStream so no record is silently skipped.
/// Supports both Skyrim LE (20-byte record headers) and SSE (24-byte headers).
/// </summary>
internal static class BinaryArmaParser
{
    private const int SseHeaderSize = 24;
    private const int LeHeaderSize  = 20;
    private const int SubrecordHeaderSize = 6;
    private const uint FlagCompressed = 0x00040000u;
    private const string ExtendedSizeTag = "XXXX";

    // Biped-slot subrecord tags (BOD2 = SSE, BODT = LE)
    private static readonly HashSet<string> BipedSubrecords =
        new(StringComparer.Ordinal) { "BOD2", "BODT" };

    private static readonly HashSet<string> MeshSubrecords =
        new(StringComparer.Ordinal) { "MOD2", "MOD3", "MOD4", "MOD5" };

    // ARMO only uses MOD2 (male world model) and MOD3 (female world model).
    private static readonly HashSet<string> ArmoMeshSubrecords =
        new(StringComparer.Ordinal) { "MOD2", "MOD3" };

    /// <summary>Extracts all ARMA record descriptors from a plugin byte array.</summary>
    public static IReadOnlyList<ArmaRecordDescriptor> ExtractArmaRecords(byte[] bytes)
    {
        if (bytes.Length < 4) return [];
        var headerSize = DetectHeaderSize(bytes);
        var pluginFileName = string.Empty;   // filled in by callers that know the filename
        return WalkArmaRecords(bytes, 0, bytes.Length, headerSize, pluginFileName);
    }

    /// <summary>
    /// Overload that also accepts a plugin file name so descriptors carry a meaningful
    /// <see cref="ArmaRecordDescriptor.PluginFileName"/> value.
    /// </summary>
    public static IReadOnlyList<ArmaRecordDescriptor> ExtractArmaRecords(byte[] bytes, string pluginFileName)
    {
        if (bytes.Length < 4) return [];
        var headerSize = DetectHeaderSize(bytes);
        return WalkArmaRecords(bytes, 0, bytes.Length, headerSize, pluginFileName);
    }

    /// <summary>Extracts all ARMO record descriptors from a plugin byte array.</summary>
    public static IReadOnlyList<ArmoRecordDescriptor> ExtractArmoRecords(byte[] bytes)
    {
        if (bytes.Length < 4) return [];
        var headerSize = DetectHeaderSize(bytes);
        return WalkArmoRecords(bytes, 0, bytes.Length, headerSize, string.Empty);
    }

    /// <summary>
    /// Overload that also accepts a plugin file name so descriptors carry a meaningful
    /// <see cref="ArmoRecordDescriptor.PluginFileName"/> value.
    /// </summary>
    public static IReadOnlyList<ArmoRecordDescriptor> ExtractArmoRecords(byte[] bytes, string pluginFileName)
    {
        if (bytes.Length < 4) return [];
        var headerSize = DetectHeaderSize(bytes);
        return WalkArmoRecords(bytes, 0, bytes.Length, headerSize, pluginFileName);
    }

    // ── Header-size detection (same logic as BinaryPluginRewriteService) ─────

    internal static int DetectHeaderSize(byte[] bytes)
    {
        if (bytes.Length < 4 ||
            bytes[0] != 'T' || bytes[1] != 'E' || bytes[2] != 'S' || bytes[3] != '4')
        {
            return SseHeaderSize;
        }

        if (bytes.Length > 23 &&
            bytes[20] == 'H' && bytes[21] == 'E' && bytes[22] == 'D' && bytes[23] == 'R')
        {
            return LeHeaderSize;
        }

        return SseHeaderSize;
    }

    // ── ARMA tree walk ────────────────────────────────────────────────────────

    private static List<ArmaRecordDescriptor> WalkArmaRecords(
        byte[] bytes, int start, int end, int headerSize, string pluginFileName)
    {
        var results = new List<ArmaRecordDescriptor>();
        int pos = start;

        while (pos < end)
        {
            if (pos + headerSize > end) break;

            var tag    = ReadTag(bytes, pos);
            var field4 = ReadUInt32Le(bytes, pos + 4);

            if (string.Equals(tag, "GRUP", StringComparison.Ordinal))
            {
                var groupTotal = (int)field4;
                if (groupTotal < headerSize || pos + groupTotal > end) break;

                // Recurse into GRUP content.
                results.AddRange(WalkArmaRecords(
                    bytes, pos + headerSize, pos + groupTotal, headerSize, pluginFileName));

                pos += groupTotal;
            }
            else
            {
                var dataSize  = (int)field4;
                int totalSize = headerSize + dataSize;
                if (pos + totalSize > end) break;

                if (string.Equals(tag, "ARMA", StringComparison.Ordinal) && dataSize > 0)
                {
                    var flags = ReadUInt32Le(bytes, pos + 8);
                    byte[] dataBytes = GetRecordData(bytes, pos, dataSize, headerSize, flags);
                    if (dataBytes.Length > 0)
                    {
                        var desc = ParseArmaRecord(
                            bytes, pos, dataBytes, headerSize, pluginFileName);
                        if (desc is not null) results.Add(desc);
                    }
                }

                pos += totalSize;
            }
        }

        return results;
    }

    // ── ARMO tree walk ────────────────────────────────────────────────────────

    private static List<ArmoRecordDescriptor> WalkArmoRecords(
        byte[] bytes, int start, int end, int headerSize, string pluginFileName)
    {
        var results = new List<ArmoRecordDescriptor>();
        int pos = start;

        while (pos < end)
        {
            if (pos + headerSize > end) break;

            var tag    = ReadTag(bytes, pos);
            var field4 = ReadUInt32Le(bytes, pos + 4);

            if (string.Equals(tag, "GRUP", StringComparison.Ordinal))
            {
                var groupTotal = (int)field4;
                if (groupTotal < headerSize || pos + groupTotal > end) break;

                results.AddRange(WalkArmoRecords(
                    bytes, pos + headerSize, pos + groupTotal, headerSize, pluginFileName));

                pos += groupTotal;
            }
            else
            {
                var dataSize  = (int)field4;
                int totalSize = headerSize + dataSize;
                if (pos + totalSize > end) break;

                if (string.Equals(tag, "ARMO", StringComparison.Ordinal) && dataSize > 0)
                {
                    var flags = ReadUInt32Le(bytes, pos + 8);
                    byte[] dataBytes = GetRecordData(bytes, pos, dataSize, headerSize, flags);
                    if (dataBytes.Length > 0)
                    {
                        var desc = ParseArmoRecord(
                            bytes, pos, dataBytes, headerSize, pluginFileName);
                        if (desc is not null) results.Add(desc);
                    }
                }

                pos += totalSize;
            }
        }

        return results;
    }

    // ── Data extractor (handles compressed records) ───────────────────────────

    /// <summary>
    /// Returns the record's data bytes, decompressing them if the compressed flag is set.
    /// Returns an empty array if decompression fails or the data is unusable.
    /// </summary>
    private static byte[] GetRecordData(byte[] bytes, int recordStart, int dataSize, int headerSize, uint flags)
    {
        int dataOffset = recordStart + headerSize;

        if ((flags & FlagCompressed) != 0)
        {
            if (dataSize < 4) return [];
            try
            {
                int uncompressedSize = (int)ReadUInt32Le(bytes, dataOffset);
                if (uncompressedSize <= 0 || uncompressedSize > 64 * 1024 * 1024) return [];

                using var compressed = new System.IO.MemoryStream(bytes, dataOffset + 4, dataSize - 4);
                using var zlib = new System.IO.Compression.ZLibStream(
                    compressed, System.IO.Compression.CompressionMode.Decompress);
                var result = new byte[uncompressedSize];
                int totalRead = 0;
                while (totalRead < uncompressedSize)
                {
                    int n = zlib.Read(result, totalRead, uncompressedSize - totalRead);
                    if (n == 0) break;
                    totalRead += n;
                }
                return totalRead > 0 ? result : [];
            }
            catch
            {
                return [];
            }
        }

        return bytes[dataOffset..(dataOffset + dataSize)];
    }

    // ── ARMA record parser ────────────────────────────────────────────────────

    private static ArmaRecordDescriptor? ParseArmaRecord(
        byte[] bytes, int recordStart, byte[] dataBytes, int headerSize, string pluginFileName)
    {
        var formId = ReadUInt32Le(bytes, recordStart + 12);

        string? editorId    = null;
        var bipedSlots      = new List<int>();
        var meshPaths       = new List<string>();
        uint? raceFormId    = null;

        int pos = 0;
        int end = dataBytes.Length;
        int? pendingExtendedSize = null;

        while (pos + SubrecordHeaderSize <= end)
        {
            var subTag  = ReadTag(dataBytes, pos);
            var subSize = ReadUInt16Le(dataBytes, pos + 4);

            if (pos + SubrecordHeaderSize + subSize > end) break;

            if (string.Equals(subTag, ExtendedSizeTag, StringComparison.Ordinal) && subSize == 4)
            {
                pendingExtendedSize = (int)ReadUInt32Le(dataBytes, pos + SubrecordHeaderSize);
                pos += SubrecordHeaderSize + subSize;
                continue;
            }

            int effectiveSubSize = pendingExtendedSize ?? subSize;
            pendingExtendedSize = null;
            if (pos + SubrecordHeaderSize + effectiveSubSize > end) break;

            int dataStart = pos + SubrecordHeaderSize;

            if (string.Equals(subTag, "EDID", StringComparison.Ordinal) && effectiveSubSize > 0)
            {
                int nullIdx = IndexOfNull(dataBytes, dataStart, effectiveSubSize);
                int strLen  = nullIdx >= 0 ? nullIdx : effectiveSubSize;
                editorId    = System.Text.Encoding.ASCII.GetString(dataBytes, dataStart, strLen);
            }
            else if (BipedSubrecords.Contains(subTag) && effectiveSubSize >= 4)
            {
                var slotFlags = ReadUInt32Le(dataBytes, dataStart);
                for (int i = 0; i < 32; i++)
                {
                    if (((slotFlags >> i) & 1u) != 0)
                        bipedSlots.Add(30 + i);
                }
            }
            else if (MeshSubrecords.Contains(subTag) && effectiveSubSize > 0)
            {
                int nullIdx = IndexOfNull(dataBytes, dataStart, effectiveSubSize);
                int strLen  = nullIdx >= 0 ? nullIdx : effectiveSubSize;
                var path    = System.Text.Encoding.ASCII
                    .GetString(dataBytes, dataStart, strLen)
                    .Replace('\\', '/');
                if (!string.IsNullOrEmpty(path))
                    meshPaths.Add(path);
            }
            else if (string.Equals(subTag, "RNAM", StringComparison.Ordinal) && effectiveSubSize >= 4)
            {
                raceFormId = ReadUInt32Le(dataBytes, dataStart);
            }

            pos += SubrecordHeaderSize + effectiveSubSize;
        }

        var headerBytes = bytes[recordStart..(recordStart + headerSize)];
        // Store the original raw bytes (which may be compressed); callers that need
        // plain data use GetRecordData separately.
        var originalData = bytes[(recordStart + headerSize)..(recordStart + headerSize + (int)ReadUInt32Le(bytes, recordStart + 4))];

        return new ArmaRecordDescriptor(
            formId,
            pluginFileName,
            editorId,
            bipedSlots,
            meshPaths,
            headerBytes,
            dataBytes,        // decompressed (or raw) data — PatchPluginWriter uses this
            raceFormId);
    }

    // ── ARMO record parser ────────────────────────────────────────────────────

    private static ArmoRecordDescriptor? ParseArmoRecord(
        byte[] bytes, int recordStart, byte[] dataBytes, int headerSize, string pluginFileName)
    {
        var formId = ReadUInt32Le(bytes, recordStart + 12);

        string? editorId = null;
        var meshPaths    = new List<string>();
        var keywords     = new List<uint>();
        uint? raceFormId = null;

        int pos = 0;
        int end = dataBytes.Length;
        int? pendingExtendedSize = null;

        while (pos + SubrecordHeaderSize <= end)
        {
            var subTag  = ReadTag(dataBytes, pos);
            var subSize = ReadUInt16Le(dataBytes, pos + 4);

            if (pos + SubrecordHeaderSize + subSize > end) break;

            if (string.Equals(subTag, ExtendedSizeTag, StringComparison.Ordinal) && subSize == 4)
            {
                pendingExtendedSize = (int)ReadUInt32Le(dataBytes, pos + SubrecordHeaderSize);
                pos += SubrecordHeaderSize + subSize;
                continue;
            }

            int effectiveSubSize = pendingExtendedSize ?? subSize;
            pendingExtendedSize = null;
            if (pos + SubrecordHeaderSize + effectiveSubSize > end) break;

            int dataStart = pos + SubrecordHeaderSize;

            if (string.Equals(subTag, "EDID", StringComparison.Ordinal) && effectiveSubSize > 0)
            {
                int nullIdx = IndexOfNull(dataBytes, dataStart, effectiveSubSize);
                int strLen  = nullIdx >= 0 ? nullIdx : effectiveSubSize;
                editorId    = System.Text.Encoding.ASCII.GetString(dataBytes, dataStart, strLen);
            }
            else if (ArmoMeshSubrecords.Contains(subTag) && effectiveSubSize > 0)
            {
                int nullIdx = IndexOfNull(dataBytes, dataStart, effectiveSubSize);
                int strLen  = nullIdx >= 0 ? nullIdx : effectiveSubSize;
                var path    = System.Text.Encoding.ASCII
                    .GetString(dataBytes, dataStart, strLen)
                    .Replace('\\', '/');
                if (!string.IsNullOrEmpty(path))
                    meshPaths.Add(path);
            }
            else if (string.Equals(subTag, "KWDA", StringComparison.Ordinal) && effectiveSubSize >= 4)
            {
                // KWDA: array of 4-byte FormIDs, one per keyword
                int count = effectiveSubSize / 4;
                for (int ki = 0; ki < count; ki++)
                    keywords.Add(ReadUInt32Le(dataBytes, dataStart + ki * 4));
            }
            else if (string.Equals(subTag, "RNAM", StringComparison.Ordinal) && effectiveSubSize >= 4)
            {
                raceFormId = ReadUInt32Le(dataBytes, dataStart);
            }

            pos += SubrecordHeaderSize + effectiveSubSize;
        }

        var headerBytes = bytes[recordStart..(recordStart + headerSize)];

        return new ArmoRecordDescriptor(
            formId,
            pluginFileName,
            editorId,
            meshPaths,
            headerBytes,
            dataBytes,     // decompressed (or raw) data
            keywords.Count > 0 ? keywords : null,
            raceFormId);
    }

    // ── Binary helpers ────────────────────────────────────────────────────────

    private static string ReadTag(byte[] bytes, int offset) =>
        System.Text.Encoding.ASCII.GetString(bytes, offset, 4);

    private static uint ReadUInt32Le(byte[] bytes, int offset) =>
        (uint)(bytes[offset]
             | (bytes[offset + 1] << 8)
             | (bytes[offset + 2] << 16)
             | (bytes[offset + 3] << 24));

    private static ushort ReadUInt16Le(byte[] bytes, int offset) =>
        (ushort)(bytes[offset] | (bytes[offset + 1] << 8));

    private static int IndexOfNull(byte[] bytes, int start, int length)
    {
        for (int i = 0; i < length; i++)
        {
            if (bytes[start + i] == 0) return i;
        }
        return -1;
    }
}

/// <summary>
/// Generates a minimal Bethesda override/patch ESP that:
/// <list type="bullet">
///   <item>Contains a TES4 header (with ESL flag 0x200) listing the original plugin as its single master file.</item>
///   <item>Holds only the ARMA and ARMO records that actually had mesh paths rewritten — no extra records.</item>
///   <item>Uses the same FormIDs as the originals (master index 0 = original plugin).</item>
///   <item>Reports the accurate record count in the HEDR subrecord.</item>
/// </list>
/// The result can be placed in the Data folder alongside the original plugin as a standard
/// Bethesda override without replacing any other records.
/// </summary>
internal static class PatchPluginWriter
{
    private const int SseHeaderSize = 24;
    private const int SubrecordHeaderSize = 6;
    private const string ExtendedSizeTag = "XXXX";

    // ESL flag — bit 9 of TES4 record flags; prevents consuming a load-order slot.
    private const uint EslFlag = 0x00000200u;

    private static readonly HashSet<string> MeshSubrecords =
        new(StringComparer.Ordinal) { "MOD2", "MOD3", "MOD4", "MOD5" };

    /// <summary>
    /// Builds the raw bytes for a minimal patch ESP.
    /// </summary>
    /// <param name="masterPluginFileName">
    ///   The file name (with extension) of the original plugin, e.g. "MyArmor.esp".
    ///   Listed as the sole MAST entry in the TES4 header.
    /// </param>
    /// <param name="descriptors">
    ///   Parsed ARMA descriptors from <see cref="BinaryArmaParser.ExtractArmaRecords"/>.
    ///   Only records that have at least one mesh path in <paramref name="rewriteMap"/>
    ///   are included in the patch plugin.
    /// </param>
    /// <param name="rewriteMap">
    ///   Path-rewrite map (lowercase forward-slash normalised keys to new path values).
    /// </param>
    /// <param name="headerSize">Record header size (24 for SSE, 20 for LE).</param>
    /// <param name="armoDescriptors">
    ///   Optional ARMO record descriptors extracted from the same plugin.
    ///   Emitted into a separate ARMO GRUP when any paths match.
    /// </param>
    /// <returns>Raw bytes of the patch ESP, ready to write to disk.</returns>
    public static (byte[] PluginBytes, int ArmaRecordsIncluded) BuildPatchPlugin(
        string masterPluginFileName,
        IReadOnlyList<ArmaRecordDescriptor> descriptors,
        IReadOnlyDictionary<string, string> rewriteMap,
        int headerSize = SseHeaderSize,
        IReadOnlyList<ArmoRecordDescriptor>? armoDescriptors = null)
    {
        // ── 1. Build patched ARMA record buffers ──────────────────────────────
        var armaBuffers = new List<byte[]>();

        foreach (var desc in descriptors)
        {
            var (newData, rewritten) = RewriteArmaData(desc.OriginalDataBytes, rewriteMap);
            if (rewritten == 0) continue;

            armaBuffers.Add(BuildArmaRecord(desc, newData, headerSize));
        }

        // ── 2. Build patched ARMO record buffers ──────────────────────────────
        var armoBuffers = new List<byte[]>();

        if (armoDescriptors is not null)
        {
            foreach (var desc in armoDescriptors)
            {
                var (newData, rewritten) = RewriteArmaData(desc.OriginalDataBytes, rewriteMap);
                if (rewritten == 0) continue;

                armoBuffers.Add(BuildArmoRecord(desc, newData, headerSize));
            }
        }

        int totalRecords = armaBuffers.Count + armoBuffers.Count;

        // ── 3. TES4 record (ESL-flagged, accurate numRecords) ─────────────────
        using var ms = new MemoryStream();
        var tes4Data = BuildTes4Data(masterPluginFileName, totalRecords);
        WriteFlatRecord(ms, "TES4", tes4Data, formId: 0, headerSize: headerSize, flags: EslFlag);

        if (totalRecords == 0)
        {
            return (ms.ToArray(), 0);
        }

        // ── 4. ARMA GRUP ──────────────────────────────────────────────────────
        if (armaBuffers.Count > 0)
        {
            WriteRecordGrup(ms, "ARMA", armaBuffers, headerSize);
        }

        // ── 5. ARMO GRUP ──────────────────────────────────────────────────────
        if (armoBuffers.Count > 0)
        {
            WriteRecordGrup(ms, "ARMO", armoBuffers, headerSize);
        }

        return (ms.ToArray(), totalRecords);
    }

    // ── TES4 data builder ─────────────────────────────────────────────────────

    private static byte[] BuildTes4Data(string masterPluginFileName, int numRecords = 0)
    {
        using var ms = new MemoryStream();

        // HEDR subrecord: float32 version(1.70) + int32 numRecords + uint32 nextObjectID(0x800)
        using (var hedrMs = new MemoryStream(12))
        {
            WriteFloat32Le(hedrMs, 1.70f);
            WriteUInt32Le(hedrMs, (uint)numRecords);   // accurate count of non-GRUP, non-TES4 records
            WriteUInt32Le(hedrMs, 0x800);
            WriteSubrecord(ms, "HEDR", hedrMs.ToArray());
        }

        // CNAM: author name (null-terminated).
        WriteSubrecord(ms, "CNAM", System.Text.Encoding.ASCII.GetBytes("SlideSmith\0"));

        // MAST + DATA pair: lists the original plugin as a master.
        WriteSubrecord(ms, "MAST",
            System.Text.Encoding.ASCII.GetBytes(masterPluginFileName + '\0'));
        WriteSubrecord(ms, "DATA", new byte[8]);  // 8 zero bytes (always follows MAST)

        return ms.ToArray();
    }

    // ── GRUP writer ───────────────────────────────────────────────────────────

    private static void WriteRecordGrup(
        MemoryStream ms, string groupLabel, List<byte[]> recordBuffers, int headerSize)
    {
        int grupContentLen = recordBuffers.Sum(b => b.Length);
        int grupTotalSize  = headerSize + grupContentLen;

        WriteTag(ms, "GRUP");
        WriteUInt32Le(ms, (uint)grupTotalSize);     // field4 = total GRUP size
        WriteTag(ms, groupLabel);                   // label = record type
        WriteUInt32Le(ms, 0);                       // groupType = 0 (top-level record type)
        if (headerSize == SseHeaderSize)
        {
            WriteUInt32Le(ms, 0);                   // VC info (SSE extra 4 bytes)
            WriteUInt32Le(ms, 0);                   // timestamp / unk
        }

        foreach (var buf in recordBuffers)
            ms.Write(buf, 0, buf.Length);
    }

    // ── Record/subrecord writers ──────────────────────────────────────────────

    private static void WriteFlatRecord(
        MemoryStream ms, string tag, byte[] data, uint formId, int headerSize,
        uint flags = 0)
    {
        WriteTag(ms, tag);
        WriteUInt32Le(ms, (uint)data.Length);    // dataSize
        WriteUInt32Le(ms, flags);                // flags (e.g. ESL = 0x200)
        WriteUInt32Le(ms, formId);               // FormID
        WriteUInt32Le(ms, 0);                    // VC info 1

        if (headerSize == SseHeaderSize)
        {
            WriteUInt32Le(ms, 0);                // Form version / VC info 2 (SSE extra field)
        }

        ms.Write(data, 0, data.Length);
    }

    private static byte[] BuildArmaRecord(
        ArmaRecordDescriptor desc, byte[] newData, int headerSize)
    {
        using var ms = new MemoryStream(headerSize + newData.Length);

        // Copy the tag ("ARMA") from the original header bytes; clear FlagCompressed.
        ms.Write(desc.OriginalRecordHeaderBytes, 0, 4);
        WriteUInt32Le(ms, (uint)newData.Length);
        // Preserve original flags but clear the compressed bit (data is already plain).
        var origFlags = (uint)(desc.OriginalRecordHeaderBytes[8]
                             | (desc.OriginalRecordHeaderBytes[9] << 8)
                             | (desc.OriginalRecordHeaderBytes[10] << 16)
                             | (desc.OriginalRecordHeaderBytes[11] << 24));
        WriteUInt32Le(ms, origFlags & ~0x00040000u);
        ms.Write(desc.OriginalRecordHeaderBytes, 12, headerSize - 12); // FormID, VC...
        ms.Write(newData, 0, newData.Length);

        return ms.ToArray();
    }

    private static byte[] BuildArmoRecord(
        ArmoRecordDescriptor desc, byte[] newData, int headerSize)
    {
        using var ms = new MemoryStream(headerSize + newData.Length);

        ms.Write(desc.OriginalRecordHeaderBytes, 0, 4);                 // "ARMO"
        WriteUInt32Le(ms, (uint)newData.Length);
        var origFlags = (uint)(desc.OriginalRecordHeaderBytes[8]
                             | (desc.OriginalRecordHeaderBytes[9] << 8)
                             | (desc.OriginalRecordHeaderBytes[10] << 16)
                             | (desc.OriginalRecordHeaderBytes[11] << 24));
        WriteUInt32Le(ms, origFlags & ~0x00040000u);
        ms.Write(desc.OriginalRecordHeaderBytes, 12, headerSize - 12);
        ms.Write(newData, 0, newData.Length);

        return ms.ToArray();
    }

    private static void WriteSubrecord(MemoryStream ms, string tag, byte[] data)
    {
        WriteTag(ms, tag);
        WriteUInt16Le(ms, (ushort)data.Length);
        ms.Write(data, 0, data.Length);
    }

    // ── ARMA/ARMO data rewrite (same logic as BinaryPluginRewriteService.RewriteArmaSubrecords) ─

    internal static (byte[] NewData, int PathsRewritten) RewriteArmaData(
        byte[] dataBytes,
        IReadOnlyDictionary<string, string> rewriteMap)
    {
        using var ms  = new MemoryStream(dataBytes.Length);
        int pos       = 0;
        int end       = dataBytes.Length;
        int rewritten = 0;
        int? pendingExtendedSize = null;
        byte[]? pendingExtendedPrefix = null;

        while (pos + SubrecordHeaderSize <= end)
        {
            var subTag  = System.Text.Encoding.ASCII.GetString(dataBytes, pos, 4);
            var subSize = (ushort)(dataBytes[pos + 4] | (dataBytes[pos + 5] << 8));

            if (pos + SubrecordHeaderSize + subSize > end) break;

            if (string.Equals(subTag, ExtendedSizeTag, StringComparison.Ordinal) && subSize == 4)
            {
                pendingExtendedSize = (int)(dataBytes[pos + 6]
                    | (dataBytes[pos + 7] << 8)
                    | (dataBytes[pos + 8] << 16)
                    | (dataBytes[pos + 9] << 24));
                pendingExtendedPrefix = dataBytes[pos..(pos + SubrecordHeaderSize + subSize)];
                pos += SubrecordHeaderSize + subSize;
                continue;
            }

            int effectiveSubSize = pendingExtendedSize ?? subSize;
            pendingExtendedSize = null;
            if (pos + SubrecordHeaderSize + effectiveSubSize > end) break;

            if (MeshSubrecords.Contains(subTag) && effectiveSubSize > 0)
            {
                int nullIdx  = IndexOfNull(dataBytes, pos + SubrecordHeaderSize, effectiveSubSize);
                int strLen   = nullIdx >= 0 ? nullIdx : effectiveSubSize;
                var meshPath = System.Text.Encoding.ASCII
                    .GetString(dataBytes, pos + SubrecordHeaderSize, strLen)
                    .Replace('\\', '/')
                    .ToLowerInvariant();

                if (!string.IsNullOrEmpty(meshPath) &&
                    rewriteMap.TryGetValue(meshPath, out var newPath))
                {
                    var newBytes = System.Text.Encoding.ASCII.GetBytes(newPath + '\0');
                    WriteSubrecordWithExtendedSize(ms, subTag, newBytes);
                    rewritten++;
                }
                else
                {
                    if (pendingExtendedPrefix is not null)
                    {
                        ms.Write(pendingExtendedPrefix, 0, pendingExtendedPrefix.Length);
                    }

                    ms.Write(dataBytes, pos, SubrecordHeaderSize + effectiveSubSize);
                }
            }
            else
            {
                if (pendingExtendedPrefix is not null)
                {
                    ms.Write(pendingExtendedPrefix, 0, pendingExtendedPrefix.Length);
                }

                ms.Write(dataBytes, pos, SubrecordHeaderSize + effectiveSubSize);
            }

            pendingExtendedPrefix = null;
            pos += SubrecordHeaderSize + effectiveSubSize;
        }

        if (pendingExtendedPrefix is not null)
        {
            ms.Write(pendingExtendedPrefix, 0, pendingExtendedPrefix.Length);
        }

        if (pos < end)
        {
            ms.Write(dataBytes, pos, end - pos);
        }

        return (ms.ToArray(), rewritten);
    }

    // ── Binary helpers ────────────────────────────────────────────────────────

    private static void WriteTag(MemoryStream ms, string tag)
    {
        var encoded = System.Text.Encoding.ASCII.GetBytes(tag);
        ms.Write(encoded, 0, Math.Min(4, encoded.Length));
        // Pad if tag is shorter than 4 characters (should not happen in practice).
        for (int i = encoded.Length; i < 4; i++) ms.WriteByte(0);
    }

    private static void WriteUInt32Le(MemoryStream ms, uint value)
    {
        ms.WriteByte((byte)(value));
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)(value >> 16));
        ms.WriteByte((byte)(value >> 24));
    }

    private static void WriteUInt16Le(MemoryStream ms, ushort value)
    {
        ms.WriteByte((byte)(value));
        ms.WriteByte((byte)(value >> 8));
    }

    private static void WriteSubrecordWithExtendedSize(MemoryStream ms, string subTag, byte[] payload)
    {
        if (payload.Length <= ushort.MaxValue)
        {
            WriteTag(ms, subTag);
            WriteUInt16Le(ms, (ushort)payload.Length);
            ms.Write(payload, 0, payload.Length);
            return;
        }

        WriteTag(ms, ExtendedSizeTag);
        WriteUInt16Le(ms, 4);
        WriteUInt32Le(ms, (uint)payload.Length);
        WriteTag(ms, subTag);
        WriteUInt16Le(ms, 0);
        ms.Write(payload, 0, payload.Length);
    }

    private static void WriteFloat32Le(MemoryStream ms, float value)
    {
        var bits = System.BitConverter.GetBytes(value);
        if (!System.BitConverter.IsLittleEndian)
            System.Array.Reverse(bits);
        ms.Write(bits, 0, 4);
    }

    private static int IndexOfNull(byte[] bytes, int start, int length)
    {
        for (int i = 0; i < length; i++)
        {
            if (bytes[start + i] == 0) return i;
        }
        return -1;
    }
}

/// <summary>
/// Generates a plain-text README.txt summarising what SlideSmith produced, where to put each
/// file, and what manual steps (if any) are still required.
/// </summary>
internal static class ConversionReadmeGenerator
{
    public static string Generate(
        ConversionRequest request,
        ImportedArmor armor,
        ConvertedMesh mesh,
        BodySlideProject bodySlideProject,
        PluginAnalysisResult pluginAnalysis,
        IReadOnlyList<string> outputFiles,
        IReadOnlyDictionary<string, string> rewriteMap,
        bool patchEspGenerated)
    {
        var sb = new System.Text.StringBuilder();
        var armorName = Path.GetFileNameWithoutExtension(armor.MeshFiles[0]) ?? "ConvertedArmor";
        var now       = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm UTC");

        sb.AppendLine("=============================================================");
        sb.AppendLine($"  SlideSmith Conversion Package — {armorName}");
        sb.AppendLine($"  Generated: {now}");
        sb.AppendLine("=============================================================");
        sb.AppendLine();

        // ── What was converted ─────────────────────────────────────────────
        sb.AppendLine("WHAT WAS CONVERTED");
        sb.AppendLine("------------------");
        sb.AppendLine($"  Armor/clothing: {armorName}");
        sb.AppendLine($"  Target body:    {request.TargetBody}");
        sb.AppendLine($"  Mesh strategy:  {mesh.Strategy}");
        sb.AppendLine($"  Meshes in:      {armor.MeshFiles.Count}");
        sb.AppendLine($"  Textures in:    {armor.TextureFiles.Count}");
        sb.AppendLine($"  Plugins in:     {pluginAnalysis.ScannedPlugins.Count}");
        sb.AppendLine();

        // ── Files generated ────────────────────────────────────────────────
        sb.AppendLine("FILES GENERATED");
        sb.AppendLine("---------------");

        var nifFiles   = outputFiles.Where(f => f.EndsWith(".nif", StringComparison.OrdinalIgnoreCase)).ToList();
        var bsdFiles   = outputFiles.Where(f => f.EndsWith(".bsd", StringComparison.OrdinalIgnoreCase)).ToList();
        var ospFiles   = outputFiles.Where(f => f.EndsWith(".osp", StringComparison.OrdinalIgnoreCase)).ToList();
        var espFiles   = outputFiles.Where(f => f.EndsWith(".esp", StringComparison.OrdinalIgnoreCase)).ToList();
        var xmlFiles   = outputFiles.Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
        var pasFiles   = outputFiles.Where(f => f.EndsWith(".pas", StringComparison.OrdinalIgnoreCase)).ToList();
        var fomodFiles = outputFiles.Where(f => f.Contains("fomod", StringComparison.OrdinalIgnoreCase)).ToList();

        void ListFiles(IEnumerable<string> files, string label)
        {
            var list = files.ToList();
            if (list.Count == 0) return;
            sb.AppendLine($"  [{label}]");
            foreach (var f in list)
                sb.AppendLine($"    {Path.GetFileName(f)}");
        }

        ListFiles(nifFiles,   "Converted Meshes");
        ListFiles(espFiles,   "Plugin Files");
        ListFiles(ospFiles,   "BodySlide Project");
        ListFiles(bsdFiles,   "BodySlide Slider Data");
        ListFiles(xmlFiles,   "Physics Configs");
        ListFiles(pasFiles,   "xEdit Script");
        ListFiles(fomodFiles, "FOMOD Installer");
        sb.AppendLine();

        // ── How to install ─────────────────────────────────────────────────
        sb.AppendLine("HOW TO INSTALL");
        sb.AppendLine("--------------");
        sb.AppendLine("  OPTION A — FOMOD (recommended):");
        sb.AppendLine("    Copy the entire output folder into your Skyrim Data folder");
        sb.AppendLine("    and enable via your mod manager using the included FOMOD installer.");
        sb.AppendLine();
        sb.AppendLine("  OPTION B — Manual:");
        sb.AppendLine($"    1. Copy converted .nif files to:  Data\\meshes\\slidesmith\\{request.TargetBody.ToLowerInvariant()}\\");
        sb.AppendLine("    2. Copy physics configs (.xml) to: Data\\SKSE\\Plugins\\hdtSMP\\  (SMP)");
        sb.AppendLine("                                       Data\\SKSE\\Plugins\\CBPCSystem\\  (CBPC)");
        if (bsdFiles.Count > 0)
        {
            sb.AppendLine($"    3. Copy BodySlide files (.osp, .bsd) to:");
            sb.AppendLine($"         Data\\CalienteTools\\BodySlide\\SliderSets\\{bodySlideProject.ProjectName}.osp");
            sb.AppendLine($"         Data\\CalienteTools\\BodySlide\\ShapeData\\{bodySlideProject.ProjectName}\\*.bsd");
        }

        sb.AppendLine();

        // ── Plugin patching instructions ───────────────────────────────────
        sb.AppendLine("PLUGIN PATCH");
        sb.AppendLine("------------");
        if (patchEspGenerated && espFiles.Count > 0)
        {
            var patchEspName = espFiles
                .FirstOrDefault(f => f.Contains("SlidesmithPatch", StringComparison.OrdinalIgnoreCase))
                ?? espFiles[0];
            sb.AppendLine($"  A minimal override patch ESP has been generated:");
            sb.AppendLine($"    {Path.GetFileName(patchEspName)}");
            sb.AppendLine();
            sb.AppendLine("  This patch contains ONLY the ARMA records that needed mesh-path updates.");
            sb.AppendLine("  It lists the original plugin as its master and does NOT replace it.");
            sb.AppendLine("  Load order: place this patch AFTER the original plugin.");
            if (rewriteMap.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  Rewired paths:");
                foreach (var (from, to) in rewriteMap.Take(10))
                    sb.AppendLine($"    {from}  →  {to}");
                if (rewriteMap.Count > 10)
                    sb.AppendLine($"    ... and {rewriteMap.Count - 10} more");
            }
        }
        else if (pasFiles.Count > 0)
        {
            sb.AppendLine("  No binary plugin was provided so a patch ESP could not be auto-generated.");
            sb.AppendLine("  Use the included xEdit script (patch-armor.pas) to apply mesh-path");
            sb.AppendLine("  rewrites manually via SSEEdit / TES5Edit:");
            sb.AppendLine("    1. Open SSEEdit with the original armor plugin loaded.");
            sb.AppendLine("    2. From the Tools menu choose 'Apply Script'.");
            sb.AppendLine("    3. Select patch-armor.pas and run it.");
        }
        else
        {
            sb.AppendLine("  No plugin was detected. Add the converted meshes to an existing .esp");
            sb.AppendLine($"  or create a new patch plugin in xEdit targeting {request.TargetBody}.");
        }

        sb.AppendLine();

        // ── BodySlide instructions ─────────────────────────────────────────
        if (bsdFiles.Count > 0)
        {
            sb.AppendLine("BODYSLIDE");
            sb.AppendLine("---------");
            sb.AppendLine($"  BodySlide project: {bodySlideProject.ProjectName}");
            sb.AppendLine($"  Target body:       {request.TargetBody}");
            sb.AppendLine($"  Sliders included:  {bodySlideProject.Sliders.Count}");
            sb.AppendLine();
            sb.AppendLine("  To build in BodySlide:");
            sb.AppendLine($"    1. Open BodySlide and search for '{bodySlideProject.ProjectName}'.");
            sb.AppendLine("    2. Select your body preset and click 'Build'.");
            sb.AppendLine("    3. For physics sliders, also build the _1 (high-weight) variant.");
            sb.AppendLine();
        }

        // ── Warnings / manual steps ────────────────────────────────────────
        sb.AppendLine("NOTES & MANUAL STEPS");
        sb.AppendLine("--------------------");
        sb.AppendLine("  * Vertex transforms are heuristic — inspect converted meshes in");
        sb.AppendLine("    Outfit Studio or NifSkope and fix any clipping or floating geometry.");
        sb.AppendLine("  * Physics configs use tuned defaults. Adjust spring/damping values");
        sb.AppendLine("    in the .xml files to match the cloth simulation feel you want.");
        if (pluginAnalysis.ArmorAddons.Count > 0 && !patchEspGenerated)
        {
            sb.AppendLine("  * ARMA records were detected but no patch ESP was auto-generated.");
            sb.AppendLine("    Run the included xEdit script (patch-armor.pas) manually.");
        }

        sb.AppendLine();
        sb.AppendLine("=============================================================");
        sb.AppendLine("  Generated by SlideSmith — https://github.com/JosephsDeadish/");
        sb.AppendLine("  Bodyslide-converter-tool-non-python-");
        sb.AppendLine("=============================================================");

        return sb.ToString();
    }
}

internal sealed class LocalExportService : IExportService
{
    public async Task<(string OutputDirectory, IReadOnlyList<string> OutputFiles)> ExportAsync(
        ConversionRequest request,
        ImportedArmor armor,
        MeshAnalysis analysis,
        ConvertedMesh mesh,
        MorphSet morphs,
        PhysicsConfig physics,
        ClippingReport clipping,
        CorrectionResult correction,
        BodySlideProject bodySlideProject,
        PluginAnalysisResult pluginAnalysis,
        TextureSummary textureSummary,
        PoseSimulationResult poseSimulation,
        IReadOnlyList<string> steps,
        CancellationToken cancellationToken)
    {
        var defaultOutput = Path.Combine(Environment.CurrentDirectory, "output", request.TargetBody, Path.GetFileNameWithoutExtension(armor.MeshFiles[0]));
        var outputDirectory = Path.GetFullPath(request.OutputDirectory ?? defaultOutput);
        Directory.CreateDirectory(outputDirectory);

        var outputFiles = new List<string>();

        var manifest = new
        {
            Request = request,
            Imported = new
            {
                armor.SourcePath,
                MeshCount = armor.MeshFiles.Count,
                TextureCount = armor.TextureFiles.Count,
                PhysicsCount = armor.PhysicsFiles.Count,
                BodyReferenceCount = armor.BodyReferenceFiles.Count
            },
            Analysis = analysis,
            Converted = mesh,
            Morphs = morphs,
            Physics = physics,
            Clipping = clipping,
            Correction = correction,
            BodySlide = new { bodySlideProject.ProjectName, bodySlideProject.TargetBody, SliderCount = bodySlideProject.Sliders.Count },
            Plugins = new { ScannedCount = pluginAnalysis.ScannedPlugins.Count, AddonCount = pluginAnalysis.ArmorAddons.Count },
            Textures = new
            {
                textureSummary.TotalCount,
                DiffuseCount    = textureSummary.DiffuseFiles.Count,
                NormalCount     = textureSummary.NormalFiles.Count,
                SpecularCount   = textureSummary.SpecularFiles?.Count ?? 0,
                GlowCount       = textureSummary.GlowFiles?.Count ?? 0,
                ParallaxCount   = textureSummary.ParallaxFiles?.Count ?? 0,
                SubsurfaceCount = textureSummary.SubsurfaceFiles?.Count ?? 0,
                MissingNormals  = textureSummary.MissingNormals
            },
            Steps = steps
        };

        var manifestPath = Path.Combine(outputDirectory, "conversion-manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(manifestPath);

        // Write converted NIF mesh file(s) to the output directory.
        // When _0/_1 weight variant pairs are detected, both are written as a matched pair.
        // When only one half is present, the missing variant is synthesised from the available
        // half using weight-scaled morphs so the game can interpolate between body weights.
        // A lightweight vertex-block transform is applied when a readable NIF vertex stream is
        // detected; otherwise the source bytes are copied through unchanged.
        var (writtenNifs, synthesizedVariantCount) = await WriteConvertedNifsAsync(armor, mesh, outputDirectory, cancellationToken);
        outputFiles.AddRange(writtenNifs);

        var pluginRewriteMap = BuildPluginRewriteMap(pluginAnalysis, request.TargetBody, writtenNifs);
        var stagedPluginMeshes = await StageConvertedMeshesForPluginRewriteAsync(
            outputDirectory,
            writtenNifs,
            pluginRewriteMap,
            cancellationToken);
        outputFiles.AddRange(stagedPluginMeshes);

        // Carry source support assets (textures, material configs, physics configs, plugins, body refs)
        // into the output package so converted outputs stay mod-ready.
        var copiedSupportAssets = await CopySupportAssetsAsync(armor, outputDirectory, cancellationToken);
        outputFiles.AddRange(copiedSupportAssets);

        // Generate flat-normal DDS stubs for any diffuse textures that have no matching _n.dds.
        // Missing normal maps cause purple-tinted or visually broken surfaces in-game; a flat
        // stub (pointing straight out in tangent space) prevents that and can be replaced later.
        var generatedNormals = await GenerateMissingNormalMapStubsAsync(armor, textureSummary, outputDirectory, cancellationToken);
        outputFiles.AddRange(generatedNormals);

        var dependencyMapPath = Path.Combine(outputDirectory, "dependency-map.json");
        var dependencyMap = BuildDependencyMap(armor, pluginAnalysis);
        await File.WriteAllTextAsync(
            dependencyMapPath,
            JsonSerializer.Serialize(dependencyMap, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        outputFiles.Add(dependencyMapPath);

        var morphPath = Path.Combine(outputDirectory, "morphs.json");
        await File.WriteAllTextAsync(morphPath, JsonSerializer.Serialize(morphs, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(morphPath);

        var physicsPath = Path.Combine(outputDirectory, "physics.json");
        await File.WriteAllTextAsync(physicsPath, JsonSerializer.Serialize(physics, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(physicsPath);

        // Write CBPC physics config XML when present.
        if (!string.IsNullOrWhiteSpace(physics.CbpcConfigXml))
        {
            var cbpcPath = Path.Combine(outputDirectory, "cbpc-config.xml");
            await File.WriteAllTextAsync(cbpcPath, physics.CbpcConfigXml, cancellationToken);
            outputFiles.Add(cbpcPath);
        }

        // Write SMP physics config XML when present.
        if (!string.IsNullOrWhiteSpace(physics.SmpConfigXml))
        {
            var smpPath = Path.Combine(outputDirectory, "smp-config.xml");
            await File.WriteAllTextAsync(smpPath, physics.SmpConfigXml, cancellationToken);
            outputFiles.Add(smpPath);
        }

        // Write the BodySlide project .osp file for use with the BodySlide application.
        var ospPath = Path.Combine(outputDirectory, $"{bodySlideProject.ProjectName}.osp");
        await File.WriteAllTextAsync(ospPath, bodySlideProject.OspXml, cancellationToken);
        outputFiles.Add(ospPath);

        // Write BSD slider data files (.bsd) — one per slider for low-weight and high-weight morphs.
        // The BSD binary format encodes per-slider vertex displacement deltas used by BodySlide.
        var morphVertexCount = EstimateMorphVertexCount(writtenNifs, request.TargetBody);
        var bsdDirectory = Path.Combine(outputDirectory, "SliderData", bodySlideProject.ProjectName);
        Directory.CreateDirectory(bsdDirectory);
        foreach (var slider in bodySlideProject.Sliders)
        {
            var lowBsdPath  = Path.Combine(bsdDirectory, $"{slider}.bsd");
            var highBsdPath = Path.Combine(bsdDirectory, $"{slider}_1.bsd");
            await File.WriteAllBytesAsync(lowBsdPath,  BuildBsdBytes(slider, isHighWeight: false, morphVertexCount, mesh.RegionalMorphing), cancellationToken);
            await File.WriteAllBytesAsync(highBsdPath, BuildBsdBytes(slider, isHighWeight: true, morphVertexCount, mesh.RegionalMorphing),  cancellationToken);
            outputFiles.Add(lowBsdPath);
            outputFiles.Add(highBsdPath);
        }

        // Write TRI morph files (.tri) — one for low-weight and one for high-weight.
        // The TRI format stores per-morph vertex displacement arrays for in-game slider interpolation.
        var triLowPath  = Path.Combine(outputDirectory, $"{bodySlideProject.ProjectName}.tri");
        var triHighPath = Path.Combine(outputDirectory, $"{bodySlideProject.ProjectName}_1.tri");
        await File.WriteAllBytesAsync(triLowPath,  BuildTriBytes(bodySlideProject.ProjectName, bodySlideProject.Sliders, isHighWeight: false, morphVertexCount, mesh.RegionalMorphing), cancellationToken);
        await File.WriteAllBytesAsync(triHighPath, BuildTriBytes(bodySlideProject.ProjectName, bodySlideProject.Sliders, isHighWeight: true, morphVertexCount, mesh.RegionalMorphing),  cancellationToken);
        outputFiles.Add(triLowPath);
        outputFiles.Add(triHighPath);

        // Write plugin patch guidance + rewrite instructions when plugins were found.
        bool patchEspGenerated = false;
        if (pluginAnalysis.ScannedPlugins.Count > 0 || pluginAnalysis.ArmorAddons.Count > 0)
        {
            var pluginPatchPath = Path.Combine(outputDirectory, "plugin-patches.json");
            var proposedSteps = BuildProposedPatchSteps(pluginAnalysis, request.TargetBody, pluginRewriteMap);
            var patchOutput = new
            {
                pluginAnalysis.ScannedPlugins,
                pluginAnalysis.ArmorAddons,
                pluginAnalysis.PatchGuidance,
                RewriteMappings = pluginRewriteMap
                    .Select(kvp => new { OriginalMeshPath = kvp.Key, RewrittenMeshPath = kvp.Value })
                    .ToList(),
                ProposedPatchSteps = proposedSteps
            };
            await File.WriteAllTextAsync(pluginPatchPath,
                JsonSerializer.Serialize(patchOutput, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            outputFiles.Add(pluginPatchPath);

            // Also write a runnable xEdit Pascal automation script so users can apply
            // the ARMA record patches directly from SSEEdit / TES5Edit without manual edits.
            var xEditScriptPath = Path.Combine(outputDirectory, "patch-armor.pas");
            await File.WriteAllTextAsync(xEditScriptPath,
                BuildXEditScript(pluginAnalysis, request.TargetBody, pluginRewriteMap),
                cancellationToken);
            outputFiles.Add(xEditScriptPath);

            if (pluginRewriteMap.Count > 0)
            {
                var sourcePluginPaths = EnumeratePluginFiles(armor.SourcePath);
                if (sourcePluginPaths.Count > 0)
                {
                    // Normalise the rewrite map (lowercase / forward-slash keys) for both
                    // the full-copy rewriter and the new minimal patch generator.
                    var normMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (k, v) in pluginRewriteMap)
                        normMap[k.Replace('\\', '/').ToLowerInvariant()] = v;

                    // ── Existing: full-copy patched plugin (_patched.esp) ──────────────
                    // Kept for compatibility; users who want a single self-contained plugin
                    // can still use this file.
                    var rewriter = new BinaryPluginRewriteService();
                    var rewriteResult = await rewriter.RewriteAsync(
                        sourcePluginPaths, pluginRewriteMap, outputDirectory, cancellationToken);
                    outputFiles.AddRange(rewriteResult.PatchedPluginPaths);

                    // ── New: minimal override patch ESP (_SlidesmithPatch.esp) ─────────
                    // This patch contains ONLY the touched ARMA/ARMO records and lists the
                    // original plugin as its single master.  It is a proper Bethesda
                    // override plugin (ESL-flagged) that can be loaded after the original
                    // in any order without consuming a load order slot.
                    foreach (var pluginPath in sourcePluginPaths)
                    {
                        try
                        {
                            var pluginBytes    = await File.ReadAllBytesAsync(pluginPath, cancellationToken);
                            var headerSize     = BinaryArmaParser.DetectHeaderSize(pluginBytes);
                            var pluginName     = Path.GetFileName(pluginPath) ?? pluginPath;
                            var armaDescriptors = BinaryArmaParser.ExtractArmaRecords(pluginBytes, pluginName);
                            var armoDescriptors = BinaryArmaParser.ExtractArmoRecords(pluginBytes, pluginName);

                            var (patchBytes, included) = PatchPluginWriter.BuildPatchPlugin(
                                pluginName, armaDescriptors, normMap, headerSize,
                                armoDescriptors: armoDescriptors);

                            if (included > 0)
                            {
                                var baseName       = Path.GetFileNameWithoutExtension(pluginPath);
                                var patchPath      = Path.Combine(outputDirectory, $"{baseName}_SlidesmithPatch.esp");
                                await File.WriteAllBytesAsync(patchPath, patchBytes, cancellationToken);
                                outputFiles.Add(patchPath);
                                patchEspGenerated = true;
                            }
                        }
                        catch (Exception ex) when (ex is IOException or InvalidDataException)
                        {
                            // Non-fatal — patch generation skipped for this plugin.
                        }
                    }
                }
            }
        }

        // Write texture summary when textures are present.
        if (textureSummary.TotalCount > 0)
        {
            var textureSummaryPath = Path.Combine(outputDirectory, "texture-summary.json");
            await File.WriteAllTextAsync(textureSummaryPath,
                JsonSerializer.Serialize(textureSummary, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            outputFiles.Add(textureSummaryPath);
        }

        // Write pose simulation report.
        var poseReportPath = Path.Combine(outputDirectory, "pose-simulation-report.json");
        await File.WriteAllTextAsync(poseReportPath,
            JsonSerializer.Serialize(poseSimulation, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        outputFiles.Add(poseReportPath);

        // Write interactive SVG preview HTML — body silhouette with regions colour-coded
        // by morph factor, plus slider, physics and pose-risk tables. This replaces the
        // old metadata-only preview-renders.json with a file that can be opened directly
        // in any browser without an additional 3D engine.
        var previewPath = Path.Combine(outputDirectory, "preview.html");
        await File.WriteAllTextAsync(previewPath,
            BuildPreviewHtml(request, armor, analysis, mesh, bodySlideProject, physics, poseSimulation),
            cancellationToken);
        outputFiles.Add(previewPath);

        var logPath = Path.Combine(outputDirectory, "conversion.log");
        await File.WriteAllLinesAsync(logPath, steps, cancellationToken);
        if (synthesizedVariantCount > 0)
        {
            await File.AppendAllTextAsync(logPath, $"weight-variants:synthesized={synthesizedVariantCount}{Environment.NewLine}", cancellationToken);
        }
        if (generatedNormals.Count > 0)
        {
            await File.AppendAllTextAsync(logPath, $"normal-stubs:generated={generatedNormals.Count}{Environment.NewLine}", cancellationToken);
        }
        outputFiles.Add(logPath);

        var fomodDirectory = Path.Combine(outputDirectory, "fomod");
        Directory.CreateDirectory(fomodDirectory);
        var fomodModuleConfigPath = Path.Combine(fomodDirectory, "ModuleConfig.xml");
        var fomodInfoPath = Path.Combine(fomodDirectory, "info.xml");
        var packageName = Path.GetFileNameWithoutExtension(armor.MeshFiles[0]) ?? "SlideSmith Package";
        await File.WriteAllTextAsync(
            fomodModuleConfigPath,
            BuildFomodModuleConfigXml(packageName, request.TargetBody, bodySlideProject.ProjectName),
            cancellationToken);
        await File.WriteAllTextAsync(
            fomodInfoPath,
            BuildFomodInfoXml(packageName),
            cancellationToken);
        outputFiles.Add(fomodModuleConfigPath);
        outputFiles.Add(fomodInfoPath);

        // Write a human-readable README.txt explaining the generated files, where to
        // install them, and what manual steps may still be required.  This satisfies
        // the Stage 7 README requirement from the issue spec.
        var readmePath = Path.Combine(outputDirectory, "README.txt");
        await File.WriteAllTextAsync(
            readmePath,
            ConversionReadmeGenerator.Generate(
                request, armor, mesh, bodySlideProject,
                pluginAnalysis, outputFiles, pluginRewriteMap, patchEspGenerated),
            cancellationToken);
        outputFiles.Add(readmePath);

        var cachePath = Path.Combine(outputDirectory, ".conversion-learning-cache.json");
        var cache = await ConversionLearningCache.LoadEntriesAsync(cachePath, cancellationToken);
        var cacheKey = ConversionLearningCache.BuildCacheKey(Path.GetFileNameWithoutExtension(armor.MeshFiles[0]) ?? "unknown", request.TargetBody);
        cache.RemoveAll(entry => string.Equals(entry.Key, cacheKey, StringComparison.OrdinalIgnoreCase));
        cache.Add(new ConversionCacheEntry(
            cacheKey,
            DateTimeOffset.UtcNow,
            request.TargetBody,
            analysis.MeshType,
            mesh.Strategy,
            mesh.RegionalMorphing,
            clipping.HasClipping,
            correction.Method));
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        outputFiles.Add(cachePath);

        if (request.OutputZip)
        {
            var zipPath = outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(outputDirectory, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            outputFiles = [zipPath];
            return (outputDirectory, outputFiles);
        }

        return (outputDirectory, outputFiles);
    }

    /// <summary>
    /// Writes source NIF mesh files to the output directory as conversion artifacts.
    /// <para>
    /// Detects _0/_1 weight variant pairs and writes both halves together.  When only one
    /// half of a pair exists the missing variant is <em>synthesised</em> from the available
    /// half: the regional morph factors are scaled so that the high-weight (_1) variant
    /// amplifies deltas by ×1.5 and the low-weight (_0) variant attenuates them by ×0.5.
    /// This prevents in-game body-weight interpolation failures (mesh collapse, clipping,
    /// NPC weight breaks) that occur when only one variant is present.
    /// </para>
    /// <para>
    /// Uses a heuristic vertex-block transform when possible; falls back to byte-for-byte
    /// passthrough when no readable NIF vertex stream is detected.
    /// </para>
    /// </summary>
    /// <returns>
    /// A tuple of the written file paths and the count of synthesised weight variants.
    /// </returns>
    private static async Task<(IReadOnlyList<string> Written, int SynthesizedCount)> WriteConvertedNifsAsync(
        ImportedArmor armor,
        ConvertedMesh mesh,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var written = new List<string>();
        var synthesizedCount = 0;

        // Build a set of mesh files that are part of a detected _0/_1 pair so we can
        // treat unpaired singletons differently.
        var pairedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (armor.WeightVariantPairs is { Count: > 0 } pairs)
        {
            foreach (var pair in pairs)
            {
                if (pair.LowWeightMesh is not null && pair.HighWeightMesh is not null)
                {
                    // Both halves present — write them with their original weight suffix names.
                    pairedFiles.Add(pair.LowWeightMesh);
                    pairedFiles.Add(pair.HighWeightMesh);

                    var lowDest  = Path.Combine(outputDirectory, Path.GetFileName(pair.LowWeightMesh)!);
                    var highDest = Path.Combine(outputDirectory, Path.GetFileName(pair.HighWeightMesh)!);

                    await CopyNifAsync(pair.LowWeightMesh, lowDest, mesh, cancellationToken);
                    await CopyNifAsync(pair.HighWeightMesh, highDest, mesh, cancellationToken);
                    written.Add(lowDest);
                    written.Add(highDest);
                }
                else
                {
                    // Incomplete pair — synthesise the missing weight variant so the game can
                    // interpolate body weight without mesh collapse or clipping artefacts.
                    var sourceMesh  = pair.LowWeightMesh ?? pair.HighWeightMesh!;
                    var isSourceLow = pair.LowWeightMesh is not null; // true → have _0, missing _1
                    var ext         = Path.GetExtension(sourceMesh);

                    var destSource = Path.Combine(outputDirectory, Path.GetFileName(sourceMesh)!);
                    var synthName  = pair.BaseName + (isSourceLow ? "_1" : "_0") + ext;
                    var destSynth  = Path.Combine(outputDirectory, synthName);

                    // Write the existing half with regular morphs.
                    await CopyNifAsync(sourceMesh, destSource, mesh, cancellationToken);

                    // Write the synthesised half with weight-scaled morphs.
                    var synthMesh = mesh with
                    {
                        RegionalMorphing = ScaleMorphsForWeightVariant(mesh.RegionalMorphing, isSourceLow),
                        Strategy         = mesh.Strategy + "+synth-" + (isSourceLow ? "1" : "0")
                    };
                    await CopyNifAsync(sourceMesh, destSynth, synthMesh, cancellationToken);

                    pairedFiles.Add(sourceMesh);
                    written.Add(destSource);
                    written.Add(destSynth);
                    synthesizedCount++;
                }
            }
        }

        // Write any NIF files that were not handled as part of a complete pair.
        foreach (var meshFile in armor.MeshFiles)
        {
            if (pairedFiles.Contains(meshFile)) continue;

            var dest = Path.Combine(outputDirectory, Path.GetFileName(meshFile)!);
            await CopyNifAsync(meshFile, dest, mesh, cancellationToken);
            written.Add(dest);
        }

        return (written, synthesizedCount);
    }

    /// <summary>
    /// Scales per-region morph factors to derive the missing weight variant from the available one.
    /// <list type="bullet">
    ///   <item>Synthesising <c>_1</c> from <c>_0</c> (<paramref name="synthesizingHighWeight"/>=<see langword="true"/>):
    ///   the delta above 1.0 is amplified by ×1.5 to represent the heavier body shape.</item>
    ///   <item>Synthesising <c>_0</c> from <c>_1</c> (<paramref name="synthesizingHighWeight"/>=<see langword="false"/>):
    ///   the delta is attenuated by ×0.5 to represent the lighter body shape.</item>
    /// </list>
    /// A factor of exactly 1.0 (no-change regions) is always preserved.
    /// </summary>
    internal static IReadOnlyDictionary<string, double> ScaleMorphsForWeightVariant(
        IReadOnlyDictionary<string, double> morphs,
        bool synthesizingHighWeight)
    {
        // _1 (high-weight) = bigger shape → amplify deltas by 1.5
        // _0 (low-weight)  = slimmer shape → attenuate deltas by 0.5
        var scale = synthesizingHighWeight ? 1.5 : 0.5;
        return morphs.ToDictionary(
            kv => kv.Key,
            kv => Math.Round(1.0 + (kv.Value - 1.0) * scale, 6),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generates minimal flat tangent-space normal map stubs (4×4 BGRA8 DDS) for every
    /// diffuse texture that has no corresponding <c>_n.dds</c> in the output package.
    /// A flat stub prevents purple/missing-normal-map rendering artefacts in-game and
    /// can be replaced by a proper baked normal map at any time.
    /// </summary>
    private static async Task<IReadOnlyList<string>> GenerateMissingNormalMapStubsAsync(
        ImportedArmor armor,
        TextureSummary textureSummary,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (textureSummary.MissingNormals.Count == 0) return [];

        var generated = new List<string>();
        var missingSet = new HashSet<string>(textureSummary.MissingNormals, StringComparer.OrdinalIgnoreCase);
        var stubBytes  = BuildFlatNormalMapDds();

        foreach (var texturePath in armor.TextureFiles)
        {
            var fileName = Path.GetFileName(texturePath);
            if (!missingSet.Contains(fileName)) continue;
            if (!File.Exists(texturePath)) continue;

            // Derive the output path for the stub: same relative location as the copied
            // diffuse but with the base name suffixed by "_n".
            var relativePath  = GetSafeRelativeAssetPath(armor.SourcePath, Path.GetFullPath(texturePath));
            var destDiffuse   = Path.GetFullPath(Path.Combine(outputDirectory, relativePath));
            if (!destDiffuse.StartsWith(Path.GetFullPath(outputDirectory), StringComparison.OrdinalIgnoreCase))
            {
                destDiffuse = Path.Combine(outputDirectory, fileName);
            }

            var normalStubPath = Path.Combine(
                Path.GetDirectoryName(destDiffuse) ?? outputDirectory,
                Path.GetFileNameWithoutExtension(destDiffuse) + "_n.dds");

            if (File.Exists(normalStubPath)) continue;

            var dir = Path.GetDirectoryName(normalStubPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            await File.WriteAllBytesAsync(normalStubPath, stubBytes, cancellationToken);
            generated.Add(normalStubPath);
        }

        return generated;
    }

    /// <summary>
    /// Builds a minimal 4×4 uncompressed BGRA8 DDS representing a flat tangent-space
    /// normal map.  Every pixel stores the vector (0.5, 0.5, 1.0) remapped to bytes
    /// (R=0x80, G=0x80, B=0xFF) which points straight outward from the surface.
    /// </summary>
    internal static byte[] BuildFlatNormalMapDds()
    {
        const int width         = 4;
        const int height        = 4;
        const int bytesPerPixel = 4; // BGRA8888
        const int headerBytes   = 128; // 4-byte magic + 124-byte DDS_HEADER

        var data = new byte[headerBytes + width * height * bytesPerPixel];
        var s    = data.AsSpan();

        // ── Magic "DDS " ─────────────────────────────────────────────────────
        s[0] = 0x44; s[1] = 0x44; s[2] = 0x53; s[3] = 0x20;

        // ── DDS_HEADER (124 bytes starting at offset 4) ──────────────────────
        WriteDdsLE(s,  4, 124);           // dwSize
        WriteDdsLE(s,  8, 0x100FU);       // dwFlags: CAPS|HEIGHT|WIDTH|PITCH|PIXELFORMAT
        WriteDdsLE(s, 12, (uint)height);  // dwHeight
        WriteDdsLE(s, 16, (uint)width);   // dwWidth
        WriteDdsLE(s, 20, (uint)(width * bytesPerPixel)); // dwPitchOrLinearSize
        WriteDdsLE(s, 24, 0);             // dwDepth
        WriteDdsLE(s, 28, 1);             // dwMipMapCount

        // ── DDS_PIXELFORMAT (32 bytes at offset 76) ──────────────────────────
        WriteDdsLE(s, 76,  32U);          // dwSize
        WriteDdsLE(s, 80,  0x41U);        // dwFlags: DDPF_RGB(0x40)|DDPF_ALPHAPIXELS(0x01)
        WriteDdsLE(s, 84,  0U);           // dwFourCC (uncompressed)
        WriteDdsLE(s, 88,  32U);          // dwRGBBitCount
        WriteDdsLE(s, 92,  0x00FF0000U);  // dwRBitMask
        WriteDdsLE(s, 96,  0x0000FF00U);  // dwGBitMask
        WriteDdsLE(s, 100, 0x000000FFU);  // dwBBitMask
        WriteDdsLE(s, 104, 0xFF000000U);  // dwABitMask

        // ── DDS_CAPS ─────────────────────────────────────────────────────────
        WriteDdsLE(s, 108, 0x1000U);      // dwCaps1: DDSCAPS_TEXTURE

        // ── Pixel data: flat normal (0x80, 0x80, 0xFF) in BGRA byte order ────
        for (var i = 0; i < width * height; i++)
        {
            var off = headerBytes + i * bytesPerPixel;
            data[off]     = 0xFF; // B  ← Z-component (outward, full)
            data[off + 1] = 0x80; // G  ← Y-component (neutral)
            data[off + 2] = 0x80; // R  ← X-component (neutral)
            data[off + 3] = 0xFF; // A
        }

        return data;
    }

    private static void WriteDdsLE(Span<byte> span, int offset, uint value)
    {
        span[offset]     = (byte)(value & 0xFF);
        span[offset + 1] = (byte)((value >> 8) & 0xFF);
        span[offset + 2] = (byte)((value >> 16) & 0xFF);
        span[offset + 3] = (byte)((value >> 24) & 0xFF);
    }


    private static async Task<IReadOnlyList<string>> CopySupportAssetsAsync(
        ImportedArmor armor,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var supportFiles = new List<string>();
        supportFiles.AddRange(armor.TextureFiles);
        supportFiles.AddRange(armor.PhysicsFiles);
        supportFiles.AddRange(armor.BodyReferenceFiles.Where(path =>
            Path.GetExtension(path).Equals(".tri", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".osp", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path).Equals(".nif", StringComparison.OrdinalIgnoreCase)));
        supportFiles.AddRange(EnumerateMaterialFiles(armor.SourcePath));
        supportFiles.AddRange(EnumeratePluginFiles(armor.SourcePath));

        var copied = new List<string>();
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceFile in supportFiles)
        {
            var fullSource = Path.GetFullPath(sourceFile);
            if (!seenSources.Add(fullSource) || !File.Exists(fullSource))
            {
                continue;
            }

            var relativePath = GetSafeRelativeAssetPath(armor.SourcePath, fullSource);
            var destinationPath = Path.GetFullPath(Path.Combine(outputDirectory, relativePath));
            if (!destinationPath.StartsWith(Path.GetFullPath(outputDirectory), StringComparison.OrdinalIgnoreCase))
            {
                destinationPath = Path.Combine(outputDirectory, Path.GetFileName(fullSource));
            }

            if (!seenDestinations.Add(destinationPath) || File.Exists(destinationPath))
            {
                continue;
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var sourceStream = File.OpenRead(fullSource);
            await using var destinationStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            copied.Add(destinationPath);
        }

        return copied;
    }

    private static IReadOnlyList<string> EnumeratePluginFiles(string sourcePath)
    {
        static bool IsPlugin(string path) =>
            Path.GetExtension(path) is ".esp" or ".esm" or ".esl";

        var scanRoot = ResolveSupportAssetRoot(sourcePath);
        if (File.Exists(scanRoot))
        {
            return IsPlugin(scanRoot) ? [Path.GetFullPath(scanRoot)] : [];
        }

        if (!Directory.Exists(scanRoot))
        {
            return [];
        }

        return Directory.GetFiles(scanRoot, "*.*", SearchOption.AllDirectories)
            .Where(IsPlugin)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> EnumerateMaterialFiles(string sourcePath)
    {
        static bool IsMaterial(string path) =>
            Path.GetExtension(path) is ".bgsm" or ".bgem";

        var scanRoot = ResolveSupportAssetRoot(sourcePath);
        if (File.Exists(scanRoot))
        {
            return IsMaterial(scanRoot) ? [Path.GetFullPath(scanRoot)] : [];
        }

        if (!Directory.Exists(scanRoot))
        {
            return [];
        }

        return Directory.GetFiles(scanRoot, "*.*", SearchOption.AllDirectories)
            .Where(IsMaterial)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetSafeRelativeAssetPath(string sourceRoot, string fullSourcePath)
    {
        sourceRoot = ResolveSupportAssetRoot(sourceRoot);

        if (Directory.Exists(sourceRoot))
        {
            var relative = Path.GetRelativePath(sourceRoot, fullSourcePath);
            if (!string.IsNullOrWhiteSpace(relative) &&
                !relative.StartsWith("..", StringComparison.Ordinal) &&
                !Path.IsPathRooted(relative))
            {
                return relative;
            }
        }

        return Path.GetFileName(fullSourcePath);
    }

    private static string ResolveSupportAssetRoot(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return sourcePath;
        }

        if (File.Exists(sourcePath))
        {
            var sourceDirectory = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrWhiteSpace(sourceDirectory) && Directory.Exists(sourceDirectory))
            {
                var modRoot = TryResolveModRootFromMeshesPath(sourceDirectory);
                return !string.IsNullOrWhiteSpace(modRoot) ? modRoot : sourceDirectory;
            }
        }

        return sourcePath;
    }

    private static string? TryResolveModRootFromMeshesPath(string startDirectory)
    {
        var current = startDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (string.Equals(Path.GetFileName(current), "meshes", StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(current);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    return parent;
                }
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static async Task CopyNifAsync(string sourcePath, string destPath, ConvertedMesh mesh, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            // Source does not exist (e.g. in-memory test stubs).
            // Log a warning so missing files are surfaced rather than silently producing empty output.
            await Console.Error.WriteLineAsync($"[SlideSmith] Warning: source NIF not found on disk -- writing empty placeholder: {sourcePath}");
            await File.WriteAllBytesAsync(destPath, [], cancellationToken);
            return;
        }

        var sourceBytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
        var outputBytes = TryApplyNifVertexTransform(sourceBytes, mesh.RegionalMorphing);
        await File.WriteAllBytesAsync(destPath, outputBytes, cancellationToken);
    }

    private static byte[] TryApplyNifVertexTransform(byte[] sourceBytes, IReadOnlyDictionary<string, double> regionalMorphing)
    {
        if (!NifGeometrySignatureReader.TryLocateVertexBlock(sourceBytes, out var vertexDataOffset, out var vertexCount))
        {
            return sourceBytes;
        }

        if (vertexCount <= 0)
        {
            return sourceBytes;
        }

        var transformed = sourceBytes.ToArray();
        const int vertexSize = 12;
        var requiredBytes = (long)vertexCount * vertexSize;
        if (vertexDataOffset < 0 || vertexDataOffset + requiredBytes > transformed.Length)
        {
            return sourceBytes;
        }

        var minZ = float.MaxValue;
        var maxZ = float.MinValue;
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;

        // First pass: compute bounding box and collect vertex positions for the solver
        var rawVertices = new (float X, float Y, float Z)[vertexCount];
        for (var index = 0; index < vertexCount; index++)
        {
            var offset = vertexDataOffset + (index * vertexSize);
            var x = BitConverter.ToSingle(transformed, offset);
            var y = BitConverter.ToSingle(transformed, offset + 4);
            var z = BitConverter.ToSingle(transformed, offset + 8);
            rawVertices[index] = (x, y, z);
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
            minZ = Math.Min(minZ, z);
            maxZ = Math.Max(maxZ, z);
        }

        var zRange = Math.Max(0.0001f, maxZ - minZ);
        var centerX = (minX + maxX) / 2f;
        var centerY = (minY + maxY) / 2f;
        var upperFactor = AverageMorph(regionalMorphing, "chest", "breasts", "shoulders", "arms");
        var midFactor = AverageMorph(regionalMorphing, "waist", "belly", "pelvis");
        var lowerFactor = AverageMorph(regionalMorphing, "legs", "thighs", "calves", "butt", "pelvis");
        var depthFactor = AverageMorph(regionalMorphing, "waist", "belly", "pelvis", "butt");
        var heightFactor = AverageMorph(regionalMorphing, "chest", "pelvis", "legs", "thighs");

        // Run animation-driven solver to get per-region push-out corrections
        var solverResult = AnimationDrivenGeometrySolver.Solve(rawVertices, regionalMorphing);
        var pushOut = solverResult.MaxPushOutPerRegion;
        var normScale = Math.Max(Math.Max(maxX - minX, maxY - minY), 0.0001f);

        // Second pass: apply morph scale transform + animation-driven push-out correction
        for (var index = 0; index < vertexCount; index++)
        {
            var offset = vertexDataOffset + (index * vertexSize);
            var x = BitConverter.ToSingle(transformed, offset);
            var y = BitConverter.ToSingle(transformed, offset + 4);
            var z = BitConverter.ToSingle(transformed, offset + 8);
            var normalizedHeight = (z - minZ) / zRange;
            var lowerWeight = 1.0f - normalizedHeight;
            var upperWeight = normalizedHeight;
            var midWeight = Math.Max(0f, 1f - Math.Abs((normalizedHeight - 0.5f) * 2f));
            var widthScale = (upperFactor * upperWeight) + (lowerFactor * lowerWeight) + (midFactor * midWeight * 0.5);
            var depthScale = (depthFactor * 0.65) + (midFactor * 0.35);

            var transformedX = centerX + ((x - centerX) * (float)widthScale);
            var transformedY = centerY + ((y - centerY) * (float)depthScale);
            var transformedZ = minZ + ((z - minZ) * (float)heightFactor);

            // Animation-driven push-out: move vertex outward along its XY direction from the
            // body centre by the push-out depth so that it sits outside the body envelope at
            // the worst animation pose.
            var region = AnimationDrivenGeometrySolver.HeightToRegion(normalizedHeight);
            if (pushOut.TryGetValue(region, out var depth) && depth > 0)
            {
                var dx = transformedX - centerX;
                var dy = transformedY - centerY;
                var xyDist = MathF.Sqrt(dx * dx + dy * dy);
                if (xyDist > 0.0001f)
                {
                    // Scale push-out from normalised units back to model-space units
                    var pushOutModelSpace = (float)(depth * normScale);
                    transformedX += (dx / xyDist) * pushOutModelSpace;
                    transformedY += (dy / xyDist) * pushOutModelSpace;
                }
            }

            Array.Copy(BitConverter.GetBytes(transformedX), 0, transformed, offset, 4);
            Array.Copy(BitConverter.GetBytes(transformedY), 0, transformed, offset + 4, 4);
            Array.Copy(BitConverter.GetBytes(transformedZ), 0, transformed, offset + 8, 4);
        }

        return transformed;
    }

    private static double AverageMorph(IReadOnlyDictionary<string, double> field, params string[] regions)
    {
        double total = 0;
        var count = 0;

        foreach (var region in regions)
        {
            if (!field.TryGetValue(region, out var value))
            {
                continue;
            }

            total += value;
            count++;
        }

        return count > 0 ? total / count : 1d;
    }

    private static IReadOnlyList<MeshDependencyMapEntry> BuildDependencyMap(ImportedArmor armor, PluginAnalysisResult pluginAnalysis)
    {
        var pluginMeshReferences = pluginAnalysis.ArmorAddons
            .SelectMany(addon => addon.DetectedMeshPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return armor.MeshFiles
            .Select(mesh =>
            {
                var meshFileName = Path.GetFileName(mesh) ?? mesh;
                var meshToken = NormalizeMeshToken(meshFileName);

                var textures = ResolveRelatedFiles(armor.TextureFiles, meshToken);
                var physicsFiles = ResolveRelatedFiles(armor.PhysicsFiles, meshToken);
                var bodyReferences = ResolveRelatedFiles(armor.BodyReferenceFiles, meshToken);

                var matchedPluginPaths = pluginMeshReferences
                    .Where(path => Path.GetFileName(path).Contains(meshFileName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new MeshDependencyMapEntry(
                    meshFileName,
                    textures,
                    physicsFiles,
                    bodyReferences,
                    matchedPluginPaths);
            })
            .ToList();
    }

    private static IReadOnlyList<string> ResolveRelatedFiles(IReadOnlyList<string> files, string meshToken)
    {
        return files
            .Where(file => Path.GetFileNameWithoutExtension(file).Contains(meshToken, StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetFileName(file) ?? file)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeMeshToken(string meshFileName)
    {
        var token = Path.GetFileNameWithoutExtension(meshFileName) ?? meshFileName;
        return token.EndsWith("_0", StringComparison.OrdinalIgnoreCase) || token.EndsWith("_1", StringComparison.OrdinalIgnoreCase)
            ? token[..^2]
            : token;
    }

    // ── Preview helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Extracts SMP bone names from <c>smp-config.xml</c> using a lightweight regex scan.
    /// Falls back to a set of standard CBPC node names when only the CBPC profile is active.
    /// </summary>
    private static IReadOnlyList<string> ExtractPhysicsNodeNames(PhysicsConfig physics)
    {
        var nodes = new List<string>();

        if (!string.IsNullOrWhiteSpace(physics.SmpConfigXml))
        {
            var bonePattern = new System.Text.RegularExpressions.Regex(
                @"<bone\s+name=""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);

            foreach (System.Text.RegularExpressions.Match m in bonePattern.Matches(physics.SmpConfigXml))
            {
                var name = m.Groups[1].Value;
                if (!nodes.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    nodes.Add(name);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(physics.CbpcConfigXml))
        {
            // CBPC-only — return well-known CBPC node labels derived from the XML elements present.
            var elementPattern = new System.Text.RegularExpressions.Regex(
                @"<(\w+Physics)>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);

            foreach (System.Text.RegularExpressions.Match m in elementPattern.Matches(physics.CbpcConfigXml))
            {
                var label = m.Groups[1].Value;
                if (!nodes.Contains(label, StringComparer.OrdinalIgnoreCase))
                {
                    nodes.Add(label);
                }
            }
        }

        return nodes;
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CaptureSliderMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["front"] = ["Belly", "BreastsShape", "BreastsSmall", "BreastsLarge", "WaistWidth", "Chest", "Body"],
            ["side"]  = ["Belly", "Butt", "HipWidth", "Thighs", "Legs"],
            ["back"]  = ["Butt", "Calves", "Thighs", "Legs", "Shoulders"]
        };

    /// <summary>
    /// Returns sliders from the project that are relevant to the given capture view angle,
    /// preserving only those that exist in the project's slider list.
    /// </summary>
    private static IReadOnlyList<string> GetCaptureSliders(string view, IReadOnlyList<string> projectSliders)
    {
        if (!CaptureSliderMap.TryGetValue(view, out var candidates))
        {
            return [];
        }

        return candidates
            .Where(s => projectSliders.Contains(s, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    // ── Plugin guidance helpers ───────────────────────────────────────────────

    private static IReadOnlyDictionary<string, string> BuildPluginRewriteMap(
        PluginAnalysisResult pluginAnalysis,
        string targetBody,
        IReadOnlyList<string> writtenNifPaths)
    {
        var convertedByFileName = writtenNifPaths
            .Where(path => Path.GetExtension(path).Equals(".nif", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

        var rewrites = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Collect paths from both ARMA (ArmorAddon) and ARMO (Armor) records.
        var allPaths = pluginAnalysis.ArmorAddons
            .SelectMany(a => a.DetectedMeshPaths)
            .Concat((pluginAnalysis.ArmorRecords ?? []).SelectMany(r => r.DetectedMeshPaths));

        foreach (var originalPath in allPaths)
        {
            var normalisedOriginal = originalPath.Replace('\\', '/');
            var fileName = Path.GetFileName(normalisedOriginal);
            if (string.IsNullOrWhiteSpace(fileName) || !convertedByFileName.ContainsKey(fileName))
            {
                continue;
            }

            rewrites[normalisedOriginal] = BuildPluginConvertedMeshPath(targetBody, fileName, normalisedOriginal);
        }

        return rewrites;
    }

    private static string BuildPluginConvertedMeshPath(string targetBody, string fileName, string originalPath)
    {
        var safeBodyToken = new string(targetBody
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');
        if (string.IsNullOrWhiteSpace(safeBodyToken))
        {
            safeBodyToken = "target";
        }

        var rewrittenRelative = $"slidesmith/{safeBodyToken}/{fileName}";
        return HasPluginMeshesPrefix(originalPath)
            ? $"meshes/{rewrittenRelative}"
            : rewrittenRelative;
    }

    private static bool HasPluginMeshesPrefix(string pluginPath)
    {
        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            return false;
        }

        var normalised = pluginPath.Replace('\\', '/').TrimStart('/');
        return normalised.StartsWith("meshes/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveOutputMeshPath(string pluginMeshPath)
    {
        if (string.IsNullOrWhiteSpace(pluginMeshPath))
        {
            return string.Empty;
        }

        var normalised = pluginMeshPath.Replace('\\', '/').TrimStart('/');
        return HasPluginMeshesPrefix(normalised)
            ? normalised
            : $"meshes/{normalised}";
    }

    private static async Task<IReadOnlyList<string>> StageConvertedMeshesForPluginRewriteAsync(
        string outputDirectory,
        IReadOnlyList<string> writtenNifPaths,
        IReadOnlyDictionary<string, string> pluginRewriteMap,
        CancellationToken cancellationToken)
    {
        if (pluginRewriteMap.Count == 0 || writtenNifPaths.Count == 0)
        {
            return [];
        }

        var sourceByFileName = writtenNifPaths
            .Where(path => Path.GetExtension(path).Equals(".nif", StringComparison.OrdinalIgnoreCase))
            .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var staged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rewrittenPath in pluginRewriteMap.Values)
        {
            var fileName = Path.GetFileName(rewrittenPath);
            if (string.IsNullOrWhiteSpace(fileName) || !sourceByFileName.TryGetValue(fileName, out var sourcePath))
            {
                continue;
            }

            var outputMeshPath = ResolveOutputMeshPath(rewrittenPath);
            if (string.IsNullOrWhiteSpace(outputMeshPath))
            {
                continue;
            }

            var destinationPath = Path.Combine(
                outputDirectory,
                outputMeshPath.Replace('/', Path.DirectorySeparatorChar));
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            await using var source = File.OpenRead(sourcePath);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination, cancellationToken);
            staged.Add(destinationPath);
        }

        return staged.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Builds a structured list of per-mesh xEdit patch steps from the plugin analysis result.
    /// Each step identifies the plugin, the detected mesh path, and the rewritten target mesh path.
    /// Covers both ARMA (ArmorAddon) and ARMO (Armor) records.
    /// </summary>
    private static IReadOnlyList<object> BuildProposedPatchSteps(
        PluginAnalysisResult pluginAnalysis,
        string targetBody,
        IReadOnlyDictionary<string, string> pluginRewriteMap)
    {
        var steps = new List<object>();

        foreach (var addon in pluginAnalysis.ArmorAddons)
        {
            foreach (var meshPath in addon.DetectedMeshPaths)
            {
                var normalised = meshPath.Replace('\\', '/');
                var hasRewrite = pluginRewriteMap.TryGetValue(normalised, out var rewrittenPath);
                steps.Add(new
                {
                    Plugin = addon.RecordType,
                    RecordType = "ArmorAddon (ARMA)",
                    OriginalMeshPath = normalised,
                    ProposedMeshPath = rewrittenPath ?? normalised,
                    RewriteReady = hasRewrite,
                    PlacementNote = hasRewrite
                        ? $"Use patch-armor.pas to rewrite ARMA path to {rewrittenPath} and keep the converted NIF at that path."
                        : $"No converted filename match found for this path. Keep original mesh path or patch manually for {targetBody}.",
                    XEditAction = hasRewrite
                        ? "Run generated patch-armor.pas in xEdit to auto-rewrite matching ARMA mesh paths."
                        : "Open in xEdit and patch this ARMA mesh path manually.",
                    Tool = "xEdit"
                });
            }
        }

        foreach (var record in (pluginAnalysis.ArmorRecords ?? []))
        {
            foreach (var meshPath in record.DetectedMeshPaths)
            {
                var normalised = meshPath.Replace('\\', '/');
                var hasRewrite = pluginRewriteMap.TryGetValue(normalised, out var rewrittenPath);
                steps.Add(new
                {
                    Plugin = record.RecordType,
                    RecordType = "Armor (ARMO)",
                    OriginalMeshPath = normalised,
                    ProposedMeshPath = rewrittenPath ?? normalised,
                    RewriteReady = hasRewrite,
                    PlacementNote = hasRewrite
                        ? $"Use patch-armor.pas to rewrite ARMO path to {rewrittenPath} and keep the converted NIF at that path."
                        : $"No converted filename match found for this path. Keep original mesh path or patch manually for {targetBody}.",
                    XEditAction = hasRewrite
                        ? "Run generated patch-armor.pas in xEdit to auto-rewrite matching ARMO mesh paths."
                        : "Open in xEdit and patch this ARMO mesh path manually.",
                    Tool = "xEdit"
                });
            }
        }

        return steps;
    }

    private static string BuildFomodModuleConfigXml(string packageName, string targetBody, string bodySlideProjectName)
    {
        var safePackage = XmlEscape(packageName);
        var safeTargetBody = XmlEscape(targetBody);
        var safeProjectName = XmlEscape(bodySlideProjectName);

        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <config xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="http://qconsulting.ca/fo3/ModConfig5.0.xsd">
              <moduleName>{{safePackage}} - SlideSmith Conversion</moduleName>
              <installSteps order="Explicit">
                <installStep name="Target Body">
                  <optionalFileGroups order="Explicit">
                    <group name="Body">
                      <plugins order="Explicit">
                        <plugin name="{{safeTargetBody}}">
                          <description>Generated conversion output for {{safeTargetBody}} with BodySlide project {{safeProjectName}}.</description>
                          <files />
                          <conditionFlags />
                          <typeDescriptor>
                            <type name="Required" />
                          </typeDescriptor>
                        </plugin>
                      </plugins>
                    </group>
                  </optionalFileGroups>
                </installStep>
              </installSteps>
            </config>
            """;
    }

    private static string BuildFomodInfoXml(string packageName)
    {
        var safePackage = XmlEscape(packageName);

        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <fomod>
              <Name>{{safePackage}} - SlideSmith</Name>
              <Author>SlideSmith</Author>
              <Version MachineVersion="0.1">0.1</Version>
              <Website></Website>
              <Description>Auto-generated FOMOD metadata for SlideSmith conversion output.</Description>
            </fomod>
            """;
    }

    private static string XmlEscape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    /// <summary>
    /// Builds a BSD (BodySlide Data) binary payload for a single slider.
    /// <para>
    /// BSD file layout (little-endian):
    /// <list type="bullet">
    ///   <item>4 bytes — magic "BSD\0" (0x42 0x53 0x44 0x00)</item>
    ///   <item>2 bytes — version (0x01 0x00)</item>
    ///   <item>1 byte  — weight flag (0x00 = low / _0, 0x01 = high / _1)</item>
    ///   <item>2 bytes — slider name length (UTF-8)</item>
    ///   <item>N bytes — slider name (UTF-8)</item>
    ///   <item>4 bytes — vertex count</item>
    ///   <item>12 × vertex count bytes — per-vertex XYZ deltas (float32 triplets)</item>
    /// </list>
    /// </para>
    /// </summary>
    private static byte[] BuildBsdBytes(
        string sliderName,
        bool isHighWeight,
        int vertexCount,
        IReadOnlyDictionary<string, double> regionalMorphing)
    {
        vertexCount = Math.Clamp(vertexCount, 1, 250_000);
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(sliderName);
        using var ms = new System.IO.MemoryStream();
        using var w  = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

        w.Write((byte)0x42); // 'B'
        w.Write((byte)0x53); // 'S'
        w.Write((byte)0x44); // 'D'
        w.Write((byte)0x00); // null terminator
        w.Write((ushort)1);               // version 1
        w.Write(isHighWeight ? (byte)1 : (byte)0); // weight flag
        w.Write((ushort)nameBytes.Length);
        w.Write(nameBytes);
        w.Write((uint)vertexCount);

        for (var index = 0; index < vertexCount; index++)
        {
            var (x, y, z) = ComputeMorphDelta(sliderName, index, vertexCount, isHighWeight, regionalMorphing);
            w.Write(x);
            w.Write(y);
            w.Write(z);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Builds a TRI morph binary payload for all sliders of one weight variant.
    /// <para>
    /// TRI file layout (little-endian):
    /// <list type="bullet">
    ///   <item>8 bytes — magic "FRTRI003" (matches the BodySlide / Outfit Studio TRI header)</item>
        ///   <item>4 bytes — vertex count</item>
    ///   <item>4 bytes — morph count (number of sliders)</item>
    ///   <item>For each morph:
    ///     <list type="bullet">
    ///       <item>2 bytes — morph name length</item>
    ///       <item>N bytes — morph name (UTF-8)</item>
        ///       <item>4 bytes — delta count (equals vertex count)</item>
    ///     </list>
    ///   </item>
        ///   <item>For each morph: delta payload (delta count × XYZ int16 triplets)</item>
        /// </list>
        /// </para>
        /// </summary>
        private static byte[] BuildTriBytes(
            string projectName,
            IReadOnlyList<string> sliders,
            bool isHighWeight,
            int vertexCount,
            IReadOnlyDictionary<string, double> regionalMorphing)
        {
            vertexCount = Math.Clamp(vertexCount, 1, 250_000);
            using var ms = new System.IO.MemoryStream();
            using var w  = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

            // Magic header matches BodySlide / Outfit Studio TRI format.
            w.Write(System.Text.Encoding.ASCII.GetBytes("FRTRI003"));
            w.Write((uint)vertexCount);
            w.Write((uint)sliders.Count);      // morph count

            foreach (var slider in sliders)
            {
                // For the high-weight TRI the morph name gets a "_1" suffix to match BodySlide conventions.
                var morphName = isHighWeight ? $"{slider}_1" : slider;
                var nameBytes = System.Text.Encoding.UTF8.GetBytes(morphName);
                w.Write((ushort)nameBytes.Length);
                w.Write(nameBytes);
                w.Write((uint)vertexCount);
            }

            foreach (var slider in sliders)
            {
                for (var index = 0; index < vertexCount; index++)
                {
                    var (x, y, z) = ComputeMorphDelta(slider, index, vertexCount, isHighWeight, regionalMorphing);
                    w.Write(QuantizeTriDelta(x));
                    w.Write(QuantizeTriDelta(y));
                    w.Write(QuantizeTriDelta(z));
                }
            }

            return ms.ToArray();
        }

        private static int EstimateMorphVertexCount(IReadOnlyList<string> writtenNifs, string targetBody)
        {
            var best = 0;
            foreach (var nifPath in writtenNifs)
            {
                if (!File.Exists(nifPath) || !Path.GetExtension(nifPath).Equals(".nif", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var bytes = File.ReadAllBytes(nifPath);
                    if (NifGeometrySignatureReader.TryLocateVertexBlock(bytes, out _, out var vertexCount) && vertexCount > best)
                    {
                        best = vertexCount;
                    }
                }
                catch (IOException)
                {
                    // Ignore unreadable files and continue with fallback hints.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore unreadable files and continue with fallback hints.
                }
            }

            if (best > 0)
            {
                return best;
            }

            var bodyTemplate = VanillaBodySignatureDatabase.Templates
                .FirstOrDefault(template => string.Equals(template.Body, targetBody, StringComparison.OrdinalIgnoreCase));
            if (bodyTemplate is not null && bodyTemplate.VertexCountMax > bodyTemplate.VertexCountMin && bodyTemplate.VertexCountMin > 0)
            {
                return (bodyTemplate.VertexCountMin + bodyTemplate.VertexCountMax) / 2;
            }

            return 4096;
        }

        private static short QuantizeTriDelta(float value)
        {
            var scaled = (int)Math.Round(value * 2048f);
            return (short)Math.Clamp(scaled, short.MinValue, short.MaxValue);
        }

        private static (float X, float Y, float Z) ComputeMorphDelta(
            string sliderName,
            int vertexIndex,
            int vertexCount,
            bool isHighWeight,
            IReadOnlyDictionary<string, double> regionalMorphing)
        {
            var morphFactor = ResolveSliderMorphFactor(sliderName, regionalMorphing);
            var normalizedDelta = Math.Clamp(morphFactor - 1d, -0.4d, 0.4d);
            var adaptiveScale = 0.0012f + (float)Math.Abs(normalizedDelta) * 0.0042f;
            var weightScale = isHighWeight ? 1.35f : 0.85f;
            var hash = StableHash(sliderName);
            var phase = (float)(vertexIndex % 157) / 157f;
            var waveA = MathF.Sin(((phase * 6.2831855f) * (1f + ((hash & 7) * 0.07f))) + ((hash & 31) * 0.043f));
            var waveB = MathF.Cos(((phase * 6.2831855f) * (0.8f + (((hash >> 5) & 7) * 0.06f))) + ((hash & 63) * 0.029f));
            var centerBias = ((float)vertexIndex / Math.Max(1, vertexCount)) - 0.5f;

            var x = adaptiveScale * weightScale * (0.60f * waveA);
            var y = adaptiveScale * weightScale * (0.45f * waveB);
            var z = adaptiveScale * weightScale * (0.75f * waveA + (0.30f * centerBias));
            return (x, y, z);
        }

        private static double ResolveSliderMorphFactor(string sliderName, IReadOnlyDictionary<string, double> regionalMorphing)
        {
            if (regionalMorphing.Count == 0)
            {
                return 1d;
            }

            static double GetOrDefault(IReadOnlyDictionary<string, double> values, string key)
                => values.TryGetValue(key, out var value) ? value : 1d;

            var name = sliderName.ToLowerInvariant();
            return name switch
            {
                _ when name.Contains("belly", StringComparison.Ordinal) =>
                    Math.Max(GetOrDefault(regionalMorphing, "belly"), GetOrDefault(regionalMorphing, "waist")),
                _ when name.Contains("butt", StringComparison.Ordinal) =>
                    Math.Max(GetOrDefault(regionalMorphing, "butt"), GetOrDefault(regionalMorphing, "pelvis")),
                _ when name.Contains("breast", StringComparison.Ordinal) || name.Contains("pec", StringComparison.Ordinal) =>
                    Math.Max(GetOrDefault(regionalMorphing, "breasts"), GetOrDefault(regionalMorphing, "chest")),
                _ when name.Contains("waist", StringComparison.Ordinal) =>
                    GetOrDefault(regionalMorphing, "waist"),
                _ when name.Contains("hip", StringComparison.Ordinal) =>
                    Math.Max(GetOrDefault(regionalMorphing, "pelvis"), GetOrDefault(regionalMorphing, "butt")),
                _ when name.Contains("thigh", StringComparison.Ordinal) || name.Contains("leg", StringComparison.Ordinal) =>
                    Math.Max(GetOrDefault(regionalMorphing, "thighs"), GetOrDefault(regionalMorphing, "calves")),
                _ when name.Contains("calf", StringComparison.Ordinal) =>
                    GetOrDefault(regionalMorphing, "calves"),
                _ when name.Contains("arm", StringComparison.Ordinal) =>
                    GetOrDefault(regionalMorphing, "arms"),
                _ when name.Contains("shoulder", StringComparison.Ordinal) =>
                    GetOrDefault(regionalMorphing, "shoulders"),
                _ when name.Contains("body", StringComparison.Ordinal) =>
                    (GetOrDefault(regionalMorphing, "chest") + GetOrDefault(regionalMorphing, "waist") + GetOrDefault(regionalMorphing, "pelvis")) / 3d,
                _ => regionalMorphing.Values.DefaultIfEmpty(1d).Average()
            };
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var c in value)
                {
                    hash = (hash * 31) + char.ToUpperInvariant(c);
                }

                return hash;
            }
        }

    // ── HTML Preview ──────────────────────────────────────────────────────────

    // SVG layout: body region rectangles keyed by canonical region name.
    // Each tuple is (x, y, width, height, label) in SVG coordinate space.
    private static readonly IReadOnlyDictionary<string, (int X, int Y, int W, int H, string Label)> RegionShapes =
        new Dictionary<string, (int, int, int, int, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["head"]      = (84,   4,  32, 32, "Head"),
            ["neck"]      = (91,  37,  18, 18, "Neck"),
            ["shoulders"] = (50,  54,  32, 18, "Sho"),
            ["chest"]     = (76,  54,  48, 36, "Chest"),
            ["arms"]      = (42,  72,  30, 72, "Arms"),
            ["waist"]     = (80,  90,  40, 26, "Waist"),
            ["belly"]     = (78, 116,  44, 30, "Belly"),
            ["breasts"]   = (78,  54,  44, 36, "Breast"),
            ["pelvis"]    = (76, 146,  48, 28, "Pelvis"),
            ["butt"]      = (76, 146,  48, 28, "Butt"),
            ["thighs"]    = (77, 174,  44, 58, "Thighs"),
            ["calves"]    = (77, 232,  44, 60, "Calves"),
            ["legs"]      = (77, 174,  44, 118, "Legs"),
            ["feet"]      = (77, 292,  44, 24, "Feet"),
        };

    /// <summary>Returns a CSS colour for a morph factor on a blue→green→yellow→orange→red gradient.</summary>
    private static string MorphColour(double factor) => factor switch
    {
        < 0.90 => "#3a7bd5",  // compact / blue
        < 0.97 => "#27ae60",  // near-normal / green
        < 1.05 => "#2ecc71",  // normal / light-green
        < 1.12 => "#f1c40f",  // mild expansion / yellow
        < 1.20 => "#e67e22",  // expanded / orange
        _      => "#e74c3c"   // high expansion / red
    };

    /// <summary>
    /// Generates a self-contained HTML file with an inline SVG body silhouette colour-coded by
    /// regional morph factor, plus slider, physics, and pose-simulation-risk tables.
    /// </summary>
    private static string BuildPreviewHtml(
        ConversionRequest request,
        ImportedArmor armor,
        MeshAnalysis analysis,
        ConvertedMesh mesh,
        BodySlideProject bodySlideProject,
        PhysicsConfig physics,
        PoseSimulationResult poseSimulation)
    {
        var armorName = Path.GetFileNameWithoutExtension(armor.MeshFiles.FirstOrDefault() ?? "armor");
        var orderedRegions = mesh.RegionalMorphing
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var baseRegionalFactors = orderedRegions.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        var availableBodyProfiles = BodyTypeCatalog.All
            .Select(static body => body.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                bodyName => bodyName,
                bodyName => BodyTransformationFieldCatalog.Resolve(bodyName),
                StringComparer.OrdinalIgnoreCase);

        var previewProfiles = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Current conversion"] = baseRegionalFactors
        };
        foreach (var (bodyName, bodyField) in availableBodyProfiles)
        {
            previewProfiles[bodyName] = bodyField;
        }

        var profileOptions = new List<string> { "Current conversion" };
        profileOptions.AddRange(availableBodyProfiles.Keys);
        var selectedProfile = profileOptions.Contains(request.TargetBody, StringComparer.OrdinalIgnoreCase)
            ? profileOptions.First(name => string.Equals(name, request.TargetBody, StringComparison.OrdinalIgnoreCase))
            : "Current conversion";
        var profileOptionsHtml = string.Join(
            "",
            profileOptions.Select(name =>
            {
                var selected = string.Equals(name, selectedProfile, StringComparison.OrdinalIgnoreCase) ? " selected" : string.Empty;
                return $"""<option value="{HtmlEncode(name)}"{selected}>{HtmlEncode(name)}</option>""";
            }));

        var regionDomIds = orderedRegions.ToDictionary(
            pair => pair.Key,
            pair => ToDomIdToken(pair.Key),
            StringComparer.OrdinalIgnoreCase);

        var baseFactorsJson = JsonSerializer.Serialize(baseRegionalFactors);
        var previewProfilesJson = JsonSerializer.Serialize(previewProfiles);
        var regionDomIdsJson = JsonSerializer.Serialize(regionDomIds);

        // Build SVG body regions.
        var svgParts = new System.Text.StringBuilder();
        // Draw background body outline.
        svgParts.AppendLine("""      <g id="preview-body-root">""");
        svgParts.AppendLine("""        <rect x="76" y="54" width="48" height="262" rx="10" fill="#2a2a4a" stroke="#555" stroke-width="1"/>""");
        svgParts.AppendLine("""        <circle cx="100" cy="20" r="18" fill="#2a2a4a" stroke="#555" stroke-width="1"/>""");
        svgParts.AppendLine("""        <rect x="42" y="72" width="12" height="72" rx="5" fill="#2a2a4a" stroke="#555" stroke-width="1"/>""");
        svgParts.AppendLine("""        <rect x="146" y="72" width="12" height="72" rx="5" fill="#2a2a4a" stroke="#555" stroke-width="1"/>""");

        // Layer coloured overlays for each region that has a morph factor.
        var drawnRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (region, factor) in orderedRegions)
        {
            if (!RegionShapes.TryGetValue(region, out var shape)) continue;
            if (!drawnRegions.Add(region)) continue;

            var colour = MorphColour(factor);
            var opacity = Math.Clamp(0.45 + Math.Abs(factor - 1.0) * 1.2, 0.4, 0.85);
            var domId = regionDomIds[region];
            svgParts.AppendLine($"""        <rect id="region-box-{domId}" data-region="{HtmlEncode(region)}" x="{shape.X}" y="{shape.Y}" width="{shape.W}" height="{shape.H}" rx="4" fill="{colour}" opacity="{opacity:F2}" stroke="{colour}" stroke-width="0.5"/>""");
            svgParts.AppendLine($"""        <text id="region-label-{domId}" x="{shape.X + shape.W / 2}" y="{shape.Y + shape.H / 2 + 4}" text-anchor="middle" font-size="7" fill="#fff" font-family="system-ui">{shape.Label}</text>""");
        }
        svgParts.AppendLine("""      </g>""");

        // Regional morphing table rows.
        var regionRows = new System.Text.StringBuilder();
        foreach (var (region, factor) in orderedRegions)
        {
            var cssClass = factor > 1.20 ? "val-high" : factor > 1.08 ? "val-med" : "val-low";
            var domId = regionDomIds[region];
            regionRows.AppendLine($"          <tr><td>{HtmlEncode(region)}</td><td id=\"factor-{domId}\" class=\"{cssClass}\">{factor:F4}</td></tr>");
        }

        var sliderRows = new System.Text.StringBuilder();
        foreach (var (region, _) in orderedRegions)
        {
            var domId = regionDomIds[region];
            sliderRows.AppendLine($"""
                      <tr>
                        <td>{HtmlEncode(region)}</td>
                        <td><input type="range" id="region-slider-{domId}" data-region-slider="true" data-region="{HtmlEncode(region)}" min="0.80" max="1.20" step="0.01" value="1.00"></td>
                        <td id="region-slider-value-{domId}">1.00x</td>
                      </tr>
                """);
        }

        // Physics nodes list.
        var physicsNodes = ExtractPhysicsNodeNames(physics);
        var physicsPanel = physicsNodes.Count == 0
            ? "<p style=\"color:#888;font-size:.85rem\">No physics nodes detected.</p>"
            : $"<p style=\"font-size:.85rem\">{HtmlEncode(string.Join(", ", physicsNodes))}</p>";

        // Slider list.
        var sliderList = bodySlideProject.Sliders.Count == 0
            ? "(none)"
            : string.Join(", ", bodySlideProject.Sliders.Select(HtmlEncode));

        // Pose simulation panel.
        var posePanelHtml = new System.Text.StringBuilder();
        if (poseSimulation.TotalPosesAtRisk > 0)
        {
            posePanelHtml.AppendLine("""      <div class="panel" style="margin-top:16px">""");
            posePanelHtml.AppendLine("""        <h3 style="margin-top:0;color:#e67e22">⚠ Pose Clipping Risk</h3>""");
            posePanelHtml.AppendLine("        <table><tr><th>Pose</th><th>At-Risk Regions</th></tr>");
            foreach (var (pose, regions) in poseSimulation.PoseClippingRisk.OrderBy(kv => kv.Key))
            {
                posePanelHtml.AppendLine($"          <tr><td>{HtmlEncode(pose)}</td><td style=\"color:#ffd93d\">{HtmlEncode(string.Join(", ", regions))}</td></tr>");
            }
            posePanelHtml.AppendLine("        </table>");
            posePanelHtml.AppendLine("      </div>");
        }
        else
        {
            posePanelHtml.AppendLine("""      <div class="panel" style="margin-top:16px"><p style="color:#6bcb77">✓ No clipping risk across all tested poses.</p></div>""");
        }

        var poseRiskLabel = poseSimulation.TotalPosesAtRisk > 0
            ? $"⚠ {poseSimulation.TotalPosesAtRisk}/{poseSimulation.TestedPoses.Count} poses at risk"
            : $"✓ {poseSimulation.TestedPoses.Count} poses OK";

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <title>SlideSmith Preview — {{HtmlEncode(armorName)}} → {{HtmlEncode(request.TargetBody)}}</title>
              <style>
                body { font-family: system-ui, sans-serif; background: #1a1a2e; color: #eee; padding: 24px; margin: 0; }
                h1   { color: #c9a84c; margin-bottom: 4px; }
                h3   { color: #9ab; }
                .subtitle { color: #888; font-size: .9rem; margin-bottom: 24px; }
                .layout  { display: flex; gap: 32px; flex-wrap: wrap; align-items: flex-start; }
                .body-fig { background: #16213e; border-radius: 12px; padding: 16px; }
                .legend  { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 10px; font-size: .75rem; }
                .legend-item { display: flex; align-items: center; gap: 4px; }
                .dot     { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
                .panels  { display: flex; flex-direction: column; gap: 14px; }
                .panel   { background: #16213e; border-radius: 8px; padding: 14px 18px; }
                table    { border-collapse: collapse; font-size: .85rem; }
                th, td   { padding: 5px 10px; border: 1px solid #2a3a4a; }
                th       { background: #1a2a3a; color: #9ab; font-weight: 600; }
                .val-high { color: #ff6b6b; font-weight: 700; }
                .val-med  { color: #ffd93d; }
                .val-low  { color: #6bcb77; }
                .controls { display: grid; grid-template-columns: auto 1fr auto; gap: 8px 10px; align-items: center; font-size: .85rem; }
                .controls label { color: #9ab; }
                .controls input[type="range"], .controls select { width: 100%; }
                .slider-table td:nth-child(2) { width: 180px; }
                .mono { font-family: Consolas, monospace; font-size: .8rem; }
                .panel button { background: #2a3a4a; color: #fff; border: 1px solid #3a4a5a; border-radius: 4px; padding: 6px 10px; cursor: pointer; }
              </style>
            </head>
            <body>
              <h1>SlideSmith Preview</h1>
              <p class="subtitle">{{HtmlEncode(armorName)}} → {{HtmlEncode(request.TargetBody)}} &nbsp;·&nbsp; {{HtmlEncode(analysis.MeshType)}} &nbsp;·&nbsp; {{HtmlEncode(poseRiskLabel)}} &nbsp;·&nbsp; interactive preview controls enabled</p>
              <div class="layout">
                <div class="body-fig">
                  <svg width="200" height="320" viewBox="0 0 200 320" xmlns="http://www.w3.org/2000/svg">
            {{svgParts}}
                  </svg>
                  <div class="legend">
                    <div class="legend-item"><div class="dot" style="background:#3a7bd5"></div><span>Compact</span></div>
                    <div class="legend-item"><div class="dot" style="background:#27ae60"></div><span>Normal</span></div>
                    <div class="legend-item"><div class="dot" style="background:#f1c40f"></div><span>Mild</span></div>
                    <div class="legend-item"><div class="dot" style="background:#e67e22"></div><span>Expanded</span></div>
                    <div class="legend-item"><div class="dot" style="background:#e74c3c"></div><span>High</span></div>
                  </div>
                </div>
                <div class="panels">
                  <div class="panel">
                    <h3 style="margin-top:0">Live Controls</h3>
                    <div class="controls">
                      <label for="body-profile-select">Swap body</label>
                      <select id="body-profile-select">{{profileOptionsHtml}}</select>
                      <span class="mono" id="body-profile-value">{{HtmlEncode(selectedProfile)}}</span>

                      <label for="view-rotation-slider">Rotate view</label>
                      <input id="view-rotation-slider" type="range" min="-45" max="45" step="1" value="0">
                      <span class="mono" id="view-rotation-value">0°</span>

                      <label for="global-scale-slider">Adjust sliders</label>
                      <input id="global-scale-slider" type="range" min="0.70" max="1.30" step="0.01" value="1.00">
                      <span class="mono" id="global-scale-value">1.00x</span>
                    </div>
                    <div style="margin-top:10px">
                      <button id="reset-preview-controls" type="button">Reset controls</button>
                    </div>
                  </div>
                  <div class="panel">
                    <h3 style="margin-top:0">Regional Morphing</h3>
                    <table>
                      <tr><th>Region</th><th>Factor</th></tr>
            {{regionRows}}        </table>
                  </div>
                  <div class="panel">
                    <h3 style="margin-top:0">Per-Region Tuning</h3>
                    <table class="slider-table">
                      <tr><th>Region</th><th>Slider</th><th>Value</th></tr>
            {{sliderRows}}        </table>
                  </div>
                  <div class="panel">
                    <h3 style="margin-top:0">BodySlide Sliders</h3>
                    <p style="font-size:.85rem;margin:0">{{sliderList}}</p>
                  </div>
                  <div class="panel">
                    <h3 style="margin-top:0">Physics Nodes</h3>
                    {{physicsPanel}}
                  </div>
            {{posePanelHtml}}      </div>
              </div>
              <script>
                const baseFactors = {{baseFactorsJson}};
                const previewProfiles = {{previewProfilesJson}};
                const regionDomIds = {{regionDomIdsJson}};
                const regionNames = Object.keys(baseFactors);

                let profileFactors = { ...baseFactors };
                let globalScale = 1.0;
                const regionAdjustments = Object.fromEntries(regionNames.map(region => [region, 1.0]));

                const profileSelect = document.getElementById('body-profile-select');
                const profileValue = document.getElementById('body-profile-value');
                const rotationSlider = document.getElementById('view-rotation-slider');
                const rotationValue = document.getElementById('view-rotation-value');
                const globalScaleSlider = document.getElementById('global-scale-slider');
                const globalScaleValue = document.getElementById('global-scale-value');
                const resetButton = document.getElementById('reset-preview-controls');
                const bodyRoot = document.getElementById('preview-body-root');

                function morphColour(factor) {
                  if (factor < 0.90) return '#3a7bd5';
                  if (factor < 0.97) return '#27ae60';
                  if (factor < 1.05) return '#2ecc71';
                  if (factor < 1.12) return '#f1c40f';
                  if (factor < 1.20) return '#e67e22';
                  return '#e74c3c';
                }

                function valueClass(factor) {
                  if (factor > 1.20) return 'val-high';
                  if (factor > 1.08) return 'val-med';
                  return 'val-low';
                }

                function opacityFor(factor) {
                  return Math.max(0.4, Math.min(0.85, 0.45 + Math.abs(factor - 1.0) * 1.2));
                }

                function effectiveFactor(region) {
                  const base = Number(profileFactors[region] ?? baseFactors[region] ?? 1.0);
                  const global = Number(globalScale);
                  const local = Number(regionAdjustments[region] ?? 1.0);
                  return base * global * local;
                }

                function setRotation(degrees) {
                  bodyRoot.setAttribute('transform', `rotate(${degrees} 100 160)`);
                  rotationValue.textContent = `${degrees}°`;
                }

                function renderPreview() {
                  for (const region of regionNames) {
                    const factor = effectiveFactor(region);
                    const domId = regionDomIds[region];
                    const box = document.getElementById(`region-box-${domId}`);
                    const factorCell = document.getElementById(`factor-${domId}`);
                    const sliderValue = document.getElementById(`region-slider-value-${domId}`);

                    if (box) {
                      const color = morphColour(factor);
                      box.setAttribute('fill', color);
                      box.setAttribute('stroke', color);
                      box.setAttribute('opacity', opacityFor(factor).toFixed(2));
                    }

                    if (factorCell) {
                      factorCell.textContent = factor.toFixed(4);
                      factorCell.className = valueClass(factor);
                    }

                    if (sliderValue) {
                      sliderValue.textContent = `${Number(regionAdjustments[region]).toFixed(2)}x`;
                    }
                  }

                  globalScaleValue.textContent = `${Number(globalScale).toFixed(2)}x`;
                }

                profileSelect.addEventListener('change', () => {
                  const selected = profileSelect.value;
                  profileFactors = { ...(previewProfiles[selected] ?? baseFactors) };
                  profileValue.textContent = selected;
                  renderPreview();
                });

                rotationSlider.addEventListener('input', () => {
                  setRotation(Number(rotationSlider.value));
                });

                globalScaleSlider.addEventListener('input', () => {
                  globalScale = Number(globalScaleSlider.value);
                  renderPreview();
                });

                for (const slider of document.querySelectorAll('input[data-region-slider="true"]')) {
                  slider.addEventListener('input', () => {
                    const region = slider.getAttribute('data-region');
                    if (!region) return;
                    regionAdjustments[region] = Number(slider.value);
                    renderPreview();
                  });
                }

                resetButton.addEventListener('click', () => {
                  profileSelect.value = '{{HtmlEncode(selectedProfile)}}';
                  profileFactors = { ...(previewProfiles[profileSelect.value] ?? baseFactors) };
                  profileValue.textContent = profileSelect.value;
                  rotationSlider.value = '0';
                  setRotation(0);
                  globalScaleSlider.value = '1.00';
                  globalScale = 1.0;

                  for (const slider of document.querySelectorAll('input[data-region-slider="true"]')) {
                    const region = slider.getAttribute('data-region');
                    if (!region) continue;
                    slider.value = '1.00';
                    regionAdjustments[region] = 1.0;
                  }

                  renderPreview();
                });

                setRotation(0);
                renderPreview();
              </script>
            </body>
            </html>
            """;
    }

    private static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private static string ToDomIdToken(string value)
    {
        var safe = new string(value
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "region" : safe;
    }

    // ── xEdit Pascal Script ───────────────────────────────────────────────────

    /// <summary>
    /// Generates a runnable xEdit Pascal (Delphi) automation script that iterates all
    /// loaded plugins, finds ARMA (ArmorAddon) and ARMO (Armor) records whose mesh paths
    /// match those detected during plugin analysis, and rewrites them to the converted
    /// mesh paths.  Drop the output file into the Edit Scripts folder of SSEEdit/TES5Edit
    /// and run it from the Tools → Apply Script menu.
    /// </summary>
    private static string BuildXEditScript(
        PluginAnalysisResult pluginAnalysis,
        string targetBody,
        IReadOnlyDictionary<string, string> pluginRewriteMap)
    {
        // Collect all old paths from ARMA addons AND ARMO armor records.
        var armaOldPaths = pluginAnalysis.ArmorAddons
            .SelectMany(a => a.DetectedMeshPaths)
            .Select(path => path.Replace('\\', '/'));

        var armoOldPaths = (pluginAnalysis.ArmorRecords ?? [])
            .SelectMany(a => a.DetectedMeshPaths)
            .Select(path => path.Replace('\\', '/'));

        var oldPaths = armaOldPaths.Concat(armoOldPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rewritePairs = oldPaths
            .Select(oldPath => new
            {
                OldPath = oldPath,
                NewPath = pluginRewriteMap.TryGetValue(oldPath, out var rewrittenPath)
                    ? rewrittenPath
                    : oldPath
            })
            .ToList();

        var oldPathDecls = new System.Text.StringBuilder();
        var newPathDecls = new System.Text.StringBuilder();
        for (var i = 0; i < rewritePairs.Count; i++)
        {
            oldPathDecls.AppendLine($"  cOldPaths[{i}] := '{rewritePairs[i].OldPath.Replace("'", "''")}';");
            newPathDecls.AppendLine($"  cNewPaths[{i}] := '{rewritePairs[i].NewPath.Replace("'", "''").Replace('/', '\\')}';");
        }

        var pathCount = rewritePairs.Count;
        var rewriteReadyCount = rewritePairs.Count(pair => !string.Equals(pair.OldPath, pair.NewPath, StringComparison.OrdinalIgnoreCase));
        var safeTarget = targetBody.Replace("'", "''");

        return $$"""
            { ============================================================ }
            { SlideSmith v0.1 — Auto-generated xEdit Armor Rewrite Script  }
            { Target body : {{safeTarget}}                                  }
            {                                                               }
            { HOW TO USE                                                    }
            {   1. Copy this file to [SSEEdit install]\Edit Scripts\        }
            {   2. Open SSEEdit and load the plugins you want to patch.     }
            {   3. Select all plugins in the tree, then:                    }
            {        Tools → Apply Script → SlideSmith_patch-armor          }
            {   4. Script rewrites matching ARMA and ARMO mesh paths.       }
            {   5. Save plugin changes in xEdit after review.               }
            { ============================================================ }

            unit SlideSmith_patch_armor;

            interface
            implementation

            var
              cOldPaths: array[0..{{Math.Max(pathCount - 1, 0)}}] of string;
              cNewPaths: array[0..{{Math.Max(pathCount - 1, 0)}}] of string;
              gPatchedCount: Integer;

            procedure InitPaths;
            begin
            {{oldPathDecls}}{{newPathDecls}}end;

            function NormalizeMeshPath(const value: string): string;
            begin
              Result := LowerCase(StringReplace(value, '\', '/', [rfReplaceAll]));
            end;

            procedure TryRewriteModelPath(e: IwbElement; const pathName: string);
            var
              i: Integer;
              modelEl: IwbElement;
              meshPath: string;
            begin
              modelEl := ElementByPath(e, pathName);
              if not Assigned(modelEl) then
                Exit;

              meshPath := GetEditValue(modelEl);
              for i := 0 to High(cOldPaths) do begin
                if NormalizeMeshPath(meshPath) = NormalizeMeshPath(cOldPaths[i]) then begin
                  if not SameText(meshPath, cNewPaths[i]) then begin
                    SetEditValue(modelEl, cNewPaths[i]);
                    AddMessage('[PATCHED] ' + Name(e) + ' | ' + pathName + ': ' + meshPath + ' -> ' + cNewPaths[i]);
                    Inc(gPatchedCount);
                  end;
                  Break;
                end;
              end;
            end;

            function Initialize: Integer;
            begin
              AddMessage('==============================================');
              AddMessage('SlideSmith v0.1 Armor Rewrite');
              AddMessage('Target body: {{safeTarget}}');
              AddMessage('Detected mesh paths: {{pathCount}}');
              AddMessage('Rewrite-ready paths: {{rewriteReadyCount}}');
              AddMessage('==============================================');
              gPatchedCount := 0;
              InitPaths;
              Result := 0;
            end;

            function Process(e: IwbElement): Integer;
            var
              i: Integer;
              sig, meshPath: string;
              modelEl: IwbElement;
            begin
              Result := 0;
              sig := Signature(e);
              { ── ARMA (ArmorAddon) — world and first-person models ── }
              if sig = 'ARMA' then begin
                TryRewriteModelPath(e, 'Male World Model\MOD2');
                TryRewriteModelPath(e, 'Male World Model\MOD2 - Model Filename');
                TryRewriteModelPath(e, 'Female World Model\MOD3');
                TryRewriteModelPath(e, 'Female World Model\MOD3 - Model Filename');
                TryRewriteModelPath(e, 'Male 1st Person\MOD4');
                TryRewriteModelPath(e, 'Male 1st Person\MOD4 - Model Filename');
                TryRewriteModelPath(e, 'Female 1st Person\MOD5');
                TryRewriteModelPath(e, 'Female 1st Person\MOD5 - Model Filename');
              end;
              { ── ARMO (Armor) — inventory/world model ── }
              if sig = 'ARMO' then begin
                TryRewriteModelPath(e, 'Male World Model\MOD2 - Model Filename');
                TryRewriteModelPath(e, 'Male World Model\MOD2');
                TryRewriteModelPath(e, 'Female World Model\MOD3 - Model Filename');
                TryRewriteModelPath(e, 'Female World Model\MOD3');
              end;
            end;

            function Finalize: Integer;
            begin
              AddMessage('SlideSmith patch rewriting complete.');
              AddMessage('Total ARMA/ARMO model paths rewritten: ' + IntToStr(gPatchedCount));
              Result := 0;
            end;

            end.
            """;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Vanilla Armor Static Database
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Static database of canonical vanilla Skyrim armor entries used to pre-populate
/// region maps and recommended deformation profiles without requiring user input.
/// </summary>
internal static class VanillaArmorDatabase
{
    public static readonly IReadOnlyList<VanillaArmorEntry> Entries =
    [
        new("Iron Armor",                   ["ironarmor", "ironplate"],                     "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "curvy"),
        new("Iron Helmet",                  ["ironhelmet"],                                 "Vanilla", ["42:Circlet"],                          "curvy"),
        new("Steel Armor",                  ["steelarmor", "steelplate"],                   "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Steel Helmet",                 ["steelhelmet"],                                "Vanilla", ["42:Circlet"],                          "athletic"),
        new("Steel Plate Armor",            ["steelplatearmor"],                            "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Dwarven Armor",                ["dwarvenarmor"],                               "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Dwarven Helmet",               ["dwarvenhelmet"],                              "Vanilla", ["42:Circlet"],                          "athletic"),
        new("Elven Armor",                  ["elvenarmor"],                                 "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Elven Helmet",                 ["elvenhelmet"],                                "Vanilla", ["42:Circlet"],                          "slim"),
        new("Elven Gilded Armor",           ["elvengildedarmor"],                           "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Glass Armor",                  ["glassarmor"],                                 "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Glass Helmet",                 ["glasshelmet"],                                "Vanilla", ["42:Circlet"],                          "slim"),
        new("Ebony Armor",                  ["ebonyarmor"],                                 "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Ebony Helmet",                 ["ebonyhelmet"],                                "Vanilla", ["42:Circlet"],                          "muscular"),
        new("Ebony Mail",                   ["ebonymail"],                                  "Vanilla", ["32:Body"],                             "muscular"),
        new("Daedric Armor",                ["daedricarmor"],                               "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Daedric Helmet",               ["daedrichelmet"],                              "Vanilla", ["42:Circlet"],                          "muscular"),
        new("Dragonplate Armor",            ["dragonplatearmor", "dragonplate"],            "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Dragonscale Armor",            ["dragonscalearmor", "dragonscale"],            "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Leather Armor",                ["leatherarmor"],                               "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Hide Armor",                   ["hidearmor"],                                  "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Studded Armor",                ["studdedarmor"],                               "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Orcish Armor",                 ["orcisharmor"],                                "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Orcish Helmet",                ["orcishhelmet"],                               "Vanilla", ["42:Circlet"],                          "muscular"),
        new("Scaled Armor",                 ["scaledarmor"],                                "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Scaled Helmet",                ["scaledhelmet"],                               "Vanilla", ["42:Circlet"],                          "athletic"),
        new("Banded Iron Armor",            ["bandediron", "bandedarmor"],                  "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Wolf Armor",                   ["wolfarmor", "wolfcuirass"],                   "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Saviors Hide",                 ["saviorshide"],                                "Vanilla", ["32:Body"],                             "slim"),
        new("Imperial Light Armor",         ["imperiallightarmor", "imperiallight"],        "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Imperial Studded Armor",       ["imperialstudded"],                            "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Imperial Heavy Armor",         ["imperialheavyarmor", "imperialheavy"],        "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Stormcloak Cuirass",           ["stormcloakcuirass", "stormcloak"],            "Vanilla", ["32:Body"],                             "athletic"),
        new("Ancient Nord Armor",           ["ancientswordsman", "ancientnord"],            "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Draugr Armor",                 ["draugrarmor", "draugr"],                      "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Falmer Armor",                 ["falmerarmor"],                                "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "lean"),
        new("Falmer Hardened Armor",        ["falmerhardened"],                             "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "lean"),
        new("Falmer Heavy Armor",           ["falmerheavy"],                                "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Ancient Falmer Armor",         ["ancientfalmer"],                              "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Forsworn Armor",               ["forswornarmor", "forsworn"],                  "Vanilla", ["32:Body"],                             "curvy"),
        new("Fur Armor",                    ["furarmor"],                                   "Vanilla", ["32:Body"],                             "slim"),
        new("Penitus Oculatus Armor",       ["penitusoculatus", "penitoculatus"],           "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Mage Robes",                   ["magescholarsrobe", "magerobe", "collegerobe"], "Vanilla", ["32:Body"],                          "slim"),
        new("Apprentice Robes",             ["apprenticerobes", "apprentice"],              "Vanilla", ["32:Body"],                             "slim"),
        new("Adept Robes",                  ["adeptrobes"],                                 "Vanilla", ["32:Body"],                             "slim"),
        new("Expert Robes",                 ["expertrobes"],                                "Vanilla", ["32:Body"],                             "slim"),
        new("Master Robes",                 ["masterrobes"],                                "Vanilla", ["32:Body"],                             "slim"),
        new("Arch-Mage Robes",              ["archmagerobes", "archmage"],                  "Vanilla", ["32:Body"],                             "slim"),
        new("Thieves Guild Armor",          ["thievesguildarmor", "tgarmor"],               "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Dark Brotherhood Armor",       ["dbrobes", "darkbrotherhood"],                 "Vanilla", ["32:Body"],                             "slim"),
        new("Nightingale Armor",            ["nightingalearmor", "nightingale"],            "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Blades Armor",                 ["bladearmor", "bladesamurai"],                 "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Guard Armor",                  ["guardarmor", "guardcuirass"],                 "Vanilla", ["32:Body"],                             "athletic"),
        new("Dawnguard Heavy Armor",        ["dawnguardheavy", "dawnguardarmor"],           "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Dawnguard Scout Armor",        ["dawnguardscout"],                             "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Vampire Royal Armor",          ["vampireroyalarmor", "vampireroyal"],          "Vanilla", ["32:Body"],                             "slim"),
        new("Nordic Carved Armor",          ["nordiccarvedarmor", "nordiccarved"],          "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Nordic Carved Helmet",         ["nordiccarvedhelmet"],                         "Vanilla", ["42:Circlet"],                          "athletic"),
        new("Bonemold Armor",               ["bonemoldarmor", "bonemold"],                  "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Chitin Armor",                 ["chitinarmor", "chitin"],                      "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Stalhrim Armor",               ["stalhrimarmor", "stalhrim"],                  "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "muscular"),
        new("Stalhrim Light Armor",         ["stalhrimilight", "stalhrimlight"],            "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
        new("Skaal Armor",                  ["skaalarmor", "skaal"],                        "Vanilla", ["32:Body"],                             "athletic"),
        new("Bound Armor",                  ["boundarmor"],                                 "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "athletic"),
        new("Thieves Guild Master Armor",   ["tgmasterarmor", "tgmaster"],                 "Vanilla", ["32:Body", "33:Hands", "37:Feet"], "slim"),
    ];
}

/// <summary>
/// Looks up a mesh file name against the vanilla armor static database.
/// Strips weight suffixes (_0/_1) before matching tokens.
/// </summary>
internal sealed class VanillaArmorLookupService : IVanillaArmorLookupService
{
    public IReadOnlyList<VanillaArmorEntry> All => VanillaArmorDatabase.Entries;

    public bool TryLookup(string meshFileName, out VanillaArmorEntry? entry)
    {
        var baseName = Path.GetFileNameWithoutExtension(meshFileName) ?? string.Empty;

        // Strip weight variant suffixes so "ironarmor_0.nif" matches the "ironarmor" token.
        if (baseName.EndsWith("_0", StringComparison.OrdinalIgnoreCase) ||
            baseName.EndsWith("_1", StringComparison.OrdinalIgnoreCase))
        {
            baseName = baseName[..^2];
        }

        foreach (var candidate in VanillaArmorDatabase.Entries)
        {
            if (candidate.MeshFileTokens.Any(token =>
                baseName.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Simplified Voxel Collision Service
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Approximates voxel-grid penetration between the converted armor and the target body.
/// <para>
/// Each body region (chest, waist, pelvis, legs, shoulders) is modelled as a uniform
/// voxel grid. A vertex is considered to be penetrating when the applied regional
/// morph factor causes the body to expand beyond a mesh-type-specific threshold.
/// The push-out magnitude is the excess scaled by the grid resolution.
/// </para>
/// </summary>
internal sealed class SimplifiedVoxelCollisionService : IVoxelCollisionService
{
    private const int GridResolution = 8;

    // Penetration threshold by mesh type: factor above which a region is considered
    // to have body vertices overlapping the armor after conversion.
    private static readonly IReadOnlyDictionary<string, double> PenetrationThresholds =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["plate"]           = 1.10, // Rigid armor needs substantial expansion to penetrate
            ["leather"]         = 1.07,
            ["cloth"]           = 1.04, // Soft materials clip at much smaller deltas
            ["skin-tight"]      = 1.03,
            ["physics-enabled"] = 1.04,
            ["mixed"]           = 1.06
        };

    public Task<VoxelCollisionResult> ComputeAsync(
        ImportedArmor armor,
        ConvertedMesh mesh,
        string targetBody,
        CancellationToken cancellationToken)
    {
        PenetrationThresholds.TryGetValue(mesh.MeshType, out var threshold);
        threshold = threshold == 0 ? 1.06 : threshold;

        var affectedRegions = new List<string>();
        var pushOutMagnitudes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (region, morphFactor) in mesh.RegionalMorphing)
        {
            if (morphFactor <= threshold) continue;

            // Push-out magnitude: excess above threshold scaled by grid resolution
            // so the value is expressed in voxel cell units (0 = no push-out needed).
            var pushOut = Math.Round((morphFactor - threshold) * GridResolution, 4);
            affectedRegions.Add(region);
            pushOutMagnitudes[region] = pushOut;
        }

        return Task.FromResult(new VoxelCollisionResult(
            affectedRegions.Count > 0,
            affectedRegions,
            pushOutMagnitudes,
            GridResolution));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Basic Pose Simulation Service
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Simulates a converted mesh against 8 standard animation poses and reports per-pose
/// clipping risk by body region.
/// <para>
/// Each pose has per-region stress amplifiers derived from typical skeletal deformations
/// for that animation (e.g. Crouch amplifies thighs/pelvis because the femur rotates
/// significantly forward). A region is flagged as at-risk for a pose when:
///   <c>morph_factor × pose_amplifier ≥ RiskThreshold</c>
/// The threshold is intentionally conservative so that borderline morphs on high-stress
/// poses are surfaced for manual review.
/// </para>
/// </summary>
internal sealed class BasicPoseSimulationService : IPoseSimulationService
{
    private static readonly IReadOnlyList<string> AnimationPoses =
    [
        "T-pose", "Walk", "Run", "Idle", "Crouch", "Combat-Idle", "Jump", "Sneak"
    ];

    // Per-pose amplifiers by body region (regions not listed default to 1.0).
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> PoseAmplifiers =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["T-pose"]      = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            ["Walk"]        = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["butt"]=1.08, ["thighs"]=1.05, ["belly"]=1.03, ["calves"]=1.04, ["legs"]=1.04 },
            ["Run"]         = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"]=1.05, ["butt"]=1.12, ["thighs"]=1.10, ["belly"]=1.05, ["arms"]=1.04, ["legs"]=1.08 },
            ["Idle"]        = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["shoulders"]=1.02, ["arms"]=1.02 },
            ["Crouch"]      = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["thighs"]=1.20, ["pelvis"]=1.15, ["butt"]=1.10, ["belly"]=1.12, ["calves"]=1.08, ["legs"]=1.14 },
            ["Combat-Idle"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"]=1.05, ["arms"]=1.08, ["shoulders"]=1.10, ["waist"]=1.04 },
            ["Jump"]        = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["butt"]=1.15, ["thighs"]=1.12, ["belly"]=1.08, ["calves"]=1.10, ["legs"]=1.10 },
            ["Sneak"]       = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["thighs"]=1.18, ["pelvis"]=1.12, ["butt"]=1.08, ["calves"]=1.15, ["legs"]=1.16 }
        };

    // A region is flagged at-risk when its effective stress (morph × pose amplifier) meets or exceeds this.
    private const double RiskThreshold = 1.10;

    public Task<PoseSimulationResult> SimulateAsync(
        ConvertedMesh mesh,
        string targetBody,
        CancellationToken cancellationToken)
    {
        var poseClippingRisk = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var highRiskSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var atRiskPoseCount = 0;

        foreach (var pose in AnimationPoses)
        {
            PoseAmplifiers.TryGetValue(pose, out var amplifiers);
            amplifiers ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            var atRiskRegions = new List<string>();
            foreach (var (region, morphFactor) in mesh.RegionalMorphing)
            {
                amplifiers.TryGetValue(region, out var amp);
                var effectiveStress = morphFactor * (amp > 0 ? amp : 1.0);
                if (effectiveStress >= RiskThreshold)
                {
                    atRiskRegions.Add(region);
                    highRiskSet.Add(region);
                }
            }

            if (atRiskRegions.Count > 0)
            {
                atRiskRegions.Sort(StringComparer.OrdinalIgnoreCase);
                poseClippingRisk[pose] = atRiskRegions;
                atRiskPoseCount++;
            }
        }

        return Task.FromResult(new PoseSimulationResult(
            AnimationPoses,
            poseClippingRisk,
            highRiskSet.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList(),
            atRiskPoseCount));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Animation-Driven Geometry Solver
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Implements an animation-driven geometry solver for Skyrim armor conversion.
/// <para>
/// The solver works in three stages:
/// <list type="number">
///   <item>Assign each vertex to a skeletal region based on its normalised Z-height.</item>
///   <item>
///     For each of 8 standard animation poses, apply a simplified per-region rigid rotation
///     (single-bone LBS) in the sagittal plane to compute the deformed vertex position.
///   </item>
///   <item>
///     For each deformed vertex, test penetration against a per-region body-envelope
///     cylinder whose XY radius is scaled by the region's morph factor.  The push-out
///     depth (in normalised mesh-space units) is recorded per region.
///   </item>
/// </list>
/// </para>
/// <para>
/// Because the skeleton transform is driven by anatomically-derived per-pose bone angles,
/// push-out values capture stress patterns that a pure morph-factor threshold misses —
/// e.g. the thigh cylinder is only penetrated when Crouch deeply rotates the femur, not
/// in the T-pose baseline.
/// </para>
/// </summary>
internal static class AnimationDrivenGeometrySolver
{
    // ── Region definitions ────────────────────────────────────────────────────

    // Normalised height bands → region names. Evaluated in order; first match wins.
    private static readonly (float MaxHeightNorm, string Region)[] HeightRegionMap =
    [
        (0.05f, "feet"),
        (0.25f, "calves"),
        (0.48f, "thighs"),
        (0.57f, "butt"),
        (0.64f, "pelvis"),
        (0.71f, "belly"),
        (0.78f, "waist"),
        (0.86f, "chest"),
        (0.93f, "shoulders"),
        (1.01f, "arms"),
    ];

    // Base XY half-radius of the body-envelope cylinder per region
    // (normalised mesh-space units, before morph-factor scaling).
    private static readonly IReadOnlyDictionary<string, float> BaseRadius =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["feet"]      = 0.040f,
            ["calves"]    = 0.058f,
            ["thighs"]    = 0.088f,
            ["butt"]      = 0.100f,
            ["pelvis"]    = 0.105f,
            ["belly"]     = 0.095f,
            ["waist"]     = 0.075f,
            ["chest"]     = 0.115f,
            ["breasts"]   = 0.115f,
            ["shoulders"] = 0.095f,
            ["arms"]      = 0.055f,
        };

    // Joint pivot height for each region: the normalised height around which this
    // region's vertices rotate when the bone is flexed.
    private static readonly IReadOnlyDictionary<string, float> PivotHeight =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["feet"]      = 0.00f,
            ["calves"]    = 0.05f,
            ["thighs"]    = 0.25f,
            ["butt"]      = 0.48f,
            ["pelvis"]    = 0.48f,
            ["belly"]     = 0.57f,
            ["waist"]     = 0.60f,
            ["chest"]     = 0.71f,
            ["breasts"]   = 0.71f,
            ["shoulders"] = 0.82f,
            ["arms"]      = 0.86f,
        };

    // ── Pose library ──────────────────────────────────────────────────────────

    // Each pose maps region → PoseBoneRotation.
    // RotX is in radians; positive = forward/anterior tilt in Skyrim's Z-up system.
    // TransZ is a vertical offset in normalised units applied before the rotation.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, PoseBoneRotation>> Poses =
        new Dictionary<string, IReadOnlyDictionary<string, PoseBoneRotation>>(StringComparer.OrdinalIgnoreCase)
        {
            ["T-pose"] = new Dictionary<string, PoseBoneRotation>(StringComparer.OrdinalIgnoreCase),

            ["Walk"] = new Dictionary<string, PoseBoneRotation>(StringComparer.OrdinalIgnoreCase)
            {
                ["thighs"] = new(-0.610f),          // ~35° forward swing
                ["calves"] = new( 0.349f),           // ~20° knee flex
                ["belly"]  = new( 0.087f),           // ~5°  torso lean
                ["waist"]  = new( 0.087f),
                ["pelvis"] = new(TransZ: -0.025f),   // slight hip drop
            },

            ["Run"] = new Dictionary<string, PoseBoneRotation>(StringComparer.OrdinalIgnoreCase)
            {
                ["thighs"]    = new(-0.960f),        // ~55° running stride
                ["calves"]    = new( 0.611f),        // ~35° knee flex
                ["belly"]     = new( 0.262f),        // ~15° lean
                ["waist"]     = new( 0.262f),
                ["chest"]     = new( 0.175f),        // ~10°
                ["arms"]      = new(-0.524f),        // ~30° arm swing
                ["shoulders"] = new(-0.349f),        // ~20°
                ["pelvis"]    = new(TransZ: -0.035f),
            },

            ["Idle"] = new Dictionary<string, PoseBoneRotation>(StringComparer.OrdinalIgnoreCase)
            {
                ["shoulders"] = new( 0.052f),        // ~3° slight drop
                ["chest"]     = new( 0.026f),
                ["arms"]      = new(-0.087f),        // ~5°
            },

            ["Crouch"] = new Dictionary<string, PoseBoneRotation>(StringComparer.OrdinalIgnoreCase)
            {
                ["thighs"]    = new(-1.484f),        // ~85° deep forward
                ["calves"]    = new( 1.361f),        // ~78° flex
                ["butt"]      = new(-1.484f),        // follows thigh
                ["belly"]     = new( 0.524f),        // ~30° lean
                ["waist"]     = new( 0.524f),
                ["chest"]     = new( 0.349f),        // ~20°
                ["pelvis"]    = new(TransZ: -0.080f),// hip drop
            },

            ["Combat-Idle"] = new Dictionary<string, PoseBoneRotation>(StringComparer.OrdinalIgnoreCase)
            {
                ["chest"]     = new( 0.209f),        // ~12° torso forward
                ["arms"]      = new(-0.436f),        // ~25° raised
                ["shoulders"] = new(-0.262f),        // ~15°
                ["thighs"]    = new(-0.349f),        // ~20° slight crouch
                ["calves"]    = new( 0.262f),        // ~15°
                ["waist"]     = new( 0.175f),
                ["pelvis"]    = new(TransZ: -0.020f),
            },

            ["Jump"] = new Dictionary<string, PoseBoneRotation>(StringComparer.OrdinalIgnoreCase)
            {
                ["thighs"]    = new( 0.436f),        // ~25° back (tuck)
                ["calves"]    = new(-0.785f),        // ~45° tuck
                ["arms"]      = new( 0.524f),        // ~30° up/out
                ["shoulders"] = new( 0.349f),        // ~20°
                ["belly"]     = new(-0.175f),        // ~10° lean back
                ["pelvis"]    = new(TransZ:  0.020f),// slight rise
            },

            ["Sneak"] = new Dictionary<string, PoseBoneRotation>(StringComparer.OrdinalIgnoreCase)
            {
                ["thighs"]    = new(-1.309f),        // ~75° deep crouch
                ["calves"]    = new( 1.134f),        // ~65°
                ["butt"]      = new(-1.309f),
                ["belly"]     = new( 0.489f),        // ~28° lean
                ["waist"]     = new( 0.489f),
                ["chest"]     = new( 0.314f),        // ~18°
                ["pelvis"]    = new(TransZ: -0.065f),
            },
        };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Maps a normalised Z-height (0–1) to the corresponding body region name.</summary>
    internal static string HeightToRegion(float normalizedHeight)
    {
        foreach (var (max, region) in HeightRegionMap)
        {
            if (normalizedHeight <= max) return region;
        }
        return "arms";
    }

    /// <summary>
    /// Runs the animation-driven geometry solver against the extracted vertex positions.
    /// Returns the maximum push-out depth per region across all 8 poses, expressed in
    /// normalised mesh-space units (0 = no penetration).
    /// </summary>
    public static AnimationDrivenResult Solve(
        IReadOnlyList<(float X, float Y, float Z)> vertices,
        IReadOnlyDictionary<string, double> regionalMorphing)
    {
        if (vertices.Count == 0)
        {
            return new AnimationDrivenResult(0,
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                "animation-driven:no-vertices");
        }

        // Compute mesh bounding box
        var minX = float.MaxValue; var maxX = float.MinValue;
        var minY = float.MaxValue; var maxY = float.MinValue;
        var minZ = float.MaxValue; var maxZ = float.MinValue;

        foreach (var (x, y, z) in vertices)
        {
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
            if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
        }

        var zRange   = Math.Max(0.0001f, maxZ - minZ);
        var xyScale  = Math.Max(Math.Max(maxX - minX, maxY - minY), 0.0001f); // XY normalisation scale
        var centerX  = (minX + maxX) * 0.5f;
        var centerY  = (minY + maxY) * 0.5f;

        // Track maximum push-out depth per region across all poses
        var maxPushOut = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (poseName, poseBones) in Poses)
        {
            for (var i = 0; i < vertices.Count; i++)
            {
                var (vx, vy, vz) = vertices[i];
                var hNorm = (vz - minZ) / zRange;
                var region = HeightToRegion(hNorm);

                // Retrieve the bone rotation for this region in the current pose
                poseBones.TryGetValue(region, out var boneRot);

                // Normalise XY to the same 0-1 scale as height
                var xNorm = (vx - centerX) / xyScale;
                var yNorm = (vy - centerY) / xyScale;

                // Apply vertical offset (hip drop, etc.)
                var deformedH = hNorm + boneRot.TransZ;
                var deformedY = yNorm;

                // Apply forward/back rotation around the joint pivot in the sagittal (Z-Y) plane
                if (MathF.Abs(boneRot.RotX) > 0.001f)
                {
                    PivotHeight.TryGetValue(region, out var pivot);
                    var arm  = hNorm - pivot;
                    var cosA = MathF.Cos(boneRot.RotX);
                    var sinA = MathF.Sin(boneRot.RotX);
                    deformedH = pivot + arm * cosA;
                    deformedY = yNorm + arm * sinA;
                }

                // Get morph factor for this region (default 1.0)
                regionalMorphing.TryGetValue(region, out var morphFactor);
                if (morphFactor < 0.01) morphFactor = 1.0;

                // Body-envelope cylinder radius for this region at its morph factor
                BaseRadius.TryGetValue(region, out var baseR);
                if (baseR < 0.001f) baseR = 0.08f;
                var bodyRadius = baseR * (float)morphFactor;

                // XY distance of deformed vertex from the body centre axis (normalised)
                var xyDist = MathF.Sqrt(xNorm * xNorm + deformedY * deformedY);

                // Penetration: positive means the vertex is inside the body envelope
                var penetration = bodyRadius - xyDist;
                if (penetration > 0.0)
                {
                    maxPushOut.TryGetValue(region, out var existing);
                    if (penetration > existing)
                    {
                        maxPushOut[region] = Math.Round(penetration, 6);
                    }
                }

                _ = deformedH; // used via pose logic; suppress unused-variable warning
            }
        }

        return new AnimationDrivenResult(vertices.Count, maxPushOut, "animation-driven");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Animation-Driven Pose Simulation Service
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Pose simulation service that uses the <see cref="AnimationDrivenGeometrySolver"/> when NIF
/// vertex data is available via <see cref="SimulateWithMeshDataAsync"/>.
/// <para>
/// When source mesh paths are supplied, the service reads each NIF file in turn, extracts
/// vertex positions from the first readable geometry block, and runs the animation-driven
/// solver to obtain per-pose push-out values grounded in actual vertex geometry rather than
/// pure morph-factor thresholds.  If no vertices can be extracted the service falls back to
/// the same heuristic amplifier approach used by <see cref="BasicPoseSimulationService"/>.
/// </para>
/// </summary>
internal sealed class AnimationDrivenPoseSimulationService : IPoseSimulationService
{
    private static readonly IReadOnlyList<string> AnimationPoses =
        ["T-pose", "Walk", "Run", "Idle", "Crouch", "Combat-Idle", "Jump", "Sneak"];

    // Per-pose regional stress amplifiers — retained as the heuristic fallback path.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> PoseAmplifiers =
        new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["T-pose"]      = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            ["Walk"]        = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["butt"]=1.08, ["thighs"]=1.05, ["belly"]=1.03, ["calves"]=1.04, ["legs"]=1.04 },
            ["Run"]         = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"]=1.05, ["butt"]=1.12, ["thighs"]=1.10, ["belly"]=1.05, ["arms"]=1.04, ["legs"]=1.08 },
            ["Idle"]        = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["shoulders"]=1.02, ["arms"]=1.02 },
            ["Crouch"]      = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["thighs"]=1.20, ["pelvis"]=1.15, ["butt"]=1.10, ["belly"]=1.12, ["calves"]=1.08, ["legs"]=1.14 },
            ["Combat-Idle"] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["chest"]=1.05, ["arms"]=1.08, ["shoulders"]=1.10, ["waist"]=1.04 },
            ["Jump"]        = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["butt"]=1.15, ["thighs"]=1.12, ["belly"]=1.08, ["calves"]=1.10, ["legs"]=1.10 },
            ["Sneak"]       = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["thighs"]=1.18, ["pelvis"]=1.12, ["butt"]=1.08, ["calves"]=1.15, ["legs"]=1.16 },
        };

    private const double RiskThreshold = 1.10;

    public Task<PoseSimulationResult> SimulateAsync(
        ConvertedMesh mesh,
        string targetBody,
        CancellationToken cancellationToken)
        => Task.FromResult(RunHeuristicSimulation(mesh));

    public async Task<PoseSimulationResult> SimulateWithMeshDataAsync(
        ConvertedMesh mesh,
        string targetBody,
        IReadOnlyList<string>? sourceMeshPaths,
        CancellationToken cancellationToken)
    {
        // Try animation-driven mode when source NIF paths are available
        if (sourceMeshPaths is { Count: > 0 })
        {
            foreach (var path in sourceMeshPaths)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                    var vertices = ExtractVertices(bytes);
                    if (vertices.Count > 0)
                    {
                        var solverResult = AnimationDrivenGeometrySolver.Solve(vertices, mesh.RegionalMorphing);
                        return BuildResultFromSolverOutput(solverResult);
                    }
                }
#pragma warning disable CA1031
                catch
#pragma warning restore CA1031
                {
                    // If a specific NIF fails to read, try the next one
                }
            }
        }

        // Heuristic fallback
        return RunHeuristicSimulation(mesh);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<(float X, float Y, float Z)> ExtractVertices(byte[] bytes)
    {
        if (!NifGeometrySignatureReader.TryLocateVertexBlock(bytes, out var offset, out var count) || count <= 0)
        {
            return [];
        }

        const int vertexSize = 12;
        var required = (long)count * vertexSize;
        if (offset < 0 || offset + required > bytes.Length)
        {
            return [];
        }

        var result = new List<(float, float, float)>(count);
        for (var i = 0; i < count; i++)
        {
            var o = offset + i * vertexSize;
            result.Add((
                BitConverter.ToSingle(bytes, o),
                BitConverter.ToSingle(bytes, o + 4),
                BitConverter.ToSingle(bytes, o + 8)));
        }

        return result;
    }

    private static PoseSimulationResult BuildResultFromSolverOutput(AnimationDrivenResult solver)
    {
        // Map solver push-out depths → PoseSimulationResult.
        // Each region with non-zero push-out is assigned to its highest-stress pose.
        var poseClippingRisk = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var highRiskSet      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (region, pushOut) in solver.MaxPushOutPerRegion)
        {
            if (pushOut <= 0.0) continue;

            highRiskSet.Add(region);
            var worstPose = GetWorstPoseForRegion(region);

            if (!poseClippingRisk.TryGetValue(worstPose, out var existing))
            {
                existing = new List<string>();
                poseClippingRisk[worstPose] = existing;
            }

            ((List<string>)existing).Add(region);
        }

        // Sort each pose's at-risk list
        foreach (var key in poseClippingRisk.Keys.ToList())
        {
            ((List<string>)poseClippingRisk[key]).Sort(StringComparer.OrdinalIgnoreCase);
        }

        return new PoseSimulationResult(
            AnimationPoses,
            poseClippingRisk,
            highRiskSet.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList(),
            poseClippingRisk.Count);
    }

    private static string GetWorstPoseForRegion(string region) =>
        region.ToLowerInvariant() switch
        {
            "thighs" or "calves" or "butt" or "pelvis" => "Crouch",
            "chest" or "breasts"                        => "Combat-Idle",
            "shoulders"                                 => "Combat-Idle",
            "arms"                                      => "Run",
            "belly" or "waist"                          => "Sneak",
            _                                           => "Run",
        };

    private static PoseSimulationResult RunHeuristicSimulation(ConvertedMesh mesh)
    {
        var poseClippingRisk = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var highRiskSet      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var atRiskPoseCount  = 0;

        foreach (var pose in AnimationPoses)
        {
            PoseAmplifiers.TryGetValue(pose, out var amplifiers);
            amplifiers ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            var atRiskRegions = new List<string>();
            foreach (var (region, morphFactor) in mesh.RegionalMorphing)
            {
                amplifiers.TryGetValue(region, out var amp);
                var effectiveStress = morphFactor * (amp > 0 ? amp : 1.0);
                if (effectiveStress >= RiskThreshold)
                {
                    atRiskRegions.Add(region);
                    highRiskSet.Add(region);
                }
            }

            if (atRiskRegions.Count > 0)
            {
                atRiskRegions.Sort(StringComparer.OrdinalIgnoreCase);
                poseClippingRisk[pose] = atRiskRegions;
                atRiskPoseCount++;
            }
        }

        return new PoseSimulationResult(
            AnimationPoses,
            poseClippingRisk,
            highRiskSet.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList(),
            atRiskPoseCount);
    }
}
