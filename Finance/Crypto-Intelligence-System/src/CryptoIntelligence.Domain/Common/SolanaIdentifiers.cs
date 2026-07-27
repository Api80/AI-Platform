namespace CryptoIntelligence.Domain.Common;

public readonly record struct TransactionSignature
{
    public TransactionSignature(string value)
    {
        Value = SolanaText.Validate(value, 64, 88, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProgramId
{
    public ProgramId(string value)
    {
        Value = SolanaText.Validate(value, 32, 44, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct TokenAddress
{
    public TokenAddress(string value)
    {
        Value = SolanaText.Validate(value, 32, 44, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WalletAddress
{
    public WalletAddress(string value)
    {
        Value = SolanaText.Validate(value, 32, 44, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

internal static class SolanaText
{
    private const string Base58Alphabet =
        "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static string Validate(string value, int minimumLength, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length < minimumLength || value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Solana value length must be between {minimumLength} and {maximumLength}.");
        }

        if (value.Any(character => !Base58Alphabet.Contains(character, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Solana value contains a non-Base58 character.", parameterName);
        }

        return value;
    }
}
