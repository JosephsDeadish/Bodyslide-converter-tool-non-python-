using System.Diagnostics;

namespace Bodyslide.Core;

internal sealed record OutputSizeCategory(string Category, int FileCount, long Bytes);
internal sealed record OutputSizeReport(
    bool Complete, int FileCount, long TotalBytes, long ScanMilliseconds,
    IReadOnlyList<OutputSizeCategory> Categories, IReadOnlyList<string> Warnings,
    string Scope = "Physical output files; archive bytes are additional to unpacked files. Inventory excludes itself.");

internal static class OutputSizeInventory
{
    internal static OutputSizeReport Create(
        string outputDirectory, string? archivePath, CancellationToken cancellationToken, int maximumEntries = 100_000)
    {
        var stopwatch = Stopwatch.StartNew();
        var categories = new Dictionary<string, (int Count, long Bytes)>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var pending = new Stack<string>();
        pending.Push(outputDirectory);
        var entries = 0;
        var complete = true;
        while (pending.Count > 0 && complete)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    Warn("Linked directories were excluded.");
                    continue;
                }
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++entries > maximumEntries)
                    {
                        Warn("Output inventory entry limit reached.");
                        break;
                    }
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        Warn("Linked files/directories were excluded.");
                        continue;
                    }
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    else if (!Path.GetFullPath(entry).Equals(Path.GetFullPath(Path.Combine(outputDirectory, "output-size-inventory.json")),
                                 OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                        Add(entry);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Warn("Some output metadata could not be read.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(archivePath) &&
            !Path.GetFullPath(archivePath).StartsWith(Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            try
            {
                if ((File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0)
                    Warn("Linked archives were excluded.");
                else Add(archivePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Warn("Archive metadata could not be read.");
            }
        }
        return new OutputSizeReport(complete, categories.Sum(pair => pair.Value.Count),
            categories.Sum(pair => pair.Value.Bytes), stopwatch.ElapsedMilliseconds,
            categories.OrderBy(pair => pair.Key).Select(pair =>
                new OutputSizeCategory(pair.Key, pair.Value.Count, pair.Value.Bytes)).ToArray(), warnings);

        void Warn(string message)
        {
            complete = false;
            if (!warnings.Contains(message)) warnings.Add(message);
        }

        void Add(string path)
        {
            var category = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".nif" => "meshes",
                ".dds" or ".png" or ".tga" or ".bmp" or ".jpg" or ".jpeg" => "textures",
                ".tri" or ".bsd" or ".osd" => "morphs",
                ".esp" or ".esm" or ".esl" => "plugins",
                ".zip" or ".7z" or ".rar" or ".bsa" or ".ba2" => "archives",
                ".json" or ".log" or ".html" or ".svg" or ".txt" => "reports",
                _ => "other"
            };
            var size = new FileInfo(path).Length;
            var current = categories.GetValueOrDefault(category);
            categories[category] = (current.Count + 1, current.Bytes + size);
        }
    }
}
