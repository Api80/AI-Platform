namespace CryptoIntelligence.Domain.Common;

public readonly record struct Slot
{
    public Slot(ulong value)
    {
        Value = value;
    }

    public ulong Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
