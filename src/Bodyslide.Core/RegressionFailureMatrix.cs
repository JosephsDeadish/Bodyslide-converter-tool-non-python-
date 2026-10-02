namespace Bodyslide.Core;

public sealed record RegressionFailureCell(
    string SourceBody,
    string TargetBody,
    string MeshType,
    string SupportTier,
    string IssueCode,
    int Count,
    IReadOnlyList<string> SampleIds);

public sealed record RegressionFailureMatrixReport(
    int SampleCount,
    int SamplesRequiringReview,
    IReadOnlyList<RegressionFailureCell> Cells);

public static class RegressionFailureMatrix
{
    public static RegressionFailureMatrixReport Build(
        string targetBody,
        IReadOnlyList<ArmorPackValidationItem> items)
    {
        var failures = new List<(string Source, string Target, string Mesh, string Tier, string Code, string Sample)>();
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
            foreach (var code in codes)
            {
                failures.Add((
                    NormalizeBody(item.DetectedSourceBody),
                    NormalizeBody(targetBody),
                    Normalize(item.MeshType),
                    Normalize(item.SupportTier),
                    code.ToLowerInvariant(),
                    $"sample-{index + 1:D4}"));
            }
        }

        var cells = failures
            .GroupBy(static failure => (failure.Source, failure.Target, failure.Mesh, failure.Tier, failure.Code))
            .Select(static group => new RegressionFailureCell(
                group.Key.Source, group.Key.Target, group.Key.Mesh, group.Key.Tier, group.Key.Code,
                group.Count(), group.Select(static failure => failure.Sample).ToArray()))
            .OrderByDescending(static cell => cell.Count)
            .ThenBy(static cell => cell.SourceBody, StringComparer.Ordinal)
            .ThenBy(static cell => cell.TargetBody, StringComparer.Ordinal)
            .ThenBy(static cell => cell.MeshType, StringComparer.Ordinal)
            .ThenBy(static cell => cell.SupportTier, StringComparer.Ordinal)
            .ThenBy(static cell => cell.IssueCode, StringComparer.Ordinal)
            .ToArray();
        return new RegressionFailureMatrixReport(items.Count, reviewCount, cells);
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private static string NormalizeBody(string? value) =>
        BuiltInBodyMetadataCatalog.TryGet(value ?? string.Empty, out var metadata)
            ? Normalize(metadata.Name)
            : Normalize(value);
}
