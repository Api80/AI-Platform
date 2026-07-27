using System.Text.Json;
using System.Security.Cryptography;

namespace RaydiumAdapterSpike;

internal sealed record DecodedInstruction(
    string ProgramId,
    string Name,
    int OuterInstructionIndex,
    int? InnerInstructionIndex);

internal sealed record DecodedEvent(
    string ProgramId,
    string Name,
    int LogIndex,
    int EventOrdinal,
    string DomainEventType,
    string Source,
    string PayloadFingerprint);

internal sealed record ParsedTransactionFixture(
    long Slot,
    bool Failed,
    IReadOnlyList<DecodedInstruction> Instructions,
    IReadOnlyList<DecodedEvent> Events);

internal static class SolanaTransactionFixtureParser
{
    private const string TokenProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string Token2022ProgramId = "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb";

    public static ParsedTransactionFixture Parse(
        string json,
        IdlDiscriminatorRegistry registry)
    {
        using var document = JsonDocument.Parse(json);
        var response = document.RootElement;
        var transactionResult = response.TryGetProperty("result", out var result)
            ? result
            : response;

        if (transactionResult.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidDataException("Fixture contains no transaction result.");
        }

        var meta = transactionResult.GetProperty("meta");
        var message = transactionResult.GetProperty("transaction").GetProperty("message");
        var accountKeys = ReadAccountKeys(message, meta);
        var instructions = new List<DecodedInstruction>();
        var instructionEvents = new List<DecodedEvent>();

        var outerIndex = 0;
        foreach (var instruction in message.GetProperty("instructions").EnumerateArray())
        {
            DecodeInstruction(
                instruction,
                accountKeys,
                registry,
                outerIndex,
                innerInstructionIndex: null,
                instructions,
                instructionEvents);
            outerIndex++;
        }

        if (meta.TryGetProperty("innerInstructions", out var innerGroups) &&
            innerGroups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in innerGroups.EnumerateArray())
            {
                var parentIndex = group.GetProperty("index").GetInt32();
                var innerIndex = 0;
                foreach (var instruction in group.GetProperty("instructions").EnumerateArray())
                {
                    DecodeInstruction(
                        instruction,
                        accountKeys,
                        registry,
                        parentIndex,
                        innerIndex,
                        instructions,
                        instructionEvents);
                    innerIndex++;
                }
            }
        }

        var logs = meta
            .GetProperty("logMessages")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        var locatedEvents = SolanaLogEventLocator.Locate(
            logs,
            registry.ProgramIds);
        var decodedEvents = MergeEventRepresentations(
            instructionEvents,
            DecodeEvents(locatedEvents, registry));
        var events = decodedEvents
            .Concat(DeriveInstructionEvents(instructions, decodedEvents))
            .Select((value, index) => value with { EventOrdinal = index })
            .ToArray();

