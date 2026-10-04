using System.Text.Json;

namespace Bodyslide.Core;

internal readonly record struct ConversionQualityReportMetric(string Property, string? Value);

internal static class ConversionQualityReportMetrics
{
    public static IReadOnlyList<ConversionQualityReportMetric> Read(JsonElement root)
    {
        var metrics = new List<ConversionQualityReportMetric>
        {
            new("Source body", TryReadString(root, "DetectedSourceBody")),
            new("Target body", TryReadString(root, "TargetBody")),
            new("Mesh type", TryReadString(root, "MeshType")),
            new("Strategy", TryReadString(root, "Strategy")),
            new("Support tier", TryReadString(root, "SupportTier")),
            new("Target body support reliability", TryReadNestedString(root, "ConversionReadiness", "TargetBodySupportReliability")),
            new("Can convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanConvert"))),
            new("Can physics-convert", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanPhysicsConvert"))),
            new("Can safely animate", FormatBool(TryReadNestedBoolValue(root, "ConversionReadiness", "CanSafelyAnimate"))),
            new("Clipping detected", FormatBool(TryReadBoolValue(root, "ClippingDetected"))),
            new("Correction applied", FormatBool(TryReadBoolValue(root, "CorrectionApplied"))),
            new("Topology risk", FormatBool(TryReadBoolValue(root, "TopologyMismatchRisk"))),
            new("Validation status", TryReadNestedString(root, "ValidationSummary", "Status")),
            new("Validation score", TryReadNestedScalar(root, "ValidationSummary", "Score")),
            new("High-risk poses", TryReadInt(root, "HighRiskPoseCount")),
            new("Missing normals", TryReadInt(root, "MissingNormalCount")),
            new("Quality warnings", TryReadArray(root, "QualityWarnings")),
            new("Topology correspondence", TryReadNestedString(root, "TopologyCorrespondence", "Classification")),
            new("Topology correspondence confidence", TryReadNestedString(root, "TopologyCorrespondence", "Confidence")),
            new("Topology matching mode", TryReadNestedString(root, "TopologyCorrespondence", "MatchingMode")),
            new("True semantic correspondence", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "UsesTrueSemanticCorrespondence"))),
            new("Semantic vertex matching", TryReadNestedString(root, "TopologyCorrespondence", "SemanticVertexMatchingStatus")),
            new("Semantic anchor profile", TryReadNestedString(root, "TopologyCorrespondence", "SemanticAnchorProfile")),
            new("Semantic anchor coverage", TryReadNestedString(root, "TopologyCorrespondence", "SemanticAnchorCoverage")),
            new("Semantic anchor evidence", TryReadNestedArray(root, "TopologyCorrespondence", "SemanticAnchorEvidence")),
            new("Manual semantic review", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "RequiresManualSemanticReview"))),
            new("Heuristic-heavy topology", FormatBool(TryReadNestedBoolValue(root, "TopologyCorrespondence", "HeuristicHeavy"))),
            new("Topology focus regions", TryReadNestedArray(root, "TopologyCorrespondence", "FocusRegions")),
            new("Unmatched topology focus regions", TryReadNestedArray(root, "TopologyCorrespondence", "UnmatchedFocusRegions")),
            new("Topology signals", TryReadNestedArray(root, "TopologyCorrespondence", "Signals")),
            new("Topology recommendations", TryReadNestedArray(root, "TopologyCorrespondence", "Recommendations"))
        };
        metrics.AddRange(ReadTargetBodySupport(root));
        return metrics;
    }

    private static IEnumerable<ConversionQualityReportMetric> ReadTargetBodySupport(JsonElement root)
    {
        if (!TryGetProperty(root, "TargetBodySupport", out var targetBodySupport))
        {
            yield break;
        }

        yield return new("Has explicit support metadata", FormatBool(TryReadBoolValue(targetBodySupport, "HasExplicitSupportMetadata")));
        yield return new("Target skeleton framework", TryReadString(targetBodySupport, "SkeletonFramework"));
        yield return new("Target body support missing fields", TryReadArray(targetBodySupport, "MissingFields"));
        yield return new("Target body support quality warnings", TryReadArray(targetBodySupport, "QualityWarnings"));
        yield return new("Expected semantic regions", TryReadArray(targetBodySupport, "ExpectedSemanticRegions"));
        yield return new("Expected collision regions", TryReadArray(targetBodySupport, "ExpectedCollisionRegions"));
        yield return new("Expected bilateral regions", TryReadArray(targetBodySupport, "ExpectedBilateralRegions"));
        yield return new("Target physics slots", TryReadInt(targetBodySupport, "PhysicsSlotCount"));
        yield return new("Target chain depth", TryReadInt(targetBodySupport, "PhysicsChainDepth"));
        yield return new("Target physics families", TryReadInt(targetBodySupport, "PhysicsFamilyCount"));
        yield return new("Target runtime physics nodes", TryReadInt(targetBodySupport, "PhysicsNodeCount"));
    }

    private static string? TryReadString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? TryReadBoolValue(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    private static string? TryReadInt(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.TryGetInt32(out var result)
            ? result.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

    private static string? TryReadArray(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? JoinArray(value)
            : null;

    private static string? TryReadNestedString(JsonElement element, string propertyName, string nestedPropertyName) =>
        TryGetProperty(element, propertyName, out var nested)
            ? TryReadString(nested, nestedPropertyName)
            : null;

    private static bool? TryReadNestedBoolValue(JsonElement element, string propertyName, string nestedPropertyName) =>
        TryGetProperty(element, propertyName, out var nested)
            ? TryReadBoolValue(nested, nestedPropertyName)
            : null;

    private static string? TryReadNestedArray(JsonElement element, string propertyName, string nestedPropertyName) =>
        TryGetProperty(element, propertyName, out var nested)
            ? TryReadArray(nested, nestedPropertyName)
            : null;

    private static string? TryReadNestedScalar(JsonElement element, string propertyName, string nestedPropertyName) =>
        TryGetProperty(element, propertyName, out var nested) && TryGetProperty(nested, nestedPropertyName, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            }
            : null;

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? JoinArray(JsonElement array)
    {
        var values = array.EnumerateArray()
            .Select(static item => item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Number => item.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => item.GetRawText()
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        return values.Length == 0 ? null : string.Join(", ", values);
    }

    private static string? FormatBool(bool? value) =>
        value is null ? null : value.Value ? "Yes" : "No";
}
