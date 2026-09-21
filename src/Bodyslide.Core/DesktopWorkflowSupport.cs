namespace Bodyslide.Core;

internal static class DesktopWorkflowSupport
{
    public static string? ReadOptionalSelection(string? selected) =>
        string.IsNullOrWhiteSpace(selected) || selected.Trim().Equals("(auto)", StringComparison.OrdinalIgnoreCase)
            ? null
            : selected.Trim();

    public static string? ReadOptionalPath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    public static IReadOnlyList<string> ParseDelimitedValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    public static IReadOnlyList<string> CombineSelections(string? primarySelection, IReadOnlyList<string> extraSelections)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(primarySelection))
        {
            values.Add(primarySelection.Trim());
        }

        values.AddRange(extraSelections.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value.Trim()));
        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? GetBestOutputDirectory(IReadOnlyList<ConversionResult> results)
    {
        if (results.Count == 0)
        {
            return null;
        }

        var first = results[0].OutputDirectory;
        if (results.All(r => string.Equals(r.OutputDirectory, first, StringComparison.OrdinalIgnoreCase)))
        {
            return first;
        }

        var commonRoot = FindCommonDirectory(results.Select(r => r.OutputDirectory));
        if (!string.IsNullOrWhiteSpace(commonRoot) && Directory.Exists(commonRoot))
        {
            return commonRoot;
        }

        return Directory.Exists(first) ? first : Path.GetDirectoryName(first);
    }

    public static string? GetFirstExistingOutputFile(IReadOnlyList<ConversionResult> results, params string[] fileNames)
    {
        if (fileNames.Length == 0)
        {
            return null;
        }

        return results
            .SelectMany(r => r.OutputFiles)
            .Where(File.Exists)
            .OrderBy(path => GetPreviewCandidateRank(Path.GetFileName(path), fileNames))
            .FirstOrDefault(path =>
                fileNames.Any(fileName => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase)));
    }

    public static int GetPreviewCandidateRank(string? fileName, IReadOnlyList<string> fileNames)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return int.MaxValue;
        }

        for (var index = 0; index < fileNames.Count; index++)
        {
            if (fileName.Equals(fileNames[index], StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    public static string? ResolvePreviewPath(string folder, IReadOnlyList<string> previewFileCandidates)
    {
        foreach (var candidate in previewFileCandidates)
        {
            var path = Path.Combine(folder, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static string? FindCommonDirectory(IEnumerable<string> directories)
    {
        var normalized = directories
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        if (normalized.Length == 0)
        {
            return null;
        }

        var candidate = normalized[0];
        while (!string.IsNullOrWhiteSpace(candidate))
        {
            var matchPrefix = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var allMatch = normalized.All(path =>
                string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(matchPrefix, StringComparison.OrdinalIgnoreCase));
            if (allMatch)
            {
                return candidate;
            }

            candidate = Path.GetDirectoryName(candidate);
        }

        return null;
    }
}
