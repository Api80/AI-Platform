using System.Numerics;
using RaydiumAdapterSpike;

const string launchLabProgramId = "LanMV9sAd7wArD4vJFi2qDdfnVhFxYSUg6eADduJ3uj";
const string tokenProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

if (args.Length == 0 || args[0] is "--self-test" or "self-test")
{
    RunSelfTests();
    return;
}

if (args[0] == "capture" && args.Length is 3 or 4)
{
    await FixtureCapture.CaptureTransactionsAsync(
        args[1],
        args[2],
        args.Length == 4 ? args[3] : "https://api.mainnet-beta.solana.com",
        CancellationToken.None);
    return;
}

if (args[0] == "capture-url" && args.Length == 3)
{
    await FixtureCapture.CaptureUrlAsync(args[1], args[2], CancellationToken.None);
    return;
}

if (args[0] == "probe-discovery" && args.Length is 4 or 5)
{
    var result = await SolanaDiscoveryProbe.RunAsync(
        args[1],
        args[2],
        args[3],
        TimeSpan.FromSeconds(args.Length == 5 ? int.Parse(args[4]) : 45),
        CancellationToken.None);
    Console.WriteLine(
        $"signature={result.Signature} slot={result.Slot} " +
        $"transactionAvailable={result.TransactionAvailable} " +
        $"confirmationStatus={result.ConfirmationStatus}");
    return;
}

if (args[0] == "verify-fixtures" && args.Length == 5)
{
    VerifyFixtures(args[1], args[2], args[3], args[4]);
    return;
}

if (args[0] == "quote" && args.Length is 5 or 6)
{
    var quote = CpmmQuoteCalculator.QuoteExactInput(
        BigInteger.Parse(args[1]),
        BigInteger.Parse(args[2]),
        BigInteger.Parse(args[3]),
        int.Parse(args[4]),
        args.Length == 6 ? int.Parse(args[5]) : 0);

    Console.WriteLine(
        $"amountOutRaw={quote.AmountOutRaw} tradingFeeRaw={quote.TradingFeeRaw} " +
        $"creatorFeeRaw={quote.CreatorFeeRaw} " +
        $"amountInAfterFeeRaw={quote.AmountInAfterFeeRaw} totalImpactBps={quote.TotalImpactBps}");
    return;
}

Console.Error.WriteLine(
    "Usage:\n" +
    "  dotnet run -- --self-test\n" +
    "  dotnet run -- capture <manifest> <raw-output-directory> [rpc-url]\n" +
    "  dotnet run -- capture-url <source-url> <output-path>\n" +
    "  dotnet run -- probe-discovery <websocket-url> <rpc-url> <program-id> [timeout-seconds]\n" +
    "  dotnet run -- verify-fixtures <manifest> <raw-directory> <launchlab-idl> <cpmm-idl>\n" +
    "  dotnet run -- quote <reserveInRaw> <reserveOutRaw> <amountInRaw> " +
    "<tradingFeeBps> [creatorFeeBps]");
Environment.ExitCode = 2;

