using System.Numerics;

namespace RaydiumAdapterSpike;

internal sealed record CpmmExactInputQuote(
    BigInteger AmountInRaw,
    BigInteger TradingFeeRaw,
    BigInteger AmountInAfterFeeRaw,
    BigInteger AmountOutRaw,
    int TotalImpactBps);

internal static class CpmmQuoteCalculator
{
    public static CpmmExactInputQuote QuoteExactInput(
        BigInteger reserveInRaw,
        BigInteger reserveOutRaw,
        BigInteger amountInRaw,
        int tradingFeeBps)
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

        if (tradingFeeBps is < 0 or >= 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tradingFeeBps),
                "Trading fee must be between 0 and 9999 basis points.");
        }

        const int basisPoints = 10_000;
        var tradingFeeRaw = DivideRoundUp(amountInRaw * tradingFeeBps, basisPoints);
        var amountInAfterFeeRaw = amountInRaw - tradingFeeRaw;

        if (amountInAfterFeeRaw <= 0)
        {
            throw new InvalidOperationException("Trading fee consumes the complete input amount.");
        }

        var amountOutRaw =
            reserveOutRaw * amountInAfterFeeRaw /
            (reserveInRaw + amountInAfterFeeRaw);

        if (amountOutRaw <= 0 || amountOutRaw >= reserveOutRaw)
        {
            throw new InvalidOperationException("Pool state cannot produce a valid exact-input quote.");
        }

        var idealOutNumerator = amountInRaw * reserveOutRaw;
        var actualOutAtIdealScale = amountOutRaw * reserveInRaw;
        var totalImpactBps = idealOutNumerator <= actualOutAtIdealScale
            ? 0
            : (int)((idealOutNumerator - actualOutAtIdealScale) * basisPoints /
                    idealOutNumerator);

        return new CpmmExactInputQuote(
            amountInRaw,
            tradingFeeRaw,
            amountInAfterFeeRaw,
            amountOutRaw,
            totalImpactBps);
    }

    private static BigInteger DivideRoundUp(BigInteger numerator, BigInteger denominator) =>
        (numerator + denominator - BigInteger.One) / denominator;
}
