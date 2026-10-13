using Bodyslide.Core;

using System.Numerics;

namespace Bodyslide.Core.Tests;

/// <summary>
/// Opt-in check over real NIFs the user is authorized to use locally (never committed).
/// Set SLIDESMITH_LOCAL_CORPUS to a folder; the test is a no-op when unset or missing.
/// It asserts that every NIF is either read, or rejected with a diagnostic (fail-closed),
/// and that the reader never throws. Results are written to the test output.
/// </summary>
public sealed class LocalCorpusNifTests
{
    private const string EnvironmentVariable = "SLIDESMITH_LOCAL_CORPUS";

    [Fact]
    public void LocalCorpusNifsAreReadOrRejectedWithDiagnostics()
    {
        var root = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        var supported = 0;
        var rejected = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*.nif", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(path);
            SkyrimSseNifShapeReadResult result;
            try
            {
                result = SkyrimSseNifShapeReader.Read(bytes);
            }
            catch (InvalidDataException exception)
            {
                Assert.False(string.IsNullOrWhiteSpace(exception.Message), path);
                var key = exception.Message.Split(':')[0];
                rejected[key] = rejected.GetValueOrDefault(key) + 1;
                continue;
            }

            if (result.Supported)
            {
                supported++;
                Assert.NotEmpty(result.Shapes);
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic), path);
                rejected[result.Diagnostic.Split(':')[0]] = rejected.GetValueOrDefault(result.Diagnostic.Split(':')[0]) + 1;
            }
        }

        Console.WriteLine($"[local-corpus] supported={supported} rejected={string.Join(", ", rejected.Select(static p => $"{p.Key}={p.Value}"))}");
    }

    [Fact]
    public void LocalCorpusSkinnedOutfitNifsAreFullyReadWhenRequested()
    {
        var root = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ||
            Environment.GetEnvironmentVariable("SLIDESMITH_LOCAL_CORPUS_EXPECT_ALL") != "1")
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.nif", SearchOption.AllDirectories))
        {
            var result = SkyrimSseNifShapeReader.Read(File.ReadAllBytes(path));
            Assert.True(result.Supported, $"{path}: {result.Diagnostic}");
            Assert.All(result.Shapes, shape => Assert.NotEmpty(shape.TriangleIndices));
        }
    }

    [Fact]
    public void LocalCorpusTransformKeepsNifsReadableWithSameVertexCounts()
    {
        var root = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) ||
            Environment.GetEnvironmentVariable("SLIDESMITH_LOCAL_CORPUS_EXPECT_ALL") != "1")
        {
            return;
        }

        var changed = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*.nif", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(path);
            var before = SkyrimSseNifShapeReader.Read(bytes);
            var transformed = LocalExportService.TryApplyNifVertexTransform(
                bytes, path, new Dictionary<string, double> { ["chest"] = 0.05, ["hips"] = 0.05 }, null);
            var after = SkyrimSseNifShapeReader.Read(transformed);
            Assert.True(after.Supported, $"{path}: {after.Diagnostic}");
            Assert.Equal(before.Shapes.Select(static x => x.Vertices.Count), after.Shapes.Select(static x => x.Vertices.Count));
            Assert.Equal(bytes.Length, transformed.Length);
            if (!transformed.AsSpan().SequenceEqual(bytes))
            {
                changed++;
            }
        }

        Console.WriteLine($"[local-corpus] transform changed={changed}");
        Assert.True(changed > 0);
    }

    [Fact]
    public void LocalCorpusCbbeBodyTriMatchesNifAndProximityConformReproducesMorph()
    {
        var root = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        var nifPath = Path.Combine(root, "femalebody_0.nif");
        var triPath = Path.Combine(root, "femalebody.tri");
        if (!File.Exists(nifPath) || !File.Exists(triPath))
        {
            return;
        }

        var body = Assert.Single(SkyrimSseNifShapeReader.Read(File.ReadAllBytes(nifPath)).Shapes);
        Assert.True(TriMorphReader.TryRead(triPath, out var tri));
        var triShape = Assert.Single(tri!.Shapes);
        Assert.Equal(body.Vertices.Count, triShape.VertexCount);

        var morph = triShape.Morphs.OrderByDescending(m => MorphPayloadAnalysis.Analyze(m.Deltas).MaxMagnitude).First();
        var source = body.Vertices.Select(static v => new Vector3(v.X, v.Y, v.Z)).ToArray();
        var target = source.Select((p, i) => p + new Vector3(morph.Deltas[i].X, morph.Deltas[i].Y, morph.Deltas[i].Z)).ToArray();

        var result = ProximityConform.TryConform(source, source, target, 1.0f, 6);
        Assert.NotNull(result);
        var maxError = 0f;
        for (var i = 0; i < source.Length; i++)
        {
            maxError = MathF.Max(maxError, (result!.Vertices[i] - target[i]).Length());
        }

        Assert.True(maxError < 0.05f, $"max error {maxError}");
    }
}
