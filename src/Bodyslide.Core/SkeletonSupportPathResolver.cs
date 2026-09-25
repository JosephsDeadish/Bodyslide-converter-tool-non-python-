namespace Bodyslide.Core;

public static class SkeletonSupportPathResolver
{
    private static readonly string[] PositiveSkeletonNameTokens =
    [
        "skeleton",
        "rig",
        "bone",
        "xpms",
        "xpmse"
    ];

    private static readonly string[] NegativeSkeletonNameTokens =
    [
        "body",
        "armor",
        "cuirass",
        "boots",
        "gauntlet",
        "glove",
        "helmet",
        "helm",
        "hood",
        "outfit",
        "dress",
        "shirt",
        "pants",
        "head",
        "hair",
        "hand",
        "foot"
    ];

    private static readonly string[] PreferredRelativeDirectories =
    [
        Path.Combine("meshes", "actors", "character", "character assets"),
        Path.Combine("meshes", "actors", "character", "character assets female"),
        Path.Combine("meshes", "actors", "character", "character assets male"),
    ];

    public static bool TryResolveSkeletonNifPath(string? supportPath, out string? skeletonNifPath)
    {
        skeletonNifPath = null;
        if (string.IsNullOrWhiteSpace(supportPath))
        {
            return false;
        }

        var normalizedPath = supportPath.Trim().Trim('"');
        if (File.Exists(normalizedPath))
        {
            var fullPath = Path.GetFullPath(normalizedPath);
            if (Path.GetExtension(fullPath).Equals(".nif", StringComparison.OrdinalIgnoreCase))
            {
                skeletonNifPath = fullPath;
                return true;
            }

            return TryResolveFromDirectory(Path.GetDirectoryName(fullPath), out skeletonNifPath);
        }

        if (Directory.Exists(normalizedPath))
        {
            return TryResolveFromDirectory(Path.GetFullPath(normalizedPath), out skeletonNifPath);
        }

        return false;
    }

    private static bool TryResolveFromDirectory(string? directoryPath, out string? skeletonNifPath)
    {
        skeletonNifPath = null;
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return false;
        }

        var originalRoot = Path.GetFullPath(directoryPath);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = originalRoot;
        for (var depth = 0; depth < 6 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            if (!visited.Add(current))
            {
                break;
            }

            if (TryResolvePreferredDirectories(current, out skeletonNifPath))
            {
                return true;
            }

            current = Path.GetDirectoryName(current);
        }

        return TryResolveFallbackWithinRoot(originalRoot, out skeletonNifPath);
    }

    private static bool TryResolvePreferredDirectories(string rootDirectory, out string? skeletonNifPath)
    {
        skeletonNifPath = null;
        foreach (var relativeDirectory in PreferredRelativeDirectories)
        {
            var candidateDirectory = Path.Combine(rootDirectory, relativeDirectory);
            if (!Directory.Exists(candidateDirectory))
            {
                continue;
            }

            var candidate = SelectBestCandidate(EnumerateSkeletonCandidates(candidateDirectory, SearchOption.TopDirectoryOnly));
            if (candidate is not null)
            {
                skeletonNifPath = Path.GetFullPath(candidate);
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveFallbackWithinRoot(string rootDirectory, out string? skeletonNifPath)
    {
        skeletonNifPath = null;

        var bestFallback = SelectBestCandidate(
            EnumerateFilesDepthFirst(rootDirectory, maxDepth: 5, "*.nif")
                .Where(IsHeuristicSkeletonCandidate));
        if (bestFallback is null)
        {
            return false;
        }

        skeletonNifPath = Path.GetFullPath(bestFallback);
        return true;
    }

    private static IEnumerable<string> EnumerateSkeletonCandidates(string directory, SearchOption searchOption)
    {
        foreach (var exact in Directory.EnumerateFiles(directory, "skeleton*.nif", searchOption))
        {
            yield return exact;
        }

        foreach (var heuristic in Directory.EnumerateFiles(directory, "*.nif", searchOption)
                     .Where(IsHeuristicSkeletonCandidate))
        {
            yield return heuristic;
        }
    }

    private static IEnumerable<string> EnumerateFilesDepthFirst(string rootDirectory, int maxDepth, string searchPattern)
    {
        if (!Directory.Exists(rootDirectory))
        {
            yield break;
        }

        var pending = new Stack<(string Directory, int Depth)>();
        pending.Push((rootDirectory, 0));

        while (pending.Count > 0)
        {
            var (directory, depth) = pending.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var child in children)
            {
                pending.Push((child, depth + 1));
            }
        }
    }

    private static string? SelectBestCandidate(IEnumerable<string> candidates)
    {
        return candidates
            .Select(static path => (Path: path, Rank: GetCandidateRank(path)))
            .OrderBy(static candidate => candidate.Rank.ExactSkeletonPenalty)
            .ThenBy(static candidate => candidate.Rank.HeuristicNamePenalty)
            .ThenBy(static candidate => candidate.Rank.NegativeTokenPenalty)
            .ThenBy(static candidate => candidate.Rank.PreferredDirectoryPenalty)
            .ThenBy(static candidate => candidate.Rank.GenderedVariantPenalty)
            .ThenBy(static candidate => candidate.Rank.PathDepth)
            .ThenBy(static candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(static candidate => candidate.Path)
            .FirstOrDefault();
    }

    private static SkeletonCandidateRank GetCandidateRank(string path)
    {
        var fileName = Path.GetFileName(path);
        var fileStem = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        var normalized = path.Replace('\\', '/');

        var exactSkeletonPenalty = fileName.Equals("skeleton.nif", StringComparison.OrdinalIgnoreCase)
            ? 0
            : fileStem.Contains("skeleton", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 2;

        var heuristicNamePenalty =
            (fileStem.Contains("xpms", StringComparison.OrdinalIgnoreCase) ||
             fileStem.Contains("xpmse", StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1) +
            (fileStem.Contains("rig", StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1) +
            (fileStem.Contains("bone", StringComparison.OrdinalIgnoreCase)
                ? 0
                : 1);

        var negativeTokenPenalty = NegativeSkeletonNameTokens.Any(token => fileStem.Contains(token, StringComparison.OrdinalIgnoreCase))
            ? 1
            : 0;

        var preferredDirectoryPenalty =
            normalized.Contains("/meshes/actors/character/character assets/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/meshes/actors/character/character assets female/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/meshes/actors/character/character assets male/", StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;

        var genderedVariantPenalty =
            normalized.Contains("/character assets female/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/character assets male/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/female/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/male/", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

        return new SkeletonCandidateRank(
            exactSkeletonPenalty,
            heuristicNamePenalty,
            negativeTokenPenalty,
            preferredDirectoryPenalty,
            genderedVariantPenalty,
            normalized.Count(static c => c == '/'));
    }

    private static bool IsHeuristicSkeletonCandidate(string path)
    {
        var fileStem = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileStem))
        {
            return false;
        }

        if (NegativeSkeletonNameTokens.Any(token => fileStem.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return PositiveSkeletonNameTokens.Any(token => fileStem.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct SkeletonCandidateRank(
        int ExactSkeletonPenalty,
        int HeuristicNamePenalty,
        int NegativeTokenPenalty,
        int PreferredDirectoryPenalty,
        int GenderedVariantPenalty,
        int PathDepth);
}
