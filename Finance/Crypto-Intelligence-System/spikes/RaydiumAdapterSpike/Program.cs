using System.Numerics;
using RaydiumAdapterSpike;

const string launchLabProgramId = "LanMV9sAd7wArD4vJFi2qDdfnVhFxYSUg6eADduJ3uj";
const string tokenProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

if (args.Length == 0 || args[0] is "--self-test" or "self-test")
{
    RunSelfTests();
    return;
}

if (args[0] == "quote" && args.Length == 5)
{
    var quote = CpmmQuoteCalculator.QuoteExactInput(
        BigInteger.Parse(args[1]),
        BigInteger.Parse(args[2]),
        BigInteger.Parse(args[3]),
        int.Parse(args[4]));

    Console.WriteLine(
        $"amountOutRaw={quote.AmountOutRaw} feeRaw={quote.TradingFeeRaw} " +
        $"amountInAfterFeeRaw={quote.AmountInAfterFeeRaw} totalImpactBps={quote.TotalImpactBps}");
    return;
}

Console.Error.WriteLine(
    "Usage:\n" +
    "  dotnet run -- --self-test\n" +
    "  dotnet run -- quote <reserveInRaw> <reserveOutRaw> <amountInRaw> <feeBps>");
Environment.ExitCode = 2;

static void RunSelfTests()
{
    var buy = CpmmQuoteCalculator.QuoteExactInput(
        BigInteger.Parse("12404532310903"),
        BigInteger.Parse("16137545623432"),
        BigInteger.Parse("100000000"),
        25);

    AssertEqual(BigInteger.Parse("250000"), buy.TradingFeeRaw, "buy fee");
    AssertEqual(BigInteger.Parse("129767668"), buy.AmountOutRaw, "buy output");
    AssertEqual(25, buy.TotalImpactBps, "buy total impact");

    var sell = CpmmQuoteCalculator.QuoteExactInput(
        BigInteger.Parse("16137545623432"),
        BigInteger.Parse("12404532310903"),
        BigInteger.Parse("1000000000"),
        25);

    AssertEqual(BigInteger.Parse("2500000"), sell.TradingFeeRaw, "sell fee");
    AssertEqual(BigInteger.Parse("766706194"), sell.AmountOutRaw, "sell output");
    AssertEqual(25, sell.TotalImpactBps, "sell total impact");

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
