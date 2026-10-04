using System.Xml.Linq;

namespace Bodyslide.Core;

internal sealed record ResolvedBodySlideSliders(
    IReadOnlyList<string> Sliders,
    IReadOnlyList<string> ZapSliders,
    string Gender,
    SourceMorphQualityMetrics? SourceMorphQuality = null,
    IReadOnlyDictionary<string, SourceMorphPayloadVariants>? ReusableMorphPayloads = null,
    SourceAssetSupportMetrics? SourceAssetSupport = null);

internal sealed record FallbackBodySlideInference(
    string? BodyName,
    string? DeformationProfile,
    IReadOnlyList<string> Signals,
    bool InferredFromPathEvidence = false);

internal static class BodySlideSourceProjectSupport
{
    private static readonly IReadOnlyList<string> DefaultSliders = ["Belly", "Butt", "BreastsShape", "WaistWidth", "HipWidth"];
    private static readonly StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;
    private static readonly SourceSliderCandidate EmptyCandidate = new(string.Empty, 0);
    private const int MaximumCachedProjects = 256;
    private const int MaximumCachedDirectoryEntries = 4096;
    private const int MaximumDiscoveryDirectories = 10000;
    private const int MaximumDiscoveryEntriesPerDirectory = 100000;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DirectorySnapshot> DirectoryCache = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private static readonly object DirectoryCacheLock = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BodySlideProjectProbeCacheEntry> OspProbeCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BodySlideLinkedAssetCacheEntry> LinkedAssetCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> ZapSliderHints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bra"] = "HideBra",
            ["panty"] = "HidePanties",
            ["panties"] = "HidePanties",
            ["underwear"] = "HidePanties",
            ["sleeve"] = "HideSleeves",
            ["cape"] = "HideCape",
            ["cloak"] = "HideCloak",
            ["hood"] = "HideHood",
            ["mask"] = "HideMask"
        };

    private static readonly string[] ZapNamePrefixes =
    [
        "hide", "remove", "toggle", "strip", "delete"
    ];

    private sealed record BodySlideDiscoveryResult(
        IReadOnlyList<string> Files,
        bool HasReferenceAssets);

    private sealed record DirectorySnapshot(
        string? Stamp,
        IReadOnlyList<string> Files,
        IReadOnlyList<string> Directories);

    private sealed record BodySlideProjectProbe(
        string OspPath,
        string OspDirectory,
        string? BodySlideRoot,
        IReadOnlyList<string> ProjectNames,
        IReadOnlyList<string> OutputPaths,
        IReadOnlyList<string> OutputFiles,
        IReadOnlyList<string> ReferencedPaths,
        IReadOnlyList<string>? DataFolders = null);

    public static async Task<ResolvedBodySlideSliders> ResolveAsync(
        ImportedArmor armor,
        string targetBody,
        CancellationToken cancellationToken)
    {
        var customProfileFound = CustomBodyProfileSupport.TryGetProfile(armor, targetBody, out var customProfile);
        var baseSliders = customProfileFound
            ? customProfile.SliderNames ?? DefaultSliders
            : BuiltInBodyMetadataCatalog.TryGet(targetBody, out var metadata) && metadata.SliderNames.Count > 0
                ? metadata.SliderNames
                : DefaultSliders;
        var sourceSupport = await ExtractSourceSupportAsync(armor, cancellationToken);
        var fallbackInference = sourceSupport.Sliders.Count == 0
            ? InferFallbackSupport(armor, targetBody)
            : null;
        var mergedSliders = MergeSliderLists(baseSliders, sourceSupport.Sliders);
        if (sourceSupport.Sliders.Count == 0 &&
            !string.IsNullOrWhiteSpace(fallbackInference?.BodyName) &&
            BuiltInBodyMetadataCatalog.TryGet(fallbackInference.BodyName, out var inferredMetadata) &&
            inferredMetadata.SliderNames.Count > 0)
        {
            mergedSliders = MergeSliderLists(mergedSliders, inferredMetadata.SliderNames);
        }

        var zapSliders = new HashSet<string>(customProfile?.ZapSliderNames ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var slider in sourceSupport.ZapSliders.Select(static candidate => candidate.Name))
        {
            zapSliders.Add(slider);
        }

        var sliderSet = mergedSliders.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var meshFile in armor.MeshFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(meshFile) ?? string.Empty;
            foreach (var (hint, zapName) in ZapSliderHints)
            {
                if (fileName.Contains(hint, StringComparison.OrdinalIgnoreCase) && !sliderSet.Contains(zapName))
                {
                    zapSliders.Add(zapName);
                }
            }
        }

        zapSliders.ExceptWith(sliderSet);

        var gender = customProfileFound && customProfile is not null
            ? string.Equals(customProfile.Gender, "male", StringComparison.OrdinalIgnoreCase) ? "male" : "female"
            : BuiltInBodyMetadataCatalog.TryGet(targetBody, out var builtInMetadata)
                ? builtInMetadata.Gender
                : "female";

        return new ResolvedBodySlideSliders(
            mergedSliders,
            zapSliders.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            gender,
            sourceSupport.SourceMorphQuality,
            sourceSupport.ReusableMorphPayloads,
            sourceSupport.BuildAssetSupport(
                baseSliders.Count > 0 && sourceSupport.Sliders.Count == 0,
                HasReferenceBodyAssets(armor.BodyReferenceFiles) || sourceSupport.HasReferenceAssets,
                fallbackInference));
    }

    public static FallbackBodySlideInference? InferFallbackSupport(
        ImportedArmor armor,
        string targetBody,
        VanillaArmorEntry? vanillaEntry = null)
    {
        var evidence = BuildFallbackEvidenceTokens(armor);
        if (evidence.Length == 0)
        {
            return vanillaEntry is null
                ? null
                : new FallbackBodySlideInference(null, vanillaEntry.RecommendedProfile, [$"vanilla:{vanillaEntry.RecommendedProfile}"]);
        }

        var inferredSourceBody = InferFallbackSourceBody(evidence, targetBody);
        var profileSignals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inferredProfile = InferDeformationProfile(evidence, vanillaEntry, profileSignals);
        if (inferredSourceBody is null && string.IsNullOrWhiteSpace(inferredProfile))
        {
            return null;
        }

        var signals = new HashSet<string>(inferredSourceBody?.Signals ?? [], StringComparer.OrdinalIgnoreCase);
        signals.UnionWith(profileSignals);
        var inferredFromPathEvidence = inferredSourceBody is not null &&
                                       signals.Any(IsPathEvidenceSignal);
        return new FallbackBodySlideInference(
            inferredSourceBody?.BodyName,
            inferredProfile,
            signals.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            inferredFromPathEvidence);
    }

    private static FallbackEvidenceToken[] BuildFallbackEvidenceTokens(ImportedArmor armor)
    {
        var evidence = new List<FallbackEvidenceToken>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddPathEvidenceTokens(armor.MeshFiles, "mesh");
        AddPathEvidenceTokens(armor.BodyReferenceFiles, "body-reference");
        AddPathEvidenceTokens(armor.PhysicsFiles, "physics");
        AddPathEvidenceTokens(armor.TextureFiles, "texture");
        return evidence.ToArray();

        void AddPathEvidenceTokens(IEnumerable<string> paths, string sourceCategory)
        {
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var fileStem = Path.GetFileNameWithoutExtension(path.Replace('\\', '/'));
                if (!string.IsNullOrWhiteSpace(fileStem) && !Guid.TryParse(fileStem, out _))
                {
                    AddEvidenceToken(sourceCategory, fileStem);
                }

                var pathSegments = path.Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(segment => !Guid.TryParse(Path.GetFileNameWithoutExtension(segment), out _))
                    .ToArray();
                foreach (var segment in pathSegments)
                {
                    if (segment.Length > 1)
                    {
                        AddEvidenceToken(sourceCategory, segment);
                    }
                }

                var condensedToken = new string(string.Join('/', pathSegments)
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray());
                if (condensedToken.Length > 4)
                {
                    AddEvidenceToken(sourceCategory, condensedToken);
                }
            }
        }

        void AddEvidenceToken(string sourceCategory, string token)
        {
            var normalizedToken = token.Trim();
            if (normalizedToken.Length == 0)
            {
                return;
            }

            var key = $"{sourceCategory}:{normalizedToken}";
            if (!seen.Add(key))
            {
                return;
            }

            evidence.Add(new FallbackEvidenceToken(normalizedToken, sourceCategory));
        }
    }

    private static async Task<BodySlideSourceSupport> ExtractSourceSupportAsync(
        ImportedArmor armor,
        CancellationToken cancellationToken)
    {
        var sliders = new List<SourceSliderCandidate>();
        var zapSliders = new List<SourceSliderCandidate>();
        var hasOsp = false;
        var hasTriPayloads = false;
        var hasBsdPayloads = false;
        var hasOsdPayloads = false;
        var discoveryStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var discovery = EnumerateAssociatedBodySlideFiles(armor, cancellationToken);
        discoveryStopwatch.Stop();

        foreach (var filePath in discovery.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(filePath);
            if (extension.Equals(".osp", StringComparison.OrdinalIgnoreCase))
            {
                var fromOsp = await TryReadOspAsync(filePath, cancellationToken);
                hasOsp |= fromOsp.HasOsp;
                sliders.AddRange(fromOsp.Sliders);
                zapSliders.AddRange(fromOsp.ZapSliders);
            }
            else if (extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadBsdSlider(filePath, out var candidate))
                {
                    hasBsdPayloads |= candidate.ReusablePayload is not null;
                    if (candidate.IsZap)
                    {
                        zapSliders.Add(candidate);
                    }
                    else
                    {
                        sliders.Add(candidate);
                    }
                }
            }
            else if (extension.Equals(".osd", StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadOsdSliders(filePath, out var osdCandidates))
                {
                    hasOsdPayloads |= osdCandidates.Any(static candidate => candidate.ReusablePayload is not null);
                    foreach (var candidate in osdCandidates)
                    {
                        if (candidate.IsZap)
                        {
                            zapSliders.Add(candidate);
                        }
                        else
                        {
                            sliders.Add(candidate);
                        }
                    }
                }
            }
            else if (extension.Equals(".tri", StringComparison.OrdinalIgnoreCase) &&
                     TryReadTriPayload(filePath, out var triPayload, out var resolvedTriVertexCount) &&
                     triPayload is not null)
            {
                foreach (var morph in triPayload.Morphs)
                {
                    var sliderName = NormalizeSliderFileName(morph.Name);
                    var deltas = ResizePayloadDeltas(morph.Deltas, resolvedTriVertexCount);
                    if (!TryCreatePayloadCandidate(
                        sliderName,
                        deltas,
                        SourcePriority.TriPayloadBase,
                        IsHighWeightVariant(morph.Name),
                        "tri",
                        out var candidate))
                    {
                        continue;
                    }

                    hasTriPayloads |= candidate.ReusablePayload is not null;
                    if (candidate.IsZap)
                    {
                        zapSliders.Add(candidate);
                    }
                    else
                    {
                        sliders.Add(candidate);
                    }
                }
            }
        }

        return new BodySlideSourceSupport(
            CollapseCandidates(sliders),
            CollapseCandidates(zapSliders),
            hasOsp,
            hasTriPayloads,
            hasBsdPayloads,
            hasOsdPayloads,
            discovery.HasReferenceAssets,
            discoveryStopwatch.ElapsedMilliseconds,
            discovery.Files.Count);
    }

    private static BodySlideDiscoveryResult EnumerateAssociatedBodySlideFiles(ImportedArmor armor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceRoot = ResolveSourceRoot(armor.SourcePath);
        var meshTokens = armor.MeshFiles
            .Select(NormalizeMeshToken)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var meshDirectories = armor.MeshFiles
            .Select(Path.GetDirectoryName)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var explicitFiles = armor.BodyReferenceFiles
            .Where(static path =>
                path.EndsWith(".osp", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".osd", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".bsd", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".tri", StringComparison.OrdinalIgnoreCase) ||
                IsLikelyBodySlideSupportXml(path));
        var discoveredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedOspFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasReferenceAssets = false;

        foreach (var location in EnumerateLikelyBodySlideRoots(sourceRoot, armor))
        {
            foreach (var path in EnumerateBodySlideSupportFiles(location.Root, location.SearchOption, meshTokens, cancellationToken))
            {
                discoveredFiles.Add(path);
            }

            foreach (var ospPath in EnumerateOspFiles(location.Root, location.SearchOption, visitedDirectories, cancellationToken))
            {
                if (!visitedOspFiles.Add(Path.GetFullPath(ospPath)) ||
                    !TryProbeOspProject(ospPath, out var probe) ||
                    !IsAssociatedWithArmor(probe, meshTokens, meshDirectories))
                {
                    continue;
                }

                discoveredFiles.Add(ospPath);
                foreach (var linkedAsset in ResolveLinkedProjectAssets(probe, cancellationToken))
                {
                    var extension = Path.GetExtension(linkedAsset);
                    if (extension.Equals(".nif", StringComparison.OrdinalIgnoreCase))
                    {
                        hasReferenceAssets = true;
                    }
                    else if (extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase) ||
                             extension.Equals(".osd", StringComparison.OrdinalIgnoreCase) ||
                             extension.Equals(".tri", StringComparison.OrdinalIgnoreCase) ||
                             extension.Equals(".osp", StringComparison.OrdinalIgnoreCase) ||
                             IsLikelyBodySlideSupportXml(linkedAsset))
                    {
                        discoveredFiles.Add(linkedAsset);
                    }
                }
            }
        }

        var files = explicitFiles
            .Concat(discoveredFiles)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new BodySlideDiscoveryResult(files, hasReferenceAssets);
    }

    internal static bool TryResolveTextureReferenceMeshes(
        IEnumerable<string> projectFiles, CancellationToken cancellationToken, out IReadOnlyList<string> meshes)
    {
        var resolvedMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        meshes = [];
        foreach (var projectFile in projectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (new FileInfo(projectFile).Length > 4 * 1024 * 1024 ||
                !TryProbeOspProject(projectFile, out var probe) ||
                (probe.DataFolders?.Count ?? 0) > 1)
            {
                return false;
            }
            var references = probe.ReferencedPaths.Where(static path =>
                Path.GetExtension(path).Equals(".nif", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (references.Length == 0)
            {
                return false;
            }
            foreach (var reference in references)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidates = ResolveLinkedPathCandidates(probe, reference).Where(File.Exists).ToArray();
                if (candidates.Length == 0)
                {
                    return false;
                }
                resolvedMeshes.UnionWith(candidates);
            }
        }
        meshes = resolvedMeshes.ToArray();
        return true;
    }

    private static bool HasReferenceBodyAssets(IReadOnlyList<string> bodyReferenceFiles) =>
        bodyReferenceFiles.Any(static path =>
            path.EndsWith(".nif", StringComparison.OrdinalIgnoreCase) && File.Exists(path));

    private static IEnumerable<SearchLocation> EnumerateLikelyBodySlideRoots(string sourceRoot, ImportedArmor armor)
    {
        var roots = new Dictionary<string, SearchOption>(StringComparer.OrdinalIgnoreCase);
        var existingDirectoryCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        static void AddRoot(IDictionary<string, SearchOption> map, string? root, SearchOption searchOption)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            if (map.TryGetValue(root, out var existing) && existing == SearchOption.AllDirectories)
            {
                return;
            }

            map[root] = searchOption;
        }

        bool DirectoryExistsCached(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            if (existingDirectoryCache.TryGetValue(directory, out var exists))
            {
                return exists;
            }

            exists = Directory.Exists(directory);
            existingDirectoryCache[directory] = exists;
            return exists;
        }

        if (Directory.Exists(sourceRoot))
        {
            foreach (var candidateRoot in EnumerateAncestorDirectories(sourceRoot))
            {
                var bodySlideRoot = Path.Combine(candidateRoot, "BodySlide");
                if (DirectoryExistsCached(bodySlideRoot))
                {
                    AddRoot(roots, bodySlideRoot, SearchOption.AllDirectories);
                }

                var calienteToolsBodySlideRoot = Path.Combine(candidateRoot, "CalienteTools", "BodySlide");
                if (DirectoryExistsCached(calienteToolsBodySlideRoot))
                {
                    AddRoot(roots, calienteToolsBodySlideRoot, SearchOption.AllDirectories);
                }
            }
        }

        foreach (var meshFile in armor.MeshFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(meshFile);
            if (DirectoryExistsCached(directory))
            {
                AddRoot(roots, directory, SearchOption.TopDirectoryOnly);
            }
        }

        foreach (var bodyReferenceFile in armor.BodyReferenceFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(bodyReferenceFile);
            if (DirectoryExistsCached(directory))
            {
                AddRoot(roots, directory, SearchOption.TopDirectoryOnly);
            }
        }

        return roots.Select(static pair => new SearchLocation(pair.Key, pair.Value));
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string directoryPath)
    {
        var current = new DirectoryInfo(directoryPath);
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private static IEnumerable<string> EnumerateBodySlideSupportFiles(string root, SearchOption searchOption, IReadOnlyList<string> meshTokens, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            return EnumerateSupportedFiles(root, cancellationToken)
                .Where(path => IsAssociatedWithArmor(path, meshTokens));
        }

        var discovered = new List<string>();
        var pending = new Stack<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            if (!visited.Add(Path.GetFullPath(current)))
            {
                continue;
            }
            if (visited.Count > MaximumDiscoveryDirectories)
            {
                throw new InvalidDataException($"BodySlide discovery exceeds {MaximumDiscoveryDirectories} directories; select a narrower input folder.");
            }
            discovered.AddRange(EnumerateSupportedFiles(current, cancellationToken)
                .Where(path => IsAssociatedWithArmor(path, meshTokens)));

            foreach (var directory in ReadDirectorySnapshot(current, cancellationToken).Directories)
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0 &&
                    ShouldTraverseBodySlideDirectory(root, directory, meshTokens))
                {
                    if (pending.Count + visited.Count >= MaximumDiscoveryDirectories)
                    {
                        throw new InvalidDataException($"BodySlide discovery exceeds {MaximumDiscoveryDirectories} directories; select a narrower input folder.");
                    }
                    pending.Push(directory);
                }
            }
        }

        return discovered;
    }

    private static IEnumerable<string> EnumerateSupportedFiles(string root, CancellationToken cancellationToken)
    {
        foreach (var path in ReadDirectorySnapshot(root, cancellationToken).Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(path);
            if (extension.Equals(".osp", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".osd", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".tri", StringComparison.OrdinalIgnoreCase) ||
                IsLikelyBodySlideSupportXml(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> EnumerateOspFiles(string root, SearchOption searchOption, HashSet<string> visitedDirectories, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            // A shallow lookup must not suppress a later recursive walk of the same root.
            if (searchOption == SearchOption.AllDirectories && !visitedDirectories.Add(Path.GetFullPath(current)))
            {
                continue;
            }
            if (visitedDirectories.Count > MaximumDiscoveryDirectories)
            {
                throw new InvalidDataException($"BodySlide discovery exceeds {MaximumDiscoveryDirectories} directories; select a narrower input folder.");
            }

            var snapshot = ReadDirectorySnapshot(current, cancellationToken);
            foreach (var file in snapshot.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Path.GetExtension(file).Equals(".osp", StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }

            if (searchOption == SearchOption.AllDirectories)
            {
                foreach (var directory in snapshot.Directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                    {
                        if (pending.Count + visitedDirectories.Count >= MaximumDiscoveryDirectories)
                        {
                            throw new InvalidDataException($"BodySlide discovery exceeds {MaximumDiscoveryDirectories} directories; select a narrower input folder.");
                        }
                        pending.Push(directory);
                    }
                }

            }
        }
    }

    private static DirectorySnapshot ReadDirectorySnapshot(string directory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(directory);
        static string? ReadStamp(string path)
        {
            var info = new DirectoryInfo(path);
            return info.Exists ? $"{info.CreationTimeUtc.Ticks}:{info.LastWriteTimeUtc.Ticks}" : null;
        }

        var stamp = ReadStamp(fullPath);
        if (stamp is not null && DirectoryCache.TryGetValue(fullPath, out var cached) && cached.Stamp == stamp)
        {
            return cached;
        }

        var files = new List<string>();
        var directories = new List<string>();
        var entryCount = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(fullPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++entryCount > MaximumDiscoveryEntriesPerDirectory)
            {
                throw new InvalidDataException($"BodySlide discovery exceeds {MaximumDiscoveryEntriesPerDirectory} entries in one directory; select a narrower input folder.");
            }
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                files.Add(entry);
            }
            else if ((attributes & FileAttributes.ReparsePoint) == 0)
            {
                directories.Add(entry);
            }
        }

        var snapshot = new DirectorySnapshot(stamp, files, directories);
        if (stamp is not null && stamp == ReadStamp(fullPath) &&
            entryCount <= MaximumCachedDirectoryEntries)
        {
            lock (DirectoryCacheLock)
            {
                if (DirectoryCache.Count >= MaximumCachedProjects && !DirectoryCache.ContainsKey(fullPath))
                {
                    DirectoryCache.Clear();
                }
                DirectoryCache[fullPath] = snapshot;
            }
        }
        return snapshot;
    }

    private static bool ShouldTraverseBodySlideDirectory(string searchRoot, string directoryPath, IReadOnlyList<string> meshTokens)
    {
        if (IsAssociatedWithArmor(directoryPath, meshTokens))
        {
            return true;
        }

        if (string.Equals(directoryPath, searchRoot, PathComparison))
        {
            return true;
        }

        var directoryName = Path.GetFileName(directoryPath);
        return directoryName.Equals("BodySlide", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("CalienteTools", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("ShapeData", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("SliderSets", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("SliderGroups", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("Presets", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("Project", StringComparison.OrdinalIgnoreCase) ||
               directoryName.Equals("Projects", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveSourceRoot(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return sourcePath;
        }

        return Path.GetDirectoryName(sourcePath) ?? sourcePath;
    }

    private static bool IsAssociatedWithArmor(string filePath, IReadOnlyList<string> meshTokens)
    {
        if (meshTokens.Count == 0)
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
        if (meshTokens.Any(token => fileName.Contains(token, PathComparison)))
        {
            return true;
        }

        var directoryPath = Path.GetDirectoryName(filePath) ?? string.Empty;
        return meshTokens.Any(token => directoryPath.Contains(token, PathComparison));
    }

    private static bool IsAssociatedWithArmor(
        BodySlideProjectProbe probe,
        IReadOnlyList<string> meshTokens,
        IReadOnlyList<string> meshDirectories)
    {
        var score = ScoreAssociation(probe.OspPath, meshTokens);
        score += probe.ProjectNames.Sum(projectName => ScoreAssociation(projectName, meshTokens));
        score += probe.ReferencedPaths.Sum(reference => ScoreAssociation(reference, meshTokens));
        score += probe.OutputFiles.Sum(outputFile => ScoreAssociation(outputFile, meshTokens));
        score += probe.OutputPaths.Sum(outputPath => ScoreAssociation(outputPath, meshDirectories));
        return score > 0;
    }

    private static int ScoreAssociation(string? value, IReadOnlyList<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(value) || tokens.Count == 0)
        {
            return 0;
        }

        var score = 0;
        foreach (var token in tokens)
        {
            if (!string.IsNullOrWhiteSpace(token) && value.Contains(token, PathComparison))
            {
                score++;
            }
        }

        return score;
    }

    private static IEnumerable<string> ResolveLinkedProjectAssets(BodySlideProjectProbe probe, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stamp = BuildLinkedAssetStamp(probe, cancellationToken);
        if (stamp is not null &&
            LinkedAssetCache.TryGetValue(probe.OspPath, out var cached) &&
            string.Equals(cached.Stamp, stamp, StringComparison.Ordinal))
        {
            return cached.Files;
        }

        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in probe.ReferencedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var resolved in ResolveLinkedPathCandidates(probe, reference))
            {
                if (File.Exists(resolved))
                {
                    discovered.Add(resolved);
                }
            }
        }

        foreach (var projectName in GetShapeDataFolderNames(probe))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(probe.BodySlideRoot))
            {
                continue;
            }

            var shapeDataFolder = Path.Combine(probe.BodySlideRoot, "ShapeData", projectName);
            if (!Directory.Exists(shapeDataFolder))
            {
                continue;
            }

            foreach (var asset in ReadDirectorySnapshot(shapeDataFolder, cancellationToken).Files.Where(static path =>
                         Path.GetExtension(path).Equals(".nif", StringComparison.OrdinalIgnoreCase) ||
                         Path.GetExtension(path).Equals(".osd", StringComparison.OrdinalIgnoreCase) ||
                         Path.GetExtension(path).Equals(".tri", StringComparison.OrdinalIgnoreCase) ||
                         Path.GetExtension(path).Equals(".bsd", StringComparison.OrdinalIgnoreCase) ||
                         IsLikelyBodySlideSupportXml(path)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                discovered.Add(asset);
            }
        }

        var result = discovered.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (stamp is not null)
        {
            if (LinkedAssetCache.Count >= MaximumCachedProjects)
            {
                LinkedAssetCache.Clear();
            }
            LinkedAssetCache[probe.OspPath] = new BodySlideLinkedAssetCacheEntry(stamp, result);
        }
        return result;
    }

    private static string? BuildLinkedAssetStamp(BodySlideProjectProbe probe, CancellationToken cancellationToken)
    {
        var stamps = new List<string?> { GetFileStamp(probe.OspPath), GetDirectoryStamp(probe.OspDirectory) };
        if (!string.IsNullOrWhiteSpace(probe.BodySlideRoot))
        {
            stamps.Add(GetDirectoryStamp(Path.Combine(probe.BodySlideRoot, "ShapeData")));
            foreach (var projectName in GetShapeDataFolderNames(probe))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var shapeDataFolder = Path.Combine(probe.BodySlideRoot, "ShapeData", projectName);
                stamps.Add(GetDirectoryStamp(shapeDataFolder));
                if (Directory.Exists(shapeDataFolder))
                {
                    foreach (var filePath in ReadDirectorySnapshot(shapeDataFolder, cancellationToken).Files.Order(StringComparer.OrdinalIgnoreCase))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        stamps.Add(GetFileStamp(filePath));
                    }
                }
            }
        }

        foreach (var reference in probe.ReferencedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var candidate in ResolveLinkedPathCandidates(probe, reference))
            {
                stamps.Add(GetFileStamp(candidate));
                stamps.Add(GetDirectoryStamp(Path.GetDirectoryName(candidate) ?? string.Empty));
            }
        }

        var filtered = stamps.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();
        return filtered.Length == 0 ? null : string.Join("|", filtered);
    }

    private static IEnumerable<string> ResolveLinkedPathCandidates(BodySlideProjectProbe probe, string referencedPath)
    {
        var normalized = referencedPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        if (probe.DataFolders is { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(probe.BodySlideRoot))
            {
                yield break;
            }
            var bodySlidePrefix = $"CalienteTools{Path.DirectorySeparatorChar}BodySlide{Path.DirectorySeparatorChar}";
            if (normalized.StartsWith(bodySlidePrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[bodySlidePrefix.Length..];
            }
            if (normalized.StartsWith($"ShapeData{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(probe.BodySlideRoot, normalized);
            }
            else
            {
                foreach (var dataFolder in probe.DataFolders)
                {
                    var folder = dataFolder.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                    yield return Path.Combine(probe.BodySlideRoot, "ShapeData", folder, normalized);
                }
            }
            yield break;
        }

        yield return Path.Combine(probe.OspDirectory, normalized);
        if (!string.IsNullOrWhiteSpace(probe.BodySlideRoot))
        {
            yield return Path.Combine(probe.BodySlideRoot, normalized);
            yield return Path.Combine(probe.BodySlideRoot, "ShapeData", normalized);

            var bodySlidePrefix = $"CalienteTools{Path.DirectorySeparatorChar}BodySlide{Path.DirectorySeparatorChar}";
            if (normalized.StartsWith(bodySlidePrefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(probe.BodySlideRoot, normalized[bodySlidePrefix.Length..]);
            }
        }
    }

    private static IReadOnlyList<string> GetShapeDataFolderNames(BodySlideProjectProbe probe) =>
        (probe.DataFolders is { Count: > 0 } ? probe.DataFolders : probe.ProjectNames)
            .Select(static folder => folder.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))
            .ToArray();

    private static bool TryProbeOspProject(string ospPath, out BodySlideProjectProbe probe)
    {
        var normalizedPath = Path.GetFullPath(ospPath);
        var stamp = GetFileStamp(normalizedPath);
        if (stamp is null)
        {
            probe = new BodySlideProjectProbe(normalizedPath, Path.GetDirectoryName(normalizedPath) ?? string.Empty, FindBodySlideRoot(Path.GetDirectoryName(normalizedPath)), [], [], [], []);
            return false;
        }

        if (OspProbeCache.TryGetValue(normalizedPath, out var cached) &&
            string.Equals(cached.Stamp, stamp, StringComparison.Ordinal))
        {
            if (cached.Success && cached.Probe is not null)
            {
                probe = cached.Probe;
                return true;
            }
            probe = new BodySlideProjectProbe(normalizedPath, Path.GetDirectoryName(normalizedPath) ?? string.Empty, FindBodySlideRoot(Path.GetDirectoryName(normalizedPath)), [], [], [], []);
            return false;
        }

        var fresh = ProbeOspProject(normalizedPath, stamp);
        if (OspProbeCache.Count >= MaximumCachedProjects)
        {
            OspProbeCache.Clear();
        }
        OspProbeCache[normalizedPath] = fresh;
        if (!fresh.Success || fresh.Probe is null)
        {
            probe = new BodySlideProjectProbe(normalizedPath, Path.GetDirectoryName(normalizedPath) ?? string.Empty, FindBodySlideRoot(Path.GetDirectoryName(normalizedPath)), [], [], [], []);
            return false;
        }

        probe = fresh.Probe;
        return true;
    }

    private static BodySlideProjectProbeCacheEntry ProbeOspProject(string ospPath, string stamp)
    {
        try
        {
            var document = XDocument.Load(ospPath, LoadOptions.None);
            var projectNames = document
                .Descendants("SliderSet")
                .Select(static element => element.Attribute("name")?.Value?.Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var outputPaths = document
                .Descendants()
                .Where(static element => element.Name.LocalName.Equals("OutputPath", StringComparison.OrdinalIgnoreCase))
                .Select(static element => element.Value.Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var outputFiles = document
                .Descendants()
                .Where(static element => element.Name.LocalName.Equals("OutputFile", StringComparison.OrdinalIgnoreCase))
                .Select(static element => element.Value.Trim())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var referencedPaths = document
                .Descendants()
                .SelectMany(static element => element.Attributes().Select(attr => attr.Value)
                    .Append(element.HasElements ? string.Empty : element.Value))
                .Select(static value => value.Trim())
                .Where(IsSupportedBodySlideReferencePath)
                .Select(static value => value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new BodySlideProjectProbeCacheEntry(
                stamp,
                true,
                new BodySlideProjectProbe(
                    ospPath,
                    Path.GetDirectoryName(ospPath) ?? string.Empty,
                    FindBodySlideRoot(Path.GetDirectoryName(ospPath)),
                    projectNames,
                    outputPaths,
                    outputFiles,
                    referencedPaths,
                    document.Descendants()
                        .Where(static element => element.Name.LocalName.Equals("DataFolder", StringComparison.OrdinalIgnoreCase))
                        .Select(static element => element.Value.Trim())
                        .Where(static value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return new BodySlideProjectProbeCacheEntry(stamp, false, null);
        }
    }

    private static bool IsSupportedBodySlideReferencePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var extension = Path.GetExtension(value);
        return extension.Equals(".nif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".osd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tri", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bsd", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsLikelyBodySlideSupportXml(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/');
        if (normalizedPath.Contains("/SliderGroups/", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Contains("/BodySlide/SliderGroups/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length <= 0 || stream.Length > 256 * 1024)
            {
                return false;
            }

            var buffer = new byte[Math.Min((int)stream.Length, 4096)];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                return false;
            }

            var snippet = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
            return snippet.Contains("<SliderGroups", StringComparison.OrdinalIgnoreCase) ||
                   snippet.Contains("<SliderSetInfo", StringComparison.OrdinalIgnoreCase) ||
                   snippet.Contains("<SliderSet", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? FindBodySlideRoot(string? startDirectory)
    {
        var current = string.IsNullOrWhiteSpace(startDirectory) ? null : new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (current.Name.Equals("BodySlide", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private sealed record BodySlideProjectProbeCacheEntry(string Stamp, bool Success, BodySlideProjectProbe? Probe);
    private sealed record BodySlideLinkedAssetCacheEntry(string Stamp, IReadOnlyList<string> Files);

    private static string? GetFileStamp(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? GetDirectoryStamp(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            var entryCount = 0;
            foreach (var _ in info.EnumerateFileSystemInfos())
            {
                entryCount++;
            }

            return $"{info.LastWriteTimeUtc.Ticks}:{entryCount}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<BodySlideSourceSupport> TryReadOspAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            var sliders = new List<SourceSliderCandidate>();
            var zapSliders = new List<SourceSliderCandidate>();

            foreach (var sliderElement in document.Descendants("Slider"))
            {
                var name = sliderElement.Attribute("name")?.Value?.Trim();
                if (!IsLikelySliderName(name))
                {
                    continue;
                }

                var isZap = IsTruthy(sliderElement.Attribute("zap")?.Value);
                if (isZap)
                {
                    zapSliders.Add(new SourceSliderCandidate(name!, SourcePriority.OspZap));
                }
                else
                {
                    sliders.Add(new SourceSliderCandidate(name!, SourcePriority.OspSlider));
                }
            }

            return new BodySlideSourceSupport(
                CollapseCandidates(sliders),
                CollapseCandidates(zapSliders),
                HasOsp: true,
                HasTriPayloads: false,
                HasBsdPayloads: false,
                HasOsdPayloads: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return new BodySlideSourceSupport([], [], HasOsp: false, HasTriPayloads: false, HasBsdPayloads: false, HasOsdPayloads: false);
        }
    }

    private static string NormalizeMeshToken(string meshFilePath)
    {
        var token = Path.GetFileNameWithoutExtension(meshFilePath) ?? meshFilePath;
        return token.EndsWith("_0", StringComparison.OrdinalIgnoreCase) || token.EndsWith("_1", StringComparison.OrdinalIgnoreCase)
            ? token[..^2]
            : token;
    }

    private static IReadOnlyList<string> MergeSliderLists(IReadOnlyList<string> primary, IReadOnlyList<SourceSliderCandidate> secondary)
    {
        var merged = new List<string>(primary.Count + secondary.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slider in primary)
        {
            if (!IsLikelySliderName(slider) || !seen.Add(slider))
            {
                continue;
            }

            merged.Add(slider);
        }

        foreach (var slider in secondary.Select(static candidate => candidate.Name))
        {
            if (!IsLikelySliderName(slider) || !seen.Add(slider))
            {
                continue;
            }

            merged.Add(slider);
        }

        return merged;
    }

    private static IReadOnlyList<string> MergeSliderLists(IReadOnlyList<string> primary, IReadOnlyList<string> secondary)
    {
        var merged = new List<string>(primary.Count + secondary.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slider in primary.Concat(secondary))
        {
            if (!IsLikelySliderName(slider) || !seen.Add(slider))
            {
                continue;
            }

            merged.Add(slider);
        }

        return merged;
    }

    private static InferredSourceBodySupport? InferFallbackSourceBody(IReadOnlyList<FallbackEvidenceToken> evidence, string targetBody)
    {
        var targetCanonicalBody = BuiltInBodyMetadataCatalog.TryResolveCanonicalName(targetBody, out var canonicalTargetBody)
            ? canonicalTargetBody
            : targetBody;

        var best = default(InferredSourceBodySupport?);
        foreach (var body in BuiltInBodyMetadataCatalog.All)
        {
            if (body.Name.Equals(targetCanonicalBody, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var signals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var score = 0;
            score += ScoreTokens(body.ReferenceTokens, "reference", signals);
            score += ScoreTokens(body.DetectionTokens, "detection", signals);
            score += ScoreTokens(body.TextureTokens, "texture", signals);
            score += ScoreTokens(body.Aliases, "alias", signals);

            if (score < 4 || signals.Count == 0)
            {
                continue;
            }

            var candidate = new InferredSourceBodySupport(body.Name, score, signals.Order(StringComparer.OrdinalIgnoreCase).ToArray());
            if (best is null || candidate.Score > best.Score)
            {
                best = candidate;
            }
        }

        return best;

        int ScoreTokens(IEnumerable<string> tokens, string category, ISet<string> signals)
        {
            var score = 0;
            foreach (var token in tokens.Where(static token => !string.IsNullOrWhiteSpace(token)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var evidenceToken in evidence)
                {
                    if (!evidenceToken.Value.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var categoryWeight = category switch
                    {
                        "reference" => 5,
                        "alias" => 4,
                        "detection" => 3,
                        "texture" => 2,
                        _ => 1
                    };
                    score += categoryWeight * GetSourceEvidenceWeight(evidenceToken.SourceCategory);
                    signals.Add($"{evidenceToken.SourceCategory}:{category}:{token}");
                    break;
                }
            }

            return score;
        }
    }

    private static string? InferDeformationProfile(
        IReadOnlyList<FallbackEvidenceToken> evidence,
        VanillaArmorEntry? vanillaEntry,
        ISet<string> signals)
    {
        var profileTokens = new (string Profile, string[] Tokens)[]
        {
            ("zeroed", ["zeroed"]),
            ("muscular", ["muscular", "muscle", "buff"]),
            ("athletic", ["athletic", "sport"]),
            ("curvy", ["curvy", "curves", "thicc"]),
            ("slim", ["slim", "slender", "thin"]),
            ("petite", ["petite", "smallframe"]),
            ("lean", ["lean"]),
            ("anime", ["anime"]),
            ("balanced", ["balanced", "vanilla"])
        };

        foreach (var (profile, tokens) in profileTokens)
        {
            foreach (var token in tokens)
            {
                if (evidence.Any(fileToken => fileToken.Value.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    signals.Add($"profile:{token}");
                    return profile;
                }
            }
        }

        if (vanillaEntry is not null)
        {
            signals.Add($"vanilla:{vanillaEntry.RecommendedProfile}");
            return vanillaEntry.RecommendedProfile;
        }

        return null;
    }

    private static bool IsPathEvidenceSignal(string signal) =>
        signal.StartsWith("mesh:", StringComparison.OrdinalIgnoreCase) ||
        signal.StartsWith("body-reference:", StringComparison.OrdinalIgnoreCase);

    private static int GetSourceEvidenceWeight(string sourceCategory) =>
        sourceCategory.ToLowerInvariant() switch
        {
            "body-reference" => 5,
            "mesh" => 4,
            "physics" => 2,
            "texture" => 1,
            _ => 1
        };

    private static string NormalizeSliderFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var normalized = fileName.Trim();
        if (normalized.EndsWith("_0", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("_1", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^2];
        }

        return normalized;
    }

    private static bool TryReadBsdSlider(string filePath, out SourceSliderCandidate candidate)
    {
        if (BsdMorphReader.TryRead(filePath, out var payload) && payload is not null)
        {
            return TryCreatePayloadCandidate(
                NormalizeSliderFileName(payload.SliderName),
                payload.Deltas,
                payload.IsHighWeight ? SourcePriority.BsdHighWeightBase : SourcePriority.BsdLowWeightBase,
                payload.IsHighWeight,
                "bsd",
                out candidate);
        }

        var sliderName = NormalizeSliderFileName(Path.GetFileNameWithoutExtension(filePath));
        if (IsLikelyZapSliderName(sliderName) && !string.IsNullOrWhiteSpace(sliderName))
        {
            candidate = new SourceSliderCandidate(sliderName, SourcePriority.UnreadablePayloadZapFallback, IsZap: true);
            return true;
        }

        candidate = EmptyCandidate;
        return false;
    }

    private static bool TryReadOsdSliders(string filePath, out IReadOnlyList<SourceSliderCandidate> candidates)
    {
        candidates = [];
        if (!OsdMorphReader.TryRead(filePath, out var payload) || payload is null || payload.Morphs.Count == 0)
        {
            return false;
        }

        var resolvedVertexCount = ResolveOsdPayloadVertexCount(filePath, payload.InferredVertexCount);
        var extracted = new List<SourceSliderCandidate>(payload.Morphs.Count);
        foreach (var morph in payload.Morphs)
        {
            var sliderName = NormalizeSliderFileName(morph.Name);
            var vertexCount = Math.Max(resolvedVertexCount, morph.SparseDeltas.Count == 0
                ? payload.InferredVertexCount
                : morph.SparseDeltas.Max(static delta => delta.Index + 1));
            if (vertexCount <= 0)
            {
                continue;
            }

            var deltas = new (float X, float Y, float Z)[vertexCount];
            foreach (var (index, x, y, z) in morph.SparseDeltas)
            {
                if (index < 0 || index >= deltas.Length)
                {
                    continue;
                }

                deltas[index] = (x, y, z);
            }

            if (TryCreatePayloadCandidate(
                sliderName,
                deltas,
                SourcePriority.OsdPayloadBase,
                IsHighWeightVariant(morph.Name),
                "osd",
                out var candidate))
            {
                extracted.Add(candidate);
            }
        }

        candidates = extracted;
        return extracted.Count > 0;
    }

    private static bool TryReadTriPayload(
        string filePath,
        out TriMorphPayload? payload,
        out int resolvedVertexCount)
    {
        payload = null;
        resolvedVertexCount = 0;
        if (!File.Exists(filePath))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (!TriMorphReader.TryRead(bytes, out payload) || payload is null)
        {
            return false;
        }

        resolvedVertexCount = IsPirtTriPayload(bytes)
            ? ResolveTriPayloadVertexCount(filePath, payload.VertexCount)
            : payload.VertexCount;
        return true;
    }

    private static bool IsPirtTriPayload(byte[] bytes) =>
        bytes.Length >= 4 &&
        bytes[0] == (byte)'P' &&
        bytes[1] == (byte)'I' &&
        bytes[2] == (byte)'R' &&
        bytes[3] == (byte)'T';

    private static IReadOnlyList<(float X, float Y, float Z)> ResizePayloadDeltas(
        IReadOnlyList<(float X, float Y, float Z)> deltas,
        int resolvedVertexCount)
    {
        if (resolvedVertexCount <= 0 || resolvedVertexCount == deltas.Count)
        {
            return deltas;
        }

        var resized = new (float X, float Y, float Z)[resolvedVertexCount];
        var copyCount = Math.Min(resolvedVertexCount, deltas.Count);
        for (var index = 0; index < copyCount; index++)
        {
            resized[index] = deltas[index];
        }

        return resized;
    }

    private static int ResolveTriPayloadVertexCount(string filePath, int inferredVertexCount)
    {
        var resolved = Math.Max(0, inferredVertexCount);
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return resolved;
        }

        foreach (var nifPath in Directory.EnumerateFiles(directory, "*.nif", SearchOption.TopDirectoryOnly))
        {
            var vertices = NifGeometrySignatureReader.TryReadFullVertices(nifPath);
            if (vertices is { Count: > 0 })
            {
                resolved = Math.Max(resolved, vertices.Count);
            }
        }

        return resolved;
    }

    private static int ResolveOsdPayloadVertexCount(string filePath, int inferredVertexCount)
    {
        var resolved = Math.Max(0, inferredVertexCount);
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return resolved;
        }

        foreach (var nifPath in Directory.EnumerateFiles(directory, "*.nif", SearchOption.TopDirectoryOnly))
        {
            var vertices = NifGeometrySignatureReader.TryReadFullVertices(nifPath);
            if (vertices is { Count: > 0 })
            {
                resolved = Math.Max(resolved, vertices.Count);
            }
        }

        return resolved;
    }

    private static bool TryCreatePayloadCandidate(
        string sliderName,
        IReadOnlyList<(float X, float Y, float Z)> deltas,
        int basePriority,
        bool isHighWeight,
        string payloadKind,
        out SourceSliderCandidate candidate)
    {
        var isZap = IsLikelyZapSliderName(sliderName);
        if (string.IsNullOrWhiteSpace(sliderName))
        {
            candidate = EmptyCandidate;
            return false;
        }

        var stats = MorphPayloadAnalysis.Analyze(deltas);
        if (!isZap && stats.MeaningfulCount == 0)
        {
            candidate = EmptyCandidate;
            return false;
        }

        candidate = new SourceSliderCandidate(
            sliderName,
            basePriority + ComputePayloadPriorityOffset(stats),
            isZap,
            stats,
            new SourceMorphPayload(sliderName, isHighWeight, payloadKind, deltas.Count, deltas));
        return true;
    }

    private static bool IsHighWeightVariant(string morphName) =>
        !string.IsNullOrWhiteSpace(morphName) &&
        morphName.Trim().EndsWith("_1", StringComparison.OrdinalIgnoreCase);

    private static int ComputePayloadPriorityOffset(MorphDeltaStats stats)
    {
        if (stats.TotalCount <= 0)
        {
            return 0;
        }

        var densityScore = (int)Math.Round(Math.Clamp(stats.MeaningfulRatio, 0f, 1f) * 100);
        var magnitudeScore = (int)Math.Round(Math.Clamp(stats.TotalMagnitude * 10f, 0f, 100f));
        var peakScore = (int)Math.Round(Math.Clamp(stats.MaxMagnitude * 80f, 0f, 80f));
        return densityScore + magnitudeScore + peakScore;
    }

    private static IReadOnlyList<SourceSliderCandidate> CollapseCandidates(IEnumerable<SourceSliderCandidate> candidates)
    {
        return candidates
            .Where(static candidate => IsLikelySliderName(candidate.Name))
            .GroupBy(
                static candidate => $"{candidate.Name}\u001f{candidate.IsZap}\u001f{candidate.ReusablePayload?.IsHighWeight ?? false}",
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderByDescending(static candidate => candidate.Priority)
                .ThenBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderByDescending(static candidate => candidate.Priority)
            .ThenBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsLikelyZapSliderName(string sliderName)
    {
        if (string.IsNullOrWhiteSpace(sliderName))
        {
            return false;
        }

        foreach (var prefix in ZapNamePrefixes)
        {
            if (sliderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            for (var i = 1; i < sliderName.Length; i++)
            {
                if (!IsTokenBoundary(sliderName, i))
                {
                    continue;
                }

                if (sliderName.AsSpan(i).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTokenBoundary(string value, int index)
    {
        var current = value[index];
        var previous = value[index - 1];
        return current is '_' or '-' or ' ' ||
               previous is '_' or '-' or ' ' ||
               char.IsUpper(current) && !char.IsUpper(previous);
    }

    private static bool IsTruthy(string? value) =>
        value is not null &&
        (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    private static bool IsLikelySliderName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return candidate.Length >= 2 && candidate.Any(char.IsLetter);
    }

    private static SourceMorphQualityMetrics? BuildSourceMorphQuality(IEnumerable<SourceSliderCandidate> sliderCandidates, IEnumerable<SourceSliderCandidate> zapCandidates)
    {
        var payloadStats = sliderCandidates
            .Concat(zapCandidates)
            .Select(static candidate => candidate.PayloadStats)
            .Where(static stats => stats is not null)
            .Select(static stats => stats!.Value)
            .ToArray();
        if (payloadStats.Length == 0)
        {
            return null;
        }

        var meaningfulPayloadMorphCount = payloadStats.Count(static stats => stats.MeaningfulCount > 0);
        var payloadStrengthScore = payloadStats
            .Select(ComputePayloadStrengthScore)
            .DefaultIfEmpty(0d)
            .Average();
        var payloadCoverageRatio = payloadStats.Length == 0
            ? 0d
            : (double)meaningfulPayloadMorphCount / payloadStats.Length;

        return new SourceMorphQualityMetrics(
            PayloadMorphCount: payloadStats.Length,
            MeaningfulPayloadMorphCount: meaningfulPayloadMorphCount,
            PayloadCoverageRatio: Math.Round(Math.Clamp(payloadCoverageRatio, 0d, 1d), 4),
            PayloadStrengthScore: Math.Round(Math.Clamp(payloadStrengthScore, 0d, 1d), 4));
    }

    private static double ComputePayloadStrengthScore(MorphDeltaStats stats)
    {
        var density = Math.Clamp(stats.MeaningfulRatio, 0f, 1f);
        var magnitude = Math.Clamp(stats.TotalMagnitude / 2f, 0f, 1f);
        var peak = Math.Clamp(stats.MaxMagnitude / 0.06f, 0f, 1f);
        return density * 0.5d + magnitude * 0.35d + peak * 0.15d;
    }

    private sealed record BodySlideSourceSupport(
        IReadOnlyList<SourceSliderCandidate> Sliders,
        IReadOnlyList<SourceSliderCandidate> ZapSliders,
        bool HasOsp,
        bool HasTriPayloads,
        bool HasBsdPayloads,
        bool HasOsdPayloads,
        bool HasReferenceAssets = false,
        long DiscoveryMilliseconds = 0,
        int DiscoveredFileCount = 0)
    {
        public SourceMorphQualityMetrics? SourceMorphQuality => BuildSourceMorphQuality(Sliders, ZapSliders);

        public IReadOnlyDictionary<string, SourceMorphPayloadVariants> ReusableMorphPayloads =>
            BuildReusableMorphPayloads(Sliders, ZapSliders);

        public SourceAssetSupportMetrics BuildAssetSupport(
            bool usedFallbackSliders,
            bool hasReferenceAssets,
            FallbackBodySlideInference? fallbackInference)
        {
            var missingAssets = new List<string>();
            if (!HasOsp)
            {
                missingAssets.Add("osp");
            }

            if (!HasTriPayloads && !HasBsdPayloads && !HasOsdPayloads)
            {
                missingAssets.Add("morph-payloads");
            }

            if (!hasReferenceAssets)
            {
                missingAssets.Add("reference-assets");
            }

            return new SourceAssetSupportMetrics(
                HasOsp,
                HasTriPayloads,
                HasBsdPayloads,
                hasReferenceAssets,
                usedFallbackSliders,
                missingAssets,
                ReusableMorphPayloads.Count,
                fallbackInference?.BodyName,
                fallbackInference?.Signals,
                fallbackInference?.DeformationProfile,
                HasOsdPayloads,
                fallbackInference?.InferredFromPathEvidence is true ? 1 : 0,
                DiscoveryMilliseconds,
                DiscoveredFileCount);
        }
    }

    private static IReadOnlyDictionary<string, SourceMorphPayloadVariants> BuildReusableMorphPayloads(
        IEnumerable<SourceSliderCandidate> sliders,
        IEnumerable<SourceSliderCandidate> zapSliders)
    {
        return sliders
            .Concat(zapSliders)
            .Where(static candidate => candidate.ReusablePayload is not null)
            .GroupBy(static candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group =>
                {
                    var lowWeight = group
                        .Where(static candidate => candidate.ReusablePayload?.IsHighWeight is false)
                        .OrderByDescending(static candidate => candidate.Priority)
                        .Select(static candidate => candidate.ReusablePayload)
                        .FirstOrDefault();
                    var highWeight = group
                        .Where(static candidate => candidate.ReusablePayload?.IsHighWeight is true)
                        .OrderByDescending(static candidate => candidate.Priority)
                        .Select(static candidate => candidate.ReusablePayload)
                        .FirstOrDefault();

                    return new SourceMorphPayloadVariants(lowWeight, highWeight);
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record SourceSliderCandidate(
        string Name,
        int Priority,
        bool IsZap = false,
        MorphDeltaStats? PayloadStats = null,
        SourceMorphPayload? ReusablePayload = null);
    private sealed record FallbackEvidenceToken(
        string Value,
        string SourceCategory);
    private sealed record InferredSourceBodySupport(string BodyName, int Score, IReadOnlyList<string> Signals);
    private sealed record SearchLocation(string Root, SearchOption SearchOption);

    private static class SourcePriority
    {
        public const int OspSlider = 100;
        public const int OspZap = 100;
        public const int TriPayloadBase = 220;
        public const int OsdPayloadBase = 240;
        public const int BsdLowWeightBase = 260;
        public const int BsdHighWeightBase = 300;
        public const int UnreadablePayloadZapFallback = 40;
    }
}