static void RunSelfTests()
{
    var buy = CpmmQuoteCalculator.QuoteExactInput(
        BigInteger.Parse("12404532310903"),
        BigInteger.Parse("16137545623432"),
        BigInteger.Parse("100000000"),
        25,
        5);

    AssertEqual(BigInteger.Parse("250000"), buy.TradingFeeRaw, "buy fee");
    AssertEqual(BigInteger.Parse("50000"), buy.CreatorFeeRaw, "buy creator fee");
    AssertEqual(BigInteger.Parse("129702622"), buy.AmountOutRaw, "buy output");
    AssertEqual(30, buy.TotalImpactBps, "buy total impact");

    var sell = CpmmQuoteCalculator.QuoteExactInput(
        BigInteger.Parse("16137545623432"),
        BigInteger.Parse("12404532310903"),
        BigInteger.Parse("1000000000"),
        25,
        5);

    AssertEqual(BigInteger.Parse("2500000"), sell.TradingFeeRaw, "sell fee");
    AssertEqual(BigInteger.Parse("500000"), sell.CreatorFeeRaw, "sell creator fee");
    AssertEqual(BigInteger.Parse("766321904"), sell.AmountOutRaw, "sell output");
    AssertEqual(30, sell.TotalImpactBps, "sell total impact");

    var logs = new[]
    {
        $"Program {launchLabProgramId} invoke [1]",
        "Program log: Instruction: BuyExactIn",
        $"Program {tokenProgramId} invoke [2]",
        "Program log: Instruction: TransferChecked",
        $"Program {tokenProgramId} success",
        "Program data: AQID",
        $"Program {launchLabProgramId} success"
    };

    var supportedPrograms = new HashSet<string>(StringComparer.Ordinal)
    {
        launchLabProgramId
    };

    var firstPass = SolanaLogEventLocator.Locate(logs, supportedPrograms);
    var secondPass = SolanaLogEventLocator.Locate(logs, supportedPrograms);

    AssertEqual(2, firstPass.Count, "located event count");
    AssertEqual("Instruction", firstPass[0].Kind, "first event kind");
    AssertEqual("BuyExactIn", firstPass[0].Value, "instruction name");
    AssertEqual(0, firstPass[0].EventOrdinal, "instruction ordinal");
    AssertEqual("ProgramData", firstPass[1].Kind, "second event kind");
    AssertEqual(1, firstPass[1].EventOrdinal, "program data ordinal");
    AssertEqual(
        string.Join('|', firstPass.Select(Serialize)),
        string.Join('|', secondPass.Select(Serialize)),
        "repeat parse determinism");

    Console.WriteLine("Raydium adapter spike self-tests passed.");
}

static void VerifyFixtures(
    string manifestPath,
    string rawDirectory,
    string launchLabIdlPath,
    string cpmmIdlPath)
{
    var registry = IdlDiscriminatorRegistry.Load(launchLabIdlPath, cpmmIdlPath);
    var files = Directory.GetFiles(rawDirectory, "*.json").Order(StringComparer.Ordinal).ToArray();
    if (files.Length == 0)
    {
        throw new InvalidOperationException($"No fixture JSON files found in '{rawDirectory}'.");
    }

    foreach (var file in files)
    {
        var json = File.ReadAllText(file);
        var firstPass = SolanaTransactionFixtureParser.Parse(json, registry);
        var secondPass = SolanaTransactionFixtureParser.Parse(json, registry);
        var firstFingerprint = Fingerprint(firstPass);
        var secondFingerprint = Fingerprint(secondPass);
        AssertEqual(firstFingerprint, secondFingerprint, $"deterministic parse for {Path.GetFileName(file)}");

        Console.WriteLine(
            $"{Path.GetFileName(file)} slot={firstPass.Slot} failed={firstPass.Failed} " +
            $"instructions=[{string.Join(',', firstPass.Instructions.Select(value => value.Name))}] " +
            $"events=[{string.Join(',', firstPass.Events.Select(value => value.DomainEventType))}]");
    }

    FixtureManifestVerifier.Verify(manifestPath, rawDirectory, registry);
    Console.WriteLine($"Verified {files.Length} offline transaction fixtures.");
}

static string Fingerprint(ParsedTransactionFixture value) =>
    string.Join(
        '|',
        value.Instructions.Select(instruction =>
            $"I:{instruction.ProgramId}:{instruction.Name}:{instruction.OuterInstructionIndex}:{instruction.InnerInstructionIndex}"))
    + "||"
    + string.Join(
        '|',
        value.Events.Select(@event =>
            $"E:{@event.ProgramId}:{@event.Name}:{@event.LogIndex}:{@event.EventOrdinal}:" +
            $"{@event.DomainEventType}:{@event.Source}:{@event.PayloadFingerprint}"));

static string Serialize(LocatedProgramEvent value) =>
    $"{value.ProgramId}:{value.Kind}:{value.Value}:{value.LogIndex}:{value.EventOrdinal}";

static void AssertEqual<T>(T expected, T actual, string assertion)
    where T : IEquatable<T>
{
    if (!expected.Equals(actual))
    {
        throw new InvalidOperationException(
            $"Assertion '{assertion}' failed. Expected '{expected}', actual '{actual}'.");
    }
}
