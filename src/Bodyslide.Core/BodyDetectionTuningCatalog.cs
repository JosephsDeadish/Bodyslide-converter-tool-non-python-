using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record BodyDetectionTuning(
    double MeshTokenWeight,
    double TextureTokenWeight,
    double PhysicsTokenWeight,
    double PhysicsExpectationBoostValue,
    double BoneSignatureWeight,
    double VertexCountWeight,
    double BoundingRatioWeight,
    double UvSignatureWeight,
    double BodyReferenceTokenWeight,
    double BodyReferenceBoostValue,
    double AmbiguityMargin,
    double AmbiguityScoreFloor,
    double MediumConfidenceThreshold,
    double HighConfidenceThreshold,
    double ReferencePriorityMargin,
    double SharedReferenceConfidenceFloor);

internal static class BodyDetectionTuningCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.body-detection-tuning.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly BodyDetectionTuning Fallback = new(
        MeshTokenWeight: 0.35,
        TextureTokenWeight: 0.20,
        PhysicsTokenWeight: 0.10,
        PhysicsExpectationBoostValue: 0.10,
        BoneSignatureWeight: 0.10,
        VertexCountWeight: 0.15,
        BoundingRatioWeight: 0.05,
        UvSignatureWeight: 0.04,
        BodyReferenceTokenWeight: 0.08,
        BodyReferenceBoostValue: 0.22,
        AmbiguityMargin: 0.12,
        AmbiguityScoreFloor: 0.25,
        MediumConfidenceThreshold: 0.35,
        HighConfidenceThreshold: 0.75,
        ReferencePriorityMargin: 0.34,
        SharedReferenceConfidenceFloor: 0.50);

    private static readonly Lazy<BodyDetectionTuning> CurrentValue = new(Load);

    public static BodyDetectionTuning Current => CurrentValue.Value;

    private static BodyDetectionTuning Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded body detection tuning resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dto = JsonSerializer.Deserialize<BodyDetectionTuningDto>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Body detection tuning could not be deserialized.");

        return new BodyDetectionTuning(
            Normalize(dto.MeshTokenWeight, Fallback.MeshTokenWeight),
            Normalize(dto.TextureTokenWeight, Fallback.TextureTokenWeight),
            Normalize(dto.PhysicsTokenWeight, Fallback.PhysicsTokenWeight),
            Normalize(dto.PhysicsExpectationBoostValue, Fallback.PhysicsExpectationBoostValue),
            Normalize(dto.BoneSignatureWeight, Fallback.BoneSignatureWeight),
            Normalize(dto.VertexCountWeight, Fallback.VertexCountWeight),
            Normalize(dto.BoundingRatioWeight, Fallback.BoundingRatioWeight),
            Normalize(dto.UvSignatureWeight, Fallback.UvSignatureWeight),
            Normalize(dto.BodyReferenceTokenWeight, Fallback.BodyReferenceTokenWeight),
            Normalize(dto.BodyReferenceBoostValue, Fallback.BodyReferenceBoostValue),
            Normalize(dto.AmbiguityMargin, Fallback.AmbiguityMargin),
            Normalize(dto.AmbiguityScoreFloor, Fallback.AmbiguityScoreFloor),
            Normalize(dto.MediumConfidenceThreshold, Fallback.MediumConfidenceThreshold),
            Normalize(dto.HighConfidenceThreshold, Fallback.HighConfidenceThreshold),
            Normalize(dto.ReferencePriorityMargin, Fallback.ReferencePriorityMargin),
            Normalize(dto.SharedReferenceConfidenceFloor, Fallback.SharedReferenceConfidenceFloor));
    }

    private static double Normalize(double value, double fallback) =>
        double.IsNaN(value) || double.IsInfinity(value) || value < 0
            ? fallback
            : Math.Round(value, 4);

    private sealed class BodyDetectionTuningDto
    {
        public double MeshTokenWeight { get; init; }
        public double TextureTokenWeight { get; init; }
        public double PhysicsTokenWeight { get; init; }
        public double PhysicsExpectationBoostValue { get; init; }
        public double BoneSignatureWeight { get; init; }
        public double VertexCountWeight { get; init; }
        public double BoundingRatioWeight { get; init; }
        public double UvSignatureWeight { get; init; }
        public double BodyReferenceTokenWeight { get; init; }
        public double BodyReferenceBoostValue { get; init; }
        public double AmbiguityMargin { get; init; }
        public double AmbiguityScoreFloor { get; init; }
        public double MediumConfidenceThreshold { get; init; }
        public double HighConfidenceThreshold { get; init; }
        public double ReferencePriorityMargin { get; init; }
        public double SharedReferenceConfidenceFloor { get; init; }
    }
}
