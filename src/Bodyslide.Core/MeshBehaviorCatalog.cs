using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record PhysicsSolverTuningProfile(
    double StiffnessMultiplier,
    double OffsetMultiplier,
    double DampingMultiplier,
    double GravityMultiplier,
    double MassMultiplier,
    double RestitutionMultiplier);

internal sealed record MeshBehaviorProfile(
    string MeshType,
    double ClippingThreshold,
    double BaseInflation,
    PhysicsSolverTuningProfile PhysicsSolver);

internal static class MeshBehaviorCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.mesh-behaviors.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly MeshBehaviorProfile FallbackProfile = new(
        MeshType: "default",
        ClippingThreshold: 1.10,
        BaseInflation: 0.040,
        PhysicsSolver: new PhysicsSolverTuningProfile(1.00, 1.00, 1.00, 1.00, 1.00, 1.00));

    private static readonly string[] FallbackMaleBodyTargets = ["HIMBO", "SAM", "SOS"];
    private static readonly Lazy<MeshBehaviorCatalogData> Data = new(Load);

    public static IReadOnlyCollection<string> MaleBodyTargets => Data.Value.MaleBodyTargets;

    public static MeshBehaviorProfile Get(string? meshType)
    {
        if (!string.IsNullOrWhiteSpace(meshType) && Data.Value.Profiles.TryGetValue(meshType.Trim(), out var profile))
        {
            return profile;
        }

        return Data.Value.DefaultProfile;
    }

    private static MeshBehaviorCatalogData Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded mesh behavior resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dto = JsonSerializer.Deserialize<MeshBehaviorCatalogDto>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Mesh behavior metadata could not be deserialized.");

        var defaultProfile = Normalize(dto.DefaultProfile, FallbackProfile.MeshType, FallbackProfile);
        var profiles = (dto.Profiles ?? [])
            .Select(profile => Normalize(profile, profile.MeshType, defaultProfile))
            .Where(static profile => !string.IsNullOrWhiteSpace(profile.MeshType))
            .ToDictionary(static profile => profile.MeshType, StringComparer.OrdinalIgnoreCase);

        var maleBodyTargets = (dto.MaleBodyTargets ?? FallbackMaleBodyTargets)
            .Where(static body => !string.IsNullOrWhiteSpace(body))
            .Select(static body => body.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MeshBehaviorCatalogData(
            defaultProfile,
            profiles,
            maleBodyTargets.Length > 0 ? maleBodyTargets : FallbackMaleBodyTargets);
    }

    private static MeshBehaviorProfile Normalize(MeshBehaviorProfileDto? dto, string? fallbackMeshType, MeshBehaviorProfile fallback)
    {
        var meshType = string.IsNullOrWhiteSpace(dto?.MeshType) ? fallbackMeshType ?? fallback.MeshType : dto.MeshType.Trim();
        var clippingThreshold = NormalizePositive(dto?.ClippingThreshold, fallback.ClippingThreshold);
        var baseInflation = NormalizePositive(dto?.BaseInflation, fallback.BaseInflation);
        var physicsSolver = Normalize(dto?.PhysicsSolver, fallback.PhysicsSolver);

        return new MeshBehaviorProfile(meshType, clippingThreshold, baseInflation, physicsSolver);
    }

    private static PhysicsSolverTuningProfile Normalize(PhysicsSolverTuningProfileDto? dto, PhysicsSolverTuningProfile fallback) =>
        new(
            NormalizePositive(dto?.StiffnessMultiplier, fallback.StiffnessMultiplier),
            NormalizePositive(dto?.OffsetMultiplier, fallback.OffsetMultiplier),
            NormalizePositive(dto?.DampingMultiplier, fallback.DampingMultiplier),
            NormalizePositive(dto?.GravityMultiplier, fallback.GravityMultiplier),
            NormalizePositive(dto?.MassMultiplier, fallback.MassMultiplier),
            NormalizePositive(dto?.RestitutionMultiplier, fallback.RestitutionMultiplier));

    private static double NormalizePositive(double? value, double fallback) =>
        value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value <= 0
            ? fallback
            : Math.Round(value.Value, 4);

    private sealed record MeshBehaviorCatalogData(
        MeshBehaviorProfile DefaultProfile,
        IReadOnlyDictionary<string, MeshBehaviorProfile> Profiles,
        IReadOnlyCollection<string> MaleBodyTargets);

    private sealed class MeshBehaviorCatalogDto
    {
        public MeshBehaviorProfileDto? DefaultProfile { get; init; }
        public MeshBehaviorProfileDto[]? Profiles { get; init; }
        public string[]? MaleBodyTargets { get; init; }
    }

    private sealed class MeshBehaviorProfileDto
    {
        public string? MeshType { get; init; }
        public double? ClippingThreshold { get; init; }
        public double? BaseInflation { get; init; }
        public PhysicsSolverTuningProfileDto? PhysicsSolver { get; init; }
    }

    private sealed class PhysicsSolverTuningProfileDto
    {
        public double? StiffnessMultiplier { get; init; }
        public double? OffsetMultiplier { get; init; }
        public double? DampingMultiplier { get; init; }
        public double? GravityMultiplier { get; init; }
        public double? MassMultiplier { get; init; }
        public double? RestitutionMultiplier { get; init; }
    }
}
