namespace CryptoIntelligence.Domain.Common;

public readonly record struct UtcTimestamp
{
    public UtcTimestamp(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC offset.", nameof(value));
        }

        Value = value;
    }

    public DateTimeOffset Value { get; }

    public static UtcTimestamp Now() => new(DateTimeOffset.UtcNow);

    public override string ToString() => Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
}
