namespace Bodyslide.Core;

public sealed record RegressionFailureCell(
    string SourceBody,
    string TargetBody,
    string MeshType,
    string SupportTier,
    string IssueCode,
    int Count,
    IReadOnlyList<string> SampleIds,
    string SkeletonFamily = "unknown",
    string PhysicsMode = "unknown",
    string TopologyFamily = "unknown",
    string PluginFamily = "unknown");

public sealed record RegressionFailureMatrixReport(
    int SampleCount,
    int SamplesRequiringReview,
    IReadOnlyList<RegressionFailureCell> Cells);

public static class RegressionFailureMatrix
{
    public static RegressionFailureMatrixReport Build(
        string targetBody,
        IReadOnlyList<ArmorPackValidationItem> items,
        IReadOnlyList<ConversionMatrixPackProofItem>? proofItems = null)
    {
        var proofs = (proofItems ?? []).ToLookup(static item => item.OutputDirectory, StringComparer.OrdinalIgnoreCase);
        var failures = new List<(string Source, string Target, string Mesh, string Tier, string Code, string Sample,
            string Skeleton, string Physics, string Topology, string Plugin)>();
        var reviewCount = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var codes = new HashSet<string>(item.IssueCodes.Where(static code => !string.IsNullOrWhiteSpace(code)), StringComparer.OrdinalIgnoreCase);
            if (!item.Success)
            {
                codes.Add("conversion-failed");
            }
            if (!string.Equals(item.ValidationStatus, "ready", StringComparison.OrdinalIgnoreCase))
            {
                codes.Add($"validation-{Normalize(item.ValidationStatus)}");
            }
            if (item.ManualCleanupLikely)
            {
                codes.Add("manual-cleanup-required");
            }
            if (item.RuntimeVerificationRequired)
            {
                codes.Add("runtime-verification-required");
            }
            if (!string.IsNullOrWhiteSpace(item.SupportTier) &&
                !string.Equals(item.SupportTier, "mainstream-automatic", StringComparison.OrdinalIgnoreCase))
            {
                codes.Add("support-tier-review-required");
            }
            if (codes.Count == 0)
            {
                continue;
            }

            reviewCount++;
            var coordinates = proofs[item.OutputDirectory].FirstOrDefault()?.MatrixCoordinates ?? [];
            foreach (var code in codes)
            {
                failures.Add((
                    NormalizeBody(item.DetectedSourceBody),
                    NormalizeBody(targetBody),
                    Normalize(item.MeshType),
                    Normalize(item.SupportTier),
                    code.ToLowerInvariant(),
                    $"sample-{index + 1:D4}",
                    ReadCoordinate(coordinates, "source-skeleton-family:"),
                    ReadCoordinate(coordinates, "runtime-physics:"),
                    ReadCoordinate(coordinates, "topology-family:"),
                    ReadCoordinate(coordinates, "plugin-family:")));
            }
        }

        var cells = failures
            .GroupBy(static failure => (failure.Source, failure.Target, failure.Mesh, failure.Tier, failure.Code,
                failure.Skeleton, failure.Physics, failure.Topology, failure.Plugin))
            .Select(static group => new RegressionFailureCell(
                group.Key.Source, group.Key.Target, group.Key.Mesh, group.Key.Tier, group.Key.Code,
                group.Count(), group.Select(static failure => failure.Sample).ToArray(),
                group.Key.Skeleton, group.Key.Physics, group.Key.Topology, group.Key.Plugin))
            .OrderByDescending(static cell => cell.Count)
            .ThenBy(static cell => cell.SourceBody, StringComparer.Ordinal)
            .ThenBy(static cell => cell.TargetBody, StringComparer.Ordinal)
            .ThenBy(static cell => cell.MeshType, StringComparer.Ordinal)
            .ThenBy(static cell => cell.SupportTier, StringComparer.Ordinal)
            .ThenBy(static cell => cell.IssueCode, StringComparer.Ordinal)
            .ThenBy(static cell => cell.SkeletonFamily, StringComparer.Ordinal)
            .ThenBy(static cell => cell.PhysicsMode, StringComparer.Ordinal)
            .ThenBy(static cell => cell.TopologyFamily, StringComparer.Ordinal)
            .ThenBy(static cell => cell.PluginFamily, StringComparer.Ordinal)
            .ToArray();
        return new RegressionFailureMatrixReport(items.Count, reviewCount, cells);
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private static string ReadCoordinate(IReadOnlyList<string> coordinates, string prefix)
    {
        var coordinate = coordinates.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return coordinate is null ? "unknown" : Normalize(coordinate[prefix.Length..]);
    }

    private static string NormalizeBody(string? value) =>
        BuiltInBodyMetadataCatalog.TryGet(value ?? string.Empty, out var metadata)
            ? Normalize(metadata.Name)
            : Normalize(value);
}
