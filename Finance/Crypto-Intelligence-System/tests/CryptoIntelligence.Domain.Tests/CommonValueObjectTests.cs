using System.Numerics;
using CryptoIntelligence.Domain.Common;

namespace CryptoIntelligence.Domain.Tests;

public sealed class CommonValueObjectTests
{
    private const string ValidAddress = "LanMV9sAd7wArD4vJFi2qDdfnVhFxYSUg6eADduJ3uj";
    private const string ValidSignature =
        "TN4Vffv1i7K8NNUHDDvz8RTqp4CdKYE2Zvuuktc788VtirTKpjoDNj9VJ6AYbkrssbToVaxQrtSH2ffFLM9KMfL";

    [Fact]
    public void SolanaIdentifiers_accept_valid_base58_values()
    {
        Assert.Equal(ValidAddress, new ProgramId(ValidAddress).Value);
        Assert.Equal(ValidAddress, new TokenAddress(ValidAddress).Value);
        Assert.Equal(ValidAddress, new WalletAddress(ValidAddress).Value);
        Assert.Equal(ValidSignature, new TransactionSignature(ValidSignature).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains-0-invalid-character")]
    [InlineData("short")]
    public void ProgramId_rejects_invalid_values(string value)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ProgramId(value));
    }

    [Fact]
    public void RawAmount_rejects_negative_value()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RawAmount(new BigInteger(-1)));
    }

    [Fact]
    public void BasisPoints_enforces_percentage_boundary()
    {
        Assert.Equal(1m, new BasisPoints(10_000).Ratio);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BasisPoints(10_001));
    }

    [Fact]
    public void UtcTimestamp_rejects_non_utc_offset()
    {
        var value = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.FromHours(8));
        Assert.Throws<ArgumentException>(() => new UtcTimestamp(value));
    }
}
