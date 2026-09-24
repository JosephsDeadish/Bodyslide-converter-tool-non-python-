namespace Bodyslide.Core;

public static class SkeletonSupportPathResolver
{
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

            var candidate = Directory
                .EnumerateFiles(candidateDirectory, "skeleton*.nif", SearchOption.TopDirectoryOnly)
                .OrderBy(path => GetCandidateScore(path))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
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

        var bestFallback = EnumerateFilesDepthFirst(rootDirectory, maxDepth: 5, "skeleton*.nif")
            .OrderBy(path => GetCandidateScore(path))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (bestFallback is null)
        {
            return false;
        }

        skeletonNifPath = Path.GetFullPath(bestFallback);
        return true;
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

    private static int GetCandidateScore(string path)
    {
        var score = 0;
        var fileName = Path.GetFileName(path);
        if (fileName.Equals("skeleton.nif", StringComparison.OrdinalIgnoreCase))
        {
            score -= 20;
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("/meshes/actors/character/character assets/", StringComparison.OrdinalIgnoreCase))
        {
            score -= 10;
        }

        if (normalized.Contains("/female/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/male/", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score + normalized.Count(static c => c == '/');
    }
}
