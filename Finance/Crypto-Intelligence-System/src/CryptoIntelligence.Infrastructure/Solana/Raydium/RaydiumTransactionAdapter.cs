using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CryptoIntelligence.Application.Ingestion;

namespace CryptoIntelligence.Infrastructure.Solana.Raydium;

public sealed class RaydiumTransactionAdapter : ISolanaTransactionAdapter
{
    private const string TokenProgramId =
        "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string Token2022ProgramId =
        "TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb";
    private const string LaunchLabProgramId =
        "LanMV9sAd7wArD4vJFi2qDdfnVhFxYSUg6eADduJ3uj";
    private const string CpmmProgramId =
        "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C";

    private readonly DiscriminatorRegistry _registry;

    public RaydiumTransactionAdapter(
        string parserVersion,
        params string[] idlJsonDocuments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parserVersion);
        ParserVersion = parserVersion;
        _registry = DiscriminatorRegistry.Load(idlJsonDocuments);
        ProgramIds = _registry.ProgramIds;
    }

    public string ParserVersion { get; }

    public IReadOnlySet<string> ProgramIds { get; }

    public static RaydiumTransactionAdapter CreatePinned(string parserVersion)
    {
        var assembly = typeof(RaydiumTransactionAdapter).Assembly;
        return new RaydiumTransactionAdapter(
            parserVersion,
            ReadResource(
                assembly,
                "CryptoIntelligence.Raydium.raydium_launchpad.json"),
            ReadResource(
                assembly,
                "CryptoIntelligence.Raydium.raydium_cp_swap.json"));
    }

    public AdapterParseResult Parse(string transactionJson)
    {
        using var document = JsonDocument.Parse(transactionJson);
        var response = document.RootElement;
        var transaction = response.TryGetProperty("result", out var result)
            ? result
            : response;
        if (transaction.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidDataException("Transaction response contains no result.");
        }

        var slot = checked((ulong)transaction.GetProperty("slot").GetInt64());
        var meta = transaction.GetProperty("meta");
        var failed = meta.GetProperty("err").ValueKind != JsonValueKind.Null;
        if (failed)
        {
            return new AdapterParseResult(slot, true, ParserVersion, []);
        }

        var message = transaction.GetProperty("transaction").GetProperty("message");
        var accountKeys = ReadAccountKeys(message, meta);
        var instructions = new List<DecodedInstruction>();
        var instructionEvents = new List<DecodedEvent>();

        var outerIndex = 0;
        foreach (var instruction in message.GetProperty("instructions").EnumerateArray())
        {
            DecodeInstruction(
                instruction,
                accountKeys,
                outerIndex,
                null,
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
                        parentIndex,
                        innerIndex,
                        instructions,
                        instructionEvents);
                    innerIndex++;
                }
            }
        }

        var logEvents = LocateProgramDataEvents(
            meta.GetProperty("logMessages")
                .EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty)
                .ToArray());
        var decoded = MergeEventRepresentations(instructionEvents, logEvents);
        var derived = EnrichDerivedEvents(
            DeriveInstructionEvents(instructions),
            decoded);
        var events = decoded
            .Where(value => value.Source == "TokenInstruction")
            .Concat(derived)
            .Select((value, index) => new ParsedAdapterEvent(
                value.ProgramId,
                value.Name,
                value.InstructionIndex,
                value.InnerInstructionIndex,
                index,
                value.DomainEventType,
                value.Source,
                value.PayloadFingerprint,
                value.Attributes))
            .ToArray();
        return new AdapterParseResult(slot, false, ParserVersion, events);
    }

    private static IReadOnlyList<string> ReadAccountKeys(
        JsonElement message,
        JsonElement meta)
    {
        var accountKeys = message.GetProperty("accountKeys")
            .EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : value.GetProperty("pubkey").GetString()!)
            .ToList();
        if (!meta.TryGetProperty("loadedAddresses", out var loadedAddresses) ||
            loadedAddresses.ValueKind != JsonValueKind.Object)
        {
            return accountKeys;
        }

        accountKeys.AddRange(
            loadedAddresses.GetProperty("writable").EnumerateArray()
                .Select(value => value.GetString()!));
        accountKeys.AddRange(
            loadedAddresses.GetProperty("readonly").EnumerateArray()
                .Select(value => value.GetString()!));
        return accountKeys;
    }

    private void DecodeInstruction(
        JsonElement instruction,
        IReadOnlyList<string> accountKeys,
        int outerInstructionIndex,
        int? innerInstructionIndex,
        ICollection<DecodedInstruction> instructions,
        ICollection<DecodedEvent> events)
    {
        var programIndex = instruction.GetProperty("programIdIndex").GetInt32();
        if (programIndex < 0 || programIndex >= accountKeys.Count)
        {
            throw new InvalidDataException(
                $"Program index {programIndex} is outside the account key list.");
        }

        var programId = accountKeys[programIndex];
        var data = Base58Decoder.Decode(
            instruction.GetProperty("data").GetString() ?? string.Empty);
        if (programId is TokenProgramId or Token2022ProgramId)
        {
            DecodeTokenInstruction(
                programId,
                data,
                outerInstructionIndex,
                innerInstructionIndex,
                events);
            return;
        }

        var name = _registry.FindInstruction(programId, data);
        if (name is not null)
        {
            instructions.Add(new DecodedInstruction(
                programId,
                name,
                outerInstructionIndex,
                innerInstructionIndex,
                BuildInstructionAttributes(
                    programId,
                    name,
                    instruction,
                    accountKeys,
                    data)));
            return;
        }

        var eventName = data.Length >= 16
            ? _registry.FindEvent(programId, data.AsSpan(8))
            : null;
        if (eventName is not null)
        {
            var eventData = data.AsSpan(8);
            events.Add(new DecodedEvent(
                programId,
                eventName,
                outerInstructionIndex,
                innerInstructionIndex,
                MapDomainEvent(eventName),
                "SelfCpi",
                Fingerprint(eventData),
                DecodeEventAttributes(programId, eventName, eventData)));
            return;
        }

        if (_registry.ProgramIds.Contains(programId) && data.Length >= 8)
        {
            throw new UnsupportedProgramVersionException(
                programId,
                Convert.ToHexString(data.AsSpan(0, 8)));
        }
    }

    private static void DecodeTokenInstruction(
        string programId,
        ReadOnlySpan<byte> data,
        int outerInstructionIndex,
        int? innerInstructionIndex,
        ICollection<DecodedEvent> events)
    {
        if (data.Length == 0)
        {
            return;
        }

        var eventType = data[0] switch
        {
            0 or 20 => "MintCreated",
            3 or 12 => "TokenTransferred",
            _ => null
        };
        if (eventType is null)
        {
            return;
        }

        events.Add(new DecodedEvent(
            programId,
            $"TokenInstruction{data[0]}",
            outerInstructionIndex,
            innerInstructionIndex,
            eventType,
            "TokenInstruction",
            $"instruction:{outerInstructionIndex}:{innerInstructionIndex}",
            null));
    }

    private IReadOnlyList<DecodedEvent> LocateProgramDataEvents(
        IReadOnlyList<string> logs)
    {
        var callStack = new Stack<string>();
        var result = new List<DecodedEvent>();
        foreach (var message in logs)
        {
            if (TryReadInvokedProgram(message, out var invoked))
            {
                callStack.Push(invoked);
                continue;
            }

            if (TryReadCompletedProgram(message, out var completed))
            {
                PopCompleted(callStack, completed);
                continue;
            }

            const string prefix = "Program data: ";
            if (callStack.Count == 0 ||
                !_registry.ProgramIds.Contains(callStack.Peek()) ||
                !message.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var data = Convert.FromBase64String(message[prefix.Length..]);
            var eventName = _registry.FindEvent(callStack.Peek(), data);
            if (eventName is not null)
            {
                result.Add(new DecodedEvent(
                    callStack.Peek(),
                    eventName,
                    -1,
                    null,
                    MapDomainEvent(eventName),
                    "ProgramData",
                    Fingerprint(data),
                    DecodeEventAttributes(
                        callStack.Peek(),
                        eventName,
                        data)));
            }
        }

        return result;
    }

    private static IReadOnlyList<DecodedEvent> MergeEventRepresentations(
        IReadOnlyList<DecodedEvent> selfCpiEvents,
        IReadOnlyList<DecodedEvent> logEvents)
    {
        var result = selfCpiEvents.ToList();
        var unmatched = selfCpiEvents
            .GroupBy(EventIdentity, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (var logEvent in logEvents)
        {
            var identity = EventIdentity(logEvent);
            if (unmatched.TryGetValue(identity, out var count) && count > 0)
            {
                unmatched[identity] = count - 1;
            }
            else
            {
                result.Add(logEvent);
            }
        }

        return result;
    }

    private static IReadOnlyList<DecodedEvent> DeriveInstructionEvents(
        IReadOnlyList<DecodedInstruction> instructions)
    {
        var result = new List<DecodedEvent>();
        foreach (var instruction in instructions)
        {
            if (instruction.ProgramId == LaunchLabProgramId)
            {
                switch (instruction.Name)
                {
                    case "initialize":
                        result.Add(Derived(instruction, "MintCreated"));
                        result.Add(Derived(instruction, "PoolCreated"));
                        break;
                    case "buy_exact_in":
                    case "sell_exact_in":
                        result.Add(Derived(instruction, "SwapObserved"));
                        break;
                    case "migrate_to_cpswap":
                        result.Add(Derived(instruction, "PoolCreated"));
                        result.Add(Derived(instruction, "LiquidityChanged"));
                        break;
                }
            }

            if (instruction.ProgramId == CpmmProgramId)
            {
                switch (instruction.Name)
                {
                    case "initialize":
                        result.Add(Derived(instruction, "PoolCreated"));
                        result.Add(Derived(instruction, "LiquidityChanged"));
                        break;
                    case "swap_base_input":
                        result.Add(Derived(instruction, "SwapObserved"));
                        break;
                    case "deposit":
                    case "withdraw":
                        result.Add(Derived(instruction, "LiquidityChanged"));
                        break;
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<DecodedEvent> EnrichDerivedEvents(
        IReadOnlyList<DecodedEvent> derived,
        IReadOnlyList<DecodedEvent> decoded)
    {
        var evidence = decoded
            .Where(value =>
                value.Attributes is not null &&
                value.Source is "ProgramData" or "SelfCpi")
            .GroupBy(value => (value.ProgramId, value.DomainEventType))
            .ToDictionary(
                group => group.Key,
                group => new Queue<DecodedEvent>(group));
        var result = new List<DecodedEvent>(derived.Count);
        foreach (var value in derived)
        {
            if (!evidence.TryGetValue(
                    (value.ProgramId, value.DomainEventType),
                    out var candidates) ||
                candidates.Count == 0)
            {
                result.Add(value);
                continue;
            }

            var eventEvidence = candidates.Dequeue();
            var attributes = value.Attributes is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(
                    value.Attributes,
                    StringComparer.Ordinal);
            foreach (var (key, attributeValue) in eventEvidence.Attributes!)
            {
                attributes[key] = attributeValue;
            }

            result.Add(value with { Attributes = attributes });
        }

        return result;
    }

    private static DecodedEvent Derived(
        DecodedInstruction instruction,
        string eventType) => new(
        instruction.ProgramId,
        instruction.Name,
        instruction.OuterInstructionIndex,
        instruction.InnerInstructionIndex,
        eventType,
        "InstructionDerived",
        $"instruction:{instruction.OuterInstructionIndex}:{instruction.InnerInstructionIndex}",
        instruction.Attributes);

    private static IReadOnlyDictionary<string, string> BuildInstructionAttributes(
        string programId,
        string name,
        JsonElement instruction,
        IReadOnlyList<string> accountKeys,
        ReadOnlySpan<byte> data)
    {
        var accounts = instruction.GetProperty("accounts")
            .EnumerateArray()
            .Select(value => accountKeys[value.GetInt32()])
            .ToArray();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (programId == LaunchLabProgramId)
        {
            switch (name)
            {
                case "initialize" when accounts.Length >= 8:
                    AddCore(values, accounts[5], accounts[6], accounts[7], accounts[1]);
                    break;
                case "buy_exact_in" when accounts.Length >= 11:
                    AddCore(values, accounts[4], accounts[9], accounts[10], accounts[0]);
                    values["side"] = "Buy";
                    values["quote_amount"] = U64(data, 8).ToString();
                    values["base_amount"] = U64(data, 16).ToString();
                    break;
                case "sell_exact_in" when accounts.Length >= 11:
                    AddCore(values, accounts[4], accounts[9], accounts[10], accounts[0]);
                    values["side"] = "Sell";
                    values["base_amount"] = U64(data, 8).ToString();
                    values["quote_amount"] = U64(data, 16).ToString();
                    break;
                case "migrate_to_cpswap" when accounts.Length >= 6:
                    AddCore(values, accounts[5], accounts[1], accounts[2], accounts[0]);
                    values["change_type"] = "Initialized";
                    break;
            }
        }

        if (programId == CpmmProgramId)
        {
            switch (name)
            {
                case "initialize" when accounts.Length >= 6:
                    AddCore(values, accounts[3], accounts[4], accounts[5], accounts[0]);
                    values["creator_address"] = accounts[0];
                    values["amm_config_address"] = accounts[1];
                    if (accounts.Length >= 17)
                    {
                        values["base_token_program_id"] = accounts[15];
                        values["quote_token_program_id"] = accounts[16];
                    }
                    values["base_reserve"] = U64(data, 8).ToString();
                    values["quote_reserve"] = U64(data, 16).ToString();
                    values["base_amount"] = values["base_reserve"];
                    values["quote_amount"] = values["quote_reserve"];
                    values["change_type"] = "Initialized";
                    break;
                case "swap_base_input" when accounts.Length >= 12:
                    AddCore(values, accounts[3], accounts[10], accounts[11], accounts[0]);
                    values["amm_config_address"] = accounts[2];
                    values["input_mint"] = accounts[10];
                    values["output_mint"] = accounts[11];
                    values["input_token_program_id"] = accounts[8];
                    values["output_token_program_id"] = accounts[9];
                    values["input_vault"] = accounts[6];
                    values["output_vault"] = accounts[7];
                    values["base_amount"] = U64(data, 8).ToString();
                    values["quote_amount"] = U64(data, 16).ToString();
                    values["side"] = "Unknown";
                    break;
                case "deposit" when accounts.Length >= 13:
                    AddCore(values, accounts[2], accounts[10], accounts[11], accounts[0]);
                    values["base_amount"] = U64(data, 16).ToString();
                    values["quote_amount"] = U64(data, 24).ToString();
                    values["change_type"] = "Added";
                    break;
                case "withdraw" when accounts.Length >= 13:
                    AddCore(values, accounts[2], accounts[10], accounts[11], accounts[0]);
                    values["base_amount"] = U64(data, 16).ToString();
                    values["quote_amount"] = U64(data, 24).ToString();
                    values["change_type"] = "Removed";
                    break;
            }
        }

        return values;
    }

    private static IReadOnlyDictionary<string, string>? DecodeEventAttributes(
        string programId,
        string eventName,
        ReadOnlySpan<byte> data)
    {
        if (programId != CpmmProgramId ||
            eventName != "SwapEvent" ||
            data.Length < 170)
        {
            return null;
        }

        var inputAmount = U64(data, 56);
        var tradeFee = U64(data, 153);
        var creatorFee = U64(data, 161);
        var creatorFeeOnInput = data[169] != 0;
        var tradingFeeBasisPoints = UniqueBasisPoints(inputAmount, tradeFee);
        var creatorFeeBasisPoints = creatorFeeOnInput
            ? UniqueBasisPoints(inputAmount, creatorFee)
            : null;
        var feeEvidenceSupported =
            data[88] != 0 &&
            creatorFeeOnInput &&
            tradingFeeBasisPoints.HasValue &&
            creatorFeeBasisPoints.HasValue;
        var result = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["input_vault_before"] = U64(data, 40).ToString(),
            ["output_vault_before"] = U64(data, 48).ToString(),
            ["input_amount"] = inputAmount.ToString(),
            ["output_amount"] = U64(data, 64).ToString(),
            ["input_transfer_fee"] = U64(data, 72).ToString(),
            ["output_transfer_fee"] = U64(data, 80).ToString(),
            ["trade_fee"] = tradeFee.ToString(),
            ["creator_fee"] = creatorFee.ToString(),
            ["base_input"] = (data[88] != 0).ToString(),
            ["creator_fee_on_input"] = creatorFeeOnInput.ToString(),
            ["fee_evidence_supported"] = feeEvidenceSupported.ToString()
        };
        if (tradingFeeBasisPoints.HasValue)
        {
            result["trading_fee_bps"] =
                tradingFeeBasisPoints.Value.ToString();
        }

        if (creatorFeeBasisPoints.HasValue)
        {
            result["creator_fee_bps"] =
                creatorFeeBasisPoints.Value.ToString();
        }

        return result;
    }

    private static int? UniqueBasisPoints(ulong amount, ulong fee)
    {
        if (amount == 0)
        {
            return null;
        }

        if (fee == 0)
        {
            return 0;
        }

        var amountValue = new BigInteger(amount);
        var feeValue = new BigInteger(fee);
        var lower =
            ((feeValue - BigInteger.One) * 10_000 / amountValue) +
            BigInteger.One;
        var upper = feeValue * 10_000 / amountValue;
        return lower == upper && lower >= 0 && lower < 10_000
            ? checked((int)lower)
            : null;
    }

    private static void AddCore(
        IDictionary<string, string> values,
        string pool,
        string baseMint,
        string quoteMint,
        string trader)
    {
        values["pool_address"] = pool;
        values["base_mint"] = baseMint;
        values["quote_mint"] = quoteMint;
        values["trader"] = trader;
    }

    private static ulong U64(ReadOnlySpan<byte> data, int offset) =>
        data.Length >= offset + sizeof(ulong)
            ? System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(
                data.Slice(offset, sizeof(ulong)))
            : 0;

    private static bool TryReadInvokedProgram(string message, out string programId)
    {
        const string prefix = "Program ";
        const string marker = " invoke [";
        var markerIndex = message.IndexOf(marker, StringComparison.Ordinal);
        if (message.StartsWith(prefix, StringComparison.Ordinal) && markerIndex > prefix.Length)
        {
            programId = message[prefix.Length..markerIndex];
            return true;
        }

        programId = string.Empty;
        return false;
    }

    private static bool TryReadCompletedProgram(string message, out string programId)
    {
        const string prefix = "Program ";
        if (!message.StartsWith(prefix, StringComparison.Ordinal))
        {
            programId = string.Empty;
            return false;
        }

        var successIndex = message.IndexOf(" success", StringComparison.Ordinal);
        var failedIndex = message.IndexOf(" failed: ", StringComparison.Ordinal);
        var end = successIndex >= 0 ? successIndex : failedIndex;
        if (end <= prefix.Length)
        {
            programId = string.Empty;
            return false;
        }

        programId = message[prefix.Length..end];
        return true;
    }

    private static void PopCompleted(Stack<string> stack, string completedProgram)
    {
        while (stack.Count > 0)
        {
            if (stack.Pop() == completedProgram)
            {
                return;
            }
        }
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

    private static string ReadResource(
        System.Reflection.Assembly assembly,
        string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded Raydium IDL '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record DecodedInstruction(
        string ProgramId,
        string Name,
        int OuterInstructionIndex,
        int? InnerInstructionIndex,
        IReadOnlyDictionary<string, string> Attributes);

    private sealed record DecodedEvent(
        string ProgramId,
        string Name,
        int InstructionIndex,
        int? InnerInstructionIndex,
        string DomainEventType,
        string Source,
        string PayloadFingerprint,
        IReadOnlyDictionary<string, string>? Attributes);
}

internal sealed class DiscriminatorRegistry
{
    private readonly Dictionary<string, ProgramDiscriminators> _programs =
        new(StringComparer.Ordinal);

    public IReadOnlySet<string> ProgramIds =>
        _programs.Keys.ToHashSet(StringComparer.Ordinal);

    public static DiscriminatorRegistry Load(IEnumerable<string> idlDocuments)
    {
        var registry = new DiscriminatorRegistry();
        foreach (var json in idlDocuments)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var programId = root.GetProperty("address").GetString()
                ?? throw new InvalidDataException("IDL has no program address.");
            registry._programs.Add(
                programId,
                new ProgramDiscriminators(
                    ReadDiscriminators(root, "instructions"),
                    ReadDiscriminators(root, "events")));
        }

        if (registry._programs.Count == 0)
        {
            throw new InvalidOperationException("At least one Raydium IDL is required.");
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
            var discriminator = item.GetProperty("discriminator")
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

internal static class Base58Decoder
{
    private const string Alphabet =
        "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static byte[] Decode(string value)
    {
        var number = BigInteger.Zero;
        foreach (var character in value)
        {
            var digit = Alphabet.IndexOf(character);
            if (digit < 0)
            {
                throw new FormatException($"Invalid Base58 character '{character}'.");
            }

            number = number * 58 + digit;
        }

        var bytes = number.ToByteArray(isUnsigned: true, isBigEndian: true);
        var leadingZeros = value.TakeWhile(character => character == '1').Count();
        var result = new byte[leadingZeros + bytes.Length];
        bytes.CopyTo(result, leadingZeros);
        return result;
    }
}
