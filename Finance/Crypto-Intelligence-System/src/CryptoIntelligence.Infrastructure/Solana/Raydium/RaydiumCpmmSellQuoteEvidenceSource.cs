using System.Numerics;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Domain.Intelligence;

namespace CryptoIntelligence.Infrastructure.Solana.Raydium;

public sealed class RaydiumCpmmSellQuoteEvidenceSource(
    string expectedAdapterVersion,
    int maximumSnapshotAgeSeconds)
    : IRaydiumSellQuoteEvidenceSource
{
    private static readonly BigInteger DecimalMaximum =
        new(decimal.MaxValue);

    private static readonly BigInteger DecimalMinimum =
        new(decimal.MinValue);

    public const string CpmmProgramId =
        "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C";

    public const string ClassicTokenProgramId =
        "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    public SellQuoteEvidence QuoteExactInput(
        RaydiumCpmmPoolSnapshot snapshot,
        BigInteger amountInRaw,
        DateTimeOffset evaluatedAt)
    {
        if (!string.Equals(
                snapshot.ProgramId,
                CpmmProgramId,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.AdapterVersion,
                expectedAdapterVersion,
                StringComparison.Ordinal))
        {
            return Failure(
                SellQuoteStatus.StructurallyUnsupported,
                amountInRaw,
                snapshot,
                "Pool program or adapter version is not pinned.");
        }

        if (!string.Equals(
                snapshot.InputTokenProgramId,
                ClassicTokenProgramId,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.OutputTokenProgramId,
                ClassicTokenProgramId,
                StringComparison.Ordinal))
        {
            return Failure(
                SellQuoteStatus.StructurallyUnsupported,
                amountInRaw,
                snapshot,
                "Token-2022 and unknown token programs are not supported in Phase 1.");
        }

        var age = evaluatedAt - snapshot.AsOfTime;
        if (age < TimeSpan.Zero ||
            age > TimeSpan.FromSeconds(maximumSnapshotAgeSeconds))
        {
            return Failure(
                SellQuoteStatus.Stale,
                amountInRaw,
                snapshot,
                $"Pool snapshot age {age.TotalSeconds:F0}s is outside the allowed range.");
        }

        try
        {
            var quote = CpmmExactInputQuoteCalculator.Calculate(
                snapshot.InputReserveRaw,
                snapshot.OutputReserveRaw,
                amountInRaw,
                snapshot.TradingFeeBasisPoints,
                snapshot.CreatorFeeBasisPoints);
            return new SellQuoteEvidence(
                SellQuoteStatus.Available,
                ToDecimal(quote.AmountInRaw),
                ToDecimal(quote.AmountOutRaw),
                quote.TotalImpactBasisPoints,
                snapshot.AsOfTime,
                snapshot.AdapterVersion,
                FailureReason: null);
        }
        catch (Exception exception) when (
            exception is ArgumentOutOfRangeException or
                InvalidOperationException or
                OverflowException)
        {
            return Failure(
                SellQuoteStatus.TemporarilyUnavailable,
                amountInRaw,
                snapshot,
                exception.Message);
        }
    }

    private static SellQuoteEvidence Failure(
        SellQuoteStatus status,
        BigInteger amountInRaw,
        RaydiumCpmmPoolSnapshot snapshot,
        string reason) => new(
        status,
        amountInRaw > DecimalMaximum || amountInRaw < DecimalMinimum
            ? 0
            : (decimal)amountInRaw,
        OutputQuoteAmount: 0,
        PriceImpactBasisPoints: 0,
        snapshot.AsOfTime,
        snapshot.AdapterVersion,
        reason);

    private static decimal ToDecimal(BigInteger value) =>
        value > DecimalMaximum || value < DecimalMinimum
            ? throw new OverflowException(
                "Quote raw amount exceeds the Phase 1 decimal storage range.")
            : (decimal)value;
}
