using System.Numerics;

namespace CryptoIntelligence.Domain.Intelligence;

public sealed record CpmmExactInputQuote(
    BigInteger AmountInRaw,
    BigInteger TradingFeeRaw,
    BigInteger CreatorFeeRaw,
    BigInteger AmountInAfterFeeRaw,
    BigInteger AmountOutRaw,
    int TotalImpactBasisPoints);

public static class CpmmExactInputQuoteCalculator
{
    private static readonly BigInteger BasisPoints = 10_000;

    public static CpmmExactInputQuote Calculate(
        BigInteger reserveInRaw,
        BigInteger reserveOutRaw,
        BigInteger amountInRaw,
        int tradingFeeBasisPoints,
        int creatorFeeBasisPoints = 0)
    {
        if (reserveInRaw <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reserveInRaw),
                "Input reserve must be greater than zero.");
        }

        if (reserveOutRaw <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reserveOutRaw),
                "Output reserve must be greater than zero.");
        }

        if (amountInRaw <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountInRaw),
                "Input amount must be greater than zero.");
        }

        if (tradingFeeBasisPoints is < 0 or >= 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tradingFeeBasisPoints),
                "Trading fee must be between 0 and 9999 basis points.");
        }

        if (creatorFeeBasisPoints is < 0 or >= 10_000 ||
            tradingFeeBasisPoints + creatorFeeBasisPoints >= 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(creatorFeeBasisPoints),
                "Total input fees must be below 10000 basis points.");
        }

        var tradingFeeRaw = DivideRoundUp(
            amountInRaw * tradingFeeBasisPoints,
            BasisPoints);
        var creatorFeeRaw = DivideRoundUp(
            amountInRaw * creatorFeeBasisPoints,
            BasisPoints);
        var amountInAfterFeeRaw =
            amountInRaw - tradingFeeRaw - creatorFeeRaw;
        if (amountInAfterFeeRaw <= 0)
        {
            throw new InvalidOperationException(
                "Fees consume the complete input amount.");
        }

        var amountOutRaw =
            reserveOutRaw * amountInAfterFeeRaw /
            (reserveInRaw + amountInAfterFeeRaw);
        if (amountOutRaw <= 0 || amountOutRaw >= reserveOutRaw)
        {
            throw new InvalidOperationException(
                "Pool state cannot produce a valid exact-input quote.");
        }

        var idealOutNumerator = amountInRaw * reserveOutRaw;
        var actualOutAtIdealScale = amountOutRaw * reserveInRaw;
        var impact = idealOutNumerator <= actualOutAtIdealScale
            ? 0
            : checked((int)(
                (idealOutNumerator - actualOutAtIdealScale) *
                BasisPoints /
                idealOutNumerator));
        return new CpmmExactInputQuote(
            amountInRaw,
            tradingFeeRaw,
            creatorFeeRaw,
            amountInAfterFeeRaw,
            amountOutRaw,
            impact);
    }

    private static BigInteger DivideRoundUp(
        BigInteger numerator,
        BigInteger denominator) =>
        (numerator + denominator - BigInteger.One) / denominator;
}