        return new ParsedTransactionFixture(
            transactionResult.GetProperty("slot").GetInt64(),
            meta.GetProperty("err").ValueKind != JsonValueKind.Null,
            instructions,
            events);
    }

    private static IReadOnlyList<string> ReadAccountKeys(JsonElement message, JsonElement meta)
    {
        var accountKeys = message
            .GetProperty("accountKeys")
            .EnumerateArray()
            .Select(value => value.GetString() ?? throw new InvalidDataException("Null account key."))
            .ToList();

        if (!meta.TryGetProperty("loadedAddresses", out var loadedAddresses) ||
            loadedAddresses.ValueKind != JsonValueKind.Object)
        {
            return accountKeys;
        }

        accountKeys.AddRange(
            loadedAddresses.GetProperty("writable").EnumerateArray()
                .Select(value => value.GetString() ?? throw new InvalidDataException("Null account key.")));
        accountKeys.AddRange(
            loadedAddresses.GetProperty("readonly").EnumerateArray()
                .Select(value => value.GetString() ?? throw new InvalidDataException("Null account key.")));
        return accountKeys;
    }

    private static void DecodeInstruction(
        JsonElement instruction,
        IReadOnlyList<string> accountKeys,
        IdlDiscriminatorRegistry registry,
        int outerInstructionIndex,
        int? innerInstructionIndex,
        ICollection<DecodedInstruction> output,
        ICollection<DecodedEvent> eventOutput)
    {
        var programIndex = instruction.GetProperty("programIdIndex").GetInt32();
        if (programIndex < 0 || programIndex >= accountKeys.Count)
        {
            throw new InvalidDataException($"Program index {programIndex} is outside account keys.");
        }

        var programId = accountKeys[programIndex];
        var data = Base58.Decode(instruction.GetProperty("data").GetString() ?? string.Empty);
        if (programId is TokenProgramId or Token2022ProgramId)
        {
            DecodeTokenInstruction(
                programId,
                data,
                outerInstructionIndex,
                innerInstructionIndex,
                eventOutput);
            return;
        }

        var name = registry.FindInstruction(programId, data);
        if (name is null)
        {
            // Anchor emit_cpi! encodes an internal __event instruction discriminator,
            // followed by the regular 8-byte event discriminator and event payload.
            var eventName = data.Length >= 16
                ? registry.FindEvent(programId, data.AsSpan(8))
                : null;
            if (eventName is not null)
            {
                eventOutput.Add(new DecodedEvent(
                    programId,
                    eventName,
                    LogIndex: -1,
                    eventOutput.Count,
                    MapDomainEvent(eventName),
                    "SelfCpi",
                    Fingerprint(data.AsSpan(8))));
                return;
            }

            if (registry.ProgramIds.Contains(programId) && data.Length >= 8)
            {
                throw new UnsupportedProgramVersionException(
                    programId,
                    Convert.ToHexString(data.AsSpan(0, 8)));
            }

            return;
        }

        output.Add(new DecodedInstruction(
            programId,
            name,
            outerInstructionIndex,
            innerInstructionIndex));
    }

    private static void DecodeTokenInstruction(
        string programId,
        ReadOnlySpan<byte> data,
        int outerInstructionIndex,
        int? innerInstructionIndex,
        ICollection<DecodedEvent> output)
    {
        if (data.Length == 0)
        {
            return;
        }

        var domainEventType = data[0] switch
        {
            0 or 20 => "MintCreated",
            3 or 12 => "TokenTransferred",
            _ => null
        };
        if (domainEventType is null)
        {
            return;
        }

        output.Add(new DecodedEvent(
            programId,
            $"TokenInstruction{data[0]}",
            LogIndex: -1,
            output.Count,
            domainEventType,
            "TokenInstruction",
            $"instruction:{outerInstructionIndex}:{innerInstructionIndex}"));
    }

    private static IReadOnlyList<DecodedEvent> DecodeEvents(
        IReadOnlyList<LocatedProgramEvent> locatedEvents,
        IdlDiscriminatorRegistry registry)
    {
        var result = new List<DecodedEvent>();
        foreach (var located in locatedEvents)
        {
            if (located.Kind == "ProgramData")
            {
                var data = Convert.FromBase64String(located.Value);
                var eventName = registry.FindEvent(located.ProgramId, data);
                if (eventName is not null)
                {
                    result.Add(new DecodedEvent(
                        located.ProgramId,
                        eventName,
                        located.LogIndex,
                        result.Count,
                        MapDomainEvent(eventName),
                        "ProgramData",
                        Fingerprint(data)));
                }

                continue;
            }

        }

        return result;
    }

    private static IReadOnlyList<DecodedEvent> MergeEventRepresentations(
        IReadOnlyList<DecodedEvent> selfCpiEvents,
        IReadOnlyList<DecodedEvent> logEvents)
    {
        var result = selfCpiEvents.ToList();
        var unmatchedSelfCpiCounts = selfCpiEvents
            .GroupBy(EventIdentity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (var logEvent in logEvents)
        {
            var identity = EventIdentity(logEvent);
            if (logEvent.Source == "ProgramData" &&
                unmatchedSelfCpiCounts.TryGetValue(identity, out var count) &&
                count > 0)
            {
                unmatchedSelfCpiCounts[identity] = count - 1;
                continue;
            }

            result.Add(logEvent);
        }

        return result;
    }

    private static IReadOnlyList<DecodedEvent> DeriveInstructionEvents(
        IReadOnlyList<DecodedInstruction> instructions,
        IReadOnlyList<DecodedEvent> decodedEvents)
    {
        const string cpmmProgramId = "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C";
        var result = new List<DecodedEvent>();
        var hasPoolCreated = decodedEvents.Any(value => value.DomainEventType == "PoolCreated");
        var hasLiquidityChanged = decodedEvents.Any(value => value.DomainEventType == "LiquidityChanged");

        foreach (var instruction in instructions)
        {
            if (instruction.ProgramId != cpmmProgramId || instruction.Name != "initialize")
            {
                continue;
            }

            if (!hasPoolCreated)
            {
                result.Add(new DecodedEvent(
                    instruction.ProgramId,
                    instruction.Name,
                    LogIndex: -1,
                    EventOrdinal: 0,
                    "PoolCreated",
                    "InstructionDerived",
                    $"instruction:{instruction.OuterInstructionIndex}:{instruction.InnerInstructionIndex}"));
                hasPoolCreated = true;
            }

            if (!hasLiquidityChanged)
            {
                result.Add(new DecodedEvent(
                    instruction.ProgramId,
                    instruction.Name,
                    LogIndex: -1,
                    EventOrdinal: 0,
                    "LiquidityChanged",
                    "InstructionDerived",
                    $"instruction:{instruction.OuterInstructionIndex}:{instruction.InnerInstructionIndex}"));
                hasLiquidityChanged = true;
            }
        }

        return result;
    }

    private static string EventIdentity(DecodedEvent value) =>
        $"{value.ProgramId}:{value.Name}:{value.PayloadFingerprint}";

    private static string Fingerprint(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(SHA256.HashData(data));

    private static string MapDomainEvent(string eventName) => eventName switch
    {
        "PoolCreateEvent" => "PoolCreated",
        "TradeEvent" => "SwapObserved",
        "SwapEvent" => "SwapObserved",
        "LpChangeEvent" => "LiquidityChanged",
        _ => "AdapterEvent"
    };
}

internal sealed class UnsupportedProgramVersionException(
    string programId,
    string discriminator)
    : Exception(
        $"Unsupported instruction discriminator '{discriminator}' for program '{programId}'.")
{
    public string ProgramId { get; } = programId;

    public string Discriminator { get; } = discriminator;
}
