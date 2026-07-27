using System.Text.Json;

namespace RaydiumAdapterSpike;

internal sealed class IdlDiscriminatorRegistry
{
    private readonly Dictionary<string, ProgramDiscriminators> _programs =
        new(StringComparer.Ordinal);

    public IReadOnlySet<string> ProgramIds => _programs.Keys.ToHashSet(StringComparer.Ordinal);

    public static IdlDiscriminatorRegistry Load(params string[] idlPaths)
    {
        var registry = new IdlDiscriminatorRegistry();
        foreach (var path in idlPaths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var programId = root.GetProperty("address").GetString()
                ?? throw new InvalidDataException($"IDL '{path}' has no program address.");

            var instructions = ReadDiscriminators(root, "instructions");
            var events = ReadDiscriminators(root, "events");
            registry._programs.Add(programId, new ProgramDiscriminators(instructions, events));
        }

        return registry;
    }

    public string? FindInstruction(string programId, ReadOnlySpan<byte> data) =>
        Find(programId, data, static value => value.Instructions);

    public string? FindEvent(string programId, ReadOnlySpan<byte> data) =>
        Find(programId, data, static value => value.Events);

    private string? Find(
        string programId,
        ReadOnlySpan<byte> data,
        Func<ProgramDiscriminators, IReadOnlyDictionary<string, string>> selector)
    {
        if (data.Length < 8 || !_programs.TryGetValue(programId, out var program))
        {
            return null;
        }

        return selector(program).GetValueOrDefault(Convert.ToHexString(data[..8]));
    }

    private static IReadOnlyDictionary<string, string> ReadDiscriminators(
        JsonElement root,
        string propertyName)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in root.GetProperty(propertyName).EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()
                ?? throw new InvalidDataException($"IDL {propertyName} item has no name.");
            var discriminator = item
                .GetProperty("discriminator")
                .EnumerateArray()
                .Select(value => value.GetByte())
                .ToArray();

            result.Add(Convert.ToHexString(discriminator), name);
        }

        return result;
    }

    private sealed record ProgramDiscriminators(
        IReadOnlyDictionary<string, string> Instructions,
        IReadOnlyDictionary<string, string> Events);
}
