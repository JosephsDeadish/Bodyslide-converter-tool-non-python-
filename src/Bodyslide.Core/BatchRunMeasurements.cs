using System.Diagnostics;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed class BatchRunMeasurements : IDisposable
{
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly Dictionary<string, long> phases = new(StringComparer.Ordinal);
    private readonly Process process = Process.GetCurrentProcess();
    private readonly object sampleGate = new();
    private readonly Timer sampler;
    private long sampledPeakWorkingSet;
    private readonly long initialAllocated = GC.GetTotalAllocatedBytes();
    internal List<string> OutputRoots { get; } = [];

    internal BatchRunMeasurements()
    {
        foreach (var phase in new[] { "extraction", "discovery", "conversion", "packaging", "cleanup" })
            phases[phase] = 0;
        SampleMemory();
        sampler = new Timer(_ => SampleMemory(), null, 250, 250);
    }

    internal IDisposable Measure(string phase) => new Phase(this, phase);

    private void SampleMemory()
    {
        try
        {
            lock (sampleGate)
            {
                process.Refresh();
                var value = process.WorkingSet64;
                long current;
                do { current = Interlocked.Read(ref sampledPeakWorkingSet); }
                while (value > current && Interlocked.CompareExchange(ref sampledPeakWorkingSet, value, current) != current);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    internal async Task WriteAsync(string directory, IReadOnlyList<string> outputRoots, string status)
    {
        sampler.Change(Timeout.Infinite, Timeout.Infinite);
        SampleMemory();
        elapsed.Stop();
        var allocatedBytes = Math.Max(0, GC.GetTotalAllocatedBytes() - initialAllocated);
        var bytes = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["meshes"] = 0, ["textures"] = 0, ["morphs"] = 0,
            ["plugins"] = 0, ["reports"] = 0, ["archives"] = 0, ["other"] = 0
        };
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var visited = new HashSet<string>(comparer);
        var measuredFiles = new HashSet<string>(comparer);
        var roots = outputRoots.Select(static root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)))
            .Distinct(comparer).ToArray();
        var pending = new Stack<string>(roots.Where(Directory.Exists));
        var entries = 0;
        var complete = true;
        var warnings = new List<string>();
        try
        {
            while (pending.Count > 0 && complete)
            {
                var root = pending.Pop();
                if (!visited.Add(root)) continue;
                foreach (var entry in Directory.EnumerateFileSystemEntries(root))
                {
                    if (++entries > 100_000) { complete = false; break; }
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if ((attributes & FileAttributes.Directory) != 0) { pending.Push(entry); continue; }
                    if (Path.GetFileName(entry).Equals("batch-performance.json", StringComparison.OrdinalIgnoreCase)) continue;
                    if (measuredFiles.Add(Path.GetFullPath(entry)))
                        bytes[Category(entry)] += new FileInfo(entry).Length;
                }
            }
            foreach (var root in roots)
            {
                if (File.Exists(root + ".zip") && measuredFiles.Add(root + ".zip"))
                    bytes["archives"] += new FileInfo(root + ".zip").Length;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            complete = false;
            warnings.Add($"Output inventory interrupted: {ex.Message}");
        }
        Directory.CreateDirectory(directory);
        var report = new
        {
            Status = status,
            TotalMilliseconds = elapsed.ElapsedMilliseconds,
            PhaseMilliseconds = phases,
            SampledPeakWorkingSetBytes = sampledPeakWorkingSet,
            MemorySampleIntervalMilliseconds = 250,
            MemoryScope = "Current process during this run; concurrent activity is included and short peaks may be missed.",
            AllocatedBytes = allocatedBytes,
            OutputBytesByCategory = bytes,
            OutputInventoryComplete = complete,
            OutputInventoryWarnings = warnings,
            OutputInventoryScope = "Existing output-root contents plus sibling ZIPs; not a source-size or newly-written-byte measurement.",
            TimingScope = "Discovery is the initial mesh scan; conversion includes item export and internal dependency scans (including single-item ZIPs). Batch packaging and archive cleanup are separate. Inventory/report writing excluded; this report is written after ZIP completion."
        };
        await File.WriteAllTextAsync(Path.Combine(directory, "batch-performance.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Category(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".nif" => "meshes",
        ".dds" or ".png" or ".tga" or ".jpg" or ".jpeg" => "textures",
        ".tri" or ".bsd" or ".osd" => "morphs",
        ".esp" or ".esm" or ".esl" => "plugins",
        ".zip" or ".7z" or ".rar" => "archives",
        ".json" or ".log" or ".html" or ".svg" => "reports",
        _ => "other"
    };

    public void Dispose()
    {
        using var finished = new ManualResetEvent(false);
        if (sampler.Dispose(finished)) finished.WaitOne();
        process.Dispose();
    }

    private sealed class Phase(BatchRunMeasurements owner, string name) : IDisposable
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        public void Dispose()
        {
            clock.Stop();
            owner.phases[name] = owner.phases.GetValueOrDefault(name) + clock.ElapsedMilliseconds;
        }
    }
}
