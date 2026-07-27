namespace CryptoIntelligence.Domain.Common;

public readonly record struct BasisPoints
{
    public BasisPoints(int value)
    {
        if (value is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Basis points must be between 0 and 10000.");
        }

        Value = value;
    }

    public int Value { get; }

    public decimal Ratio => Value / 10_000m;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
