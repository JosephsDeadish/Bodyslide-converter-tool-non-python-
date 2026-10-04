using System.Text;
using Bodyslide.Core;

namespace Bodyslide.Core.Tests;

public sealed class BodySlideLinkedOwnershipTests
{
    [Fact]
    public async Task MultiProjectOspResolvesOnlyMatchingProjectsMorphPayloadsAcrossCacheReuse()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var meshes = Path.Combine(root, "meshes");
            var bodySlide = Path.Combine(root, "CalienteTools", "BodySlide");
            Directory.CreateDirectory(meshes);
            Directory.CreateDirectory(Path.Combine(bodySlide, "SliderSets"));
            var a = Path.Combine(meshes, "jacket_0.nif");
            var b = Path.Combine(meshes, "boots_0.nif");
            await File.WriteAllTextAsync(a, "mesh");
            await File.WriteAllTextAsync(b, "mesh");
            await File.WriteAllTextAsync(Path.Combine(bodySlide, "SliderSets", "pack.osp"), """
                <SliderSetInfo>
                  <SliderSet name="ProjectA"><OutputFile>jacket</OutputFile><DataFolder>AssetsA</DataFolder>
                    <Slider name="FitA"><DataFile>payload.osd</DataFile></Slider></SliderSet>
                  <SliderSet name="ProjectB"><OutputFile>boots</OutputFile><DataFolder>AssetsB</DataFolder>
                    <Slider name="FitB"><DataFile>payload.osd</DataFile></Slider></SliderSet>
                </SliderSetInfo>
                """);
            foreach (var (folder, morph) in new[] { ("AssetsA", "MorphA"), ("AssetsB", "MorphB") })
            {
                var directory = Path.Combine(bodySlide, "ShapeData", folder);
                Directory.CreateDirectory(directory);
                await File.WriteAllBytesAsync(Path.Combine(directory, "payload.osd"), Osd(morph));
            }
            foreach (var (mesh, own, other) in new[] { (a, "MorphA", "MorphB"), (b, "MorphB", "MorphA"), (a, "MorphA", "MorphB") })
            {
                var result = await BodySlideSourceProjectSupport.ResolveAsync(
                    new ImportedArmor(mesh, [mesh], [], [], []), "CBBE", CancellationToken.None);
                Assert.Contains(own, result.Sliders);
                Assert.DoesNotContain(other, result.Sliders);
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static byte[] Osd(string name)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("OSD\0"u8);
        writer.Write(1);
        writer.Write(1);
        var encoded = Encoding.UTF8.GetBytes(name);
        writer.Write((byte)encoded.Length);
        writer.Write(encoded);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write(0.25f);
        writer.Write(0f);
        writer.Write(0f);
        return stream.ToArray();
    }
}
