using System.Reflection;
using System.Text.Json;

namespace Bodyslide.Core;

internal sealed record PhysicsBoneGroup(string Name, IReadOnlyList<string> Tokens);

internal static class PhysicsRepairCatalog
{
    private const string ResourceName = "Bodyslide.Core.Data.physics-repair.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly Lazy<IReadOnlyDictionary<string, PhysicsBoneGroup>> Groups = new(Load);

    public static IReadOnlyCollection<PhysicsBoneGroup> All => Groups.Value.Values.ToArray();

    public static bool TryMatchGroup(string boneName, out string groupName)
    {
        groupName = string.Empty;
        if (string.IsNullOrWhiteSpace(boneName))
        {
            return false;
        }

        var normalizedBone = NormalizeToken(boneName);
        var bestTokenLength = -1;
        foreach (var group in Groups.Value.Values)
        {
            var matchingTokenLength = group.Tokens
                .Where(token => normalizedBone.Contains(token, StringComparison.Ordinal))
                .Select(static token => token.Length)
                .DefaultIfEmpty(-1)
                .Max();
            if (matchingTokenLength > bestTokenLength)
            {
                groupName = group.Name;
                bestTokenLength = matchingTokenLength;
            }
        }

        return bestTokenLength >= 0;
    }

    public static IReadOnlySet<string> DetectGroups(IEnumerable<string>? bones)
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (bones is null)
        {
            return groups;
        }

        foreach (var bone in bones)
        {
            if (TryMatchGroup(bone, out var groupName))
            {
                groups.Add(groupName);
            }
        }

        return groups;
    }

    public static IReadOnlyList<string> RepairTargetBones(
        IEnumerable<string>? targetBones,
        IEnumerable<string>? supportedBones,
        IEnumerable<string>? sourceBones)
    {
        var repaired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (targetBones is not null)
        {
            repaired.UnionWith(targetBones.Where(static bone => !string.IsNullOrWhiteSpace(bone)));
        }

        var supported = (supportedBones ?? [])
            .Where(static bone => !string.IsNullOrWhiteSpace(bone))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var supportedGroups = new HashSet<string>(DetectGroups(supported), StringComparer.OrdinalIgnoreCase);
        var targetGroups = new HashSet<string>(DetectGroups(repaired), StringComparer.OrdinalIgnoreCase);

        if (sourceBones is not null)
        {
            foreach (var sourceBone in sourceBones.Where(static bone => !string.IsNullOrWhiteSpace(bone)))
            {
                if (TryMatchGroup(sourceBone, out var groupName) && supportedGroups.Contains(groupName))
                {
                    targetGroups.Add(groupName);
                }
            }
        }

        foreach (var supportedBone in supported)
        {
            if (TryMatchGroup(supportedBone, out var groupName) && targetGroups.Contains(groupName))
            {
                repaired.Add(supportedBone);
            }
        }

        return [.. repaired.Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static IReadOnlyDictionary<string, PhysicsBoneGroup> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded physics repair resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var raw = reader.ReadToEnd();
        var dto = JsonSerializer.Deserialize<PhysicsRepairCatalogDto>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Physics repair metadata could not be deserialized.");

        return (dto.Groups ?? [])
            .Select(Normalize)
            .ToDictionary(static group => group.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static PhysicsBoneGroup Normalize(PhysicsBoneGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new InvalidOperationException("Physics repair metadata contained a group without a name.");
        }

        return new PhysicsBoneGroup(
            dto.Name.Trim(),
            dto.Tokens?
                .Where(static token => !string.IsNullOrWhiteSpace(token))
                .Select(static token => NormalizeToken(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? []);
    }

    private static string NormalizeToken(string value)
    {
        const int MaxStackTokenLength = 256;
        Span<char> buffer = value.Length <= MaxStackTokenLength
            ? stackalloc char[value.Length]
            : new char[value.Length];
        var length = 0;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[length++] = char.ToLowerInvariant(ch);
            }
        }

        return new string(buffer[..length]);
    }

    private sealed class PhysicsRepairCatalogDto
    {
        public PhysicsBoneGroupDto[]? Groups { get; init; }
    }

    private sealed class PhysicsBoneGroupDto
    {
        public string? Name { get; init; }
        public string[]? Tokens { get; init; }
    }
}
