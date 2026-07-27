using System.Numerics;

namespace CryptoIntelligence.Domain.Common;

public readonly record struct RawAmount
{
    public RawAmount(BigInteger value)
    {
        if (value < BigInteger.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Raw amount cannot be negative.");
        }

        Value = value;
    }

    public BigInteger Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
