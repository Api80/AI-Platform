using System.Net.Http.Json;
using System.Numerics;
using System.Text.Json;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Infrastructure.Solana.Raydium;

namespace CryptoIntelligence.Infrastructure.Solana;

public sealed class SolanaTokenRiskEvidenceSource(
    HttpClient client,
    string sourceName)
    : ISolanaTokenRiskEvidenceSource, IDisposable
{
    public async Task<TokenAuthorityEvidence> GetAuthorityAsync(
        string mintAddress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mintAddress);
        var observedAt = DateTimeOffset.UtcNow;
        try
        {
            using var document = await SendAsync(
                "getAccountInfo",
                [
                    mintAddress,
                    new
                    {
                        commitment = "finalized",
                        encoding = "jsonParsed"
                    }
                ],
                cancellationToken);
            var result = document.RootElement.GetProperty("result");
            var slot = ContextSlot(result);
            var value = result.GetProperty("value");
            if (value.ValueKind == JsonValueKind.Null)
            {
                return AuthorityUnavailable(
                    mintAddress,
                    observedAt,
                    "Mint account is unavailable.");
            }

            var owner = value.GetProperty("owner").GetString();
            if (!string.Equals(
                    owner,
                    RaydiumCpmmSellQuoteEvidenceSource.ClassicTokenProgramId,
                    StringComparison.Ordinal))
            {
                return new TokenAuthorityEvidence(
                    EvidenceAvailability.StructurallyUnsupported,
                    mintAddress,
                    MintAuthorityEnabled: null,
                    FreezeAuthorityEnabled: null,
                    MintAuthority: null,
                    FreezeAuthority: null,
                    owner,
                    slot,
                    observedAt,
                    "Only the classic SPL Token program is supported in Phase 1.");
            }

            var parsed = value.GetProperty("data").GetProperty("parsed");
            if (!string.Equals(
                    parsed.GetProperty("type").GetString(),
                    "mint",
                    StringComparison.Ordinal))
            {
                return new TokenAuthorityEvidence(
                    EvidenceAvailability.StructurallyUnsupported,
                    mintAddress,
                    null,
                    null,
                    null,
                    null,
                    owner,
                    slot,
                    observedAt,
                    "Account is not a parsed SPL mint.");
            }

            var info = parsed.GetProperty("info");
            var mintAuthority = NullableString(info, "mintAuthority");
            var freezeAuthority = NullableString(info, "freezeAuthority");
            return new TokenAuthorityEvidence(
                EvidenceAvailability.Available,
                mintAddress,
                MintAuthorityEnabled: mintAuthority is not null,
                FreezeAuthorityEnabled: freezeAuthority is not null,
                mintAuthority,
                freezeAuthority,
                owner,
                slot,
                observedAt,
                Reason: null);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            return AuthorityUnavailable(
                mintAddress,
                observedAt,
                exception.Message);
        }
    }

    public async Task<HolderConcentrationEvidence> GetHolderConcentrationAsync(
        string mintAddress,
        string? creatorAddress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mintAddress);
        var observedAt = DateTimeOffset.UtcNow;
        try
        {
            using var supplyDocument = await SendAsync(
                "getTokenSupply",
                [mintAddress, new { commitment = "finalized" }],
                cancellationToken);
            var supplyResult = supplyDocument.RootElement.GetProperty("result");
            var supplySlot = ContextSlot(supplyResult);
            var supply = Amount(
                supplyResult.GetProperty("value"),
                "amount");
            if (supply <= 0)
            {
                return HoldersUnavailable(
                    mintAddress,
                    creatorAddress,
                    observedAt,
                    "Token supply is unavailable or zero.");
            }

            using var largestDocument = await SendAsync(
                "getTokenLargestAccounts",
                [mintAddress, new { commitment = "finalized" }],
                cancellationToken);
            var largestResult = largestDocument.RootElement.GetProperty("result");
            var largestSlot = ContextSlot(largestResult);
            var top10 = largestResult
                .GetProperty("value")
                .EnumerateArray()
                .Take(10)
                .Aggregate(
                    BigInteger.Zero,
                    (sum, value) => sum + Amount(value, "amount"));

            BigInteger? creatorHolding = null;
            ulong? creatorSlot = null;
            if (!string.IsNullOrWhiteSpace(creatorAddress))
            {
                using var creatorDocument = await SendAsync(
                    "getTokenAccountsByOwner",
                    [
                        creatorAddress,
                        new { mint = mintAddress },
                        new
                        {
                            commitment = "finalized",
                            encoding = "jsonParsed"
                        }
                    ],
                    cancellationToken);
                var creatorResult =
                    creatorDocument.RootElement.GetProperty("result");
                creatorSlot = ContextSlot(creatorResult);
                creatorHolding = creatorResult
                    .GetProperty("value")
                    .EnumerateArray()
                    .Aggregate(
                        BigInteger.Zero,
                        (sum, value) =>
                            sum + Amount(
                                value
                                    .GetProperty("account")
                                    .GetProperty("data")
                                    .GetProperty("parsed")
                                    .GetProperty("info")
                                    .GetProperty("tokenAmount"),
                                "amount"));
            }

            if (top10 > supply ||
                creatorHolding is { } creatorValue && creatorValue > supply)
            {
                return HoldersUnavailable(
                    mintAddress,
                    creatorAddress,
                    observedAt,
                    "Holder balances exceed total supply.");
            }

            var asOfSlot = new[] { (ulong?)supplySlot, largestSlot, creatorSlot }
                .Where(value => value.HasValue)
                .Min();
            var availability = creatorHolding.HasValue
                ? EvidenceAvailability.Available
                : EvidenceAvailability.Missing;
            return new HolderConcentrationEvidence(
                availability,
                mintAddress,
                creatorAddress,
                supply,
                creatorHolding,
                top10,
                creatorHolding.HasValue
                    ? ToBasisPoints(creatorHolding.Value, supply)
                    : null,
                ToBasisPoints(top10, supply),
                asOfSlot,
                observedAt,
                creatorHolding.HasValue
                    ? null
                    : "Creator address is missing; creator concentration cannot be computed.");
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            return HoldersUnavailable(
                mintAddress,
                creatorAddress,
                observedAt,
                exception.Message);
        }
    }

    private async Task<JsonDocument> SendAsync(
        string method,
        object[] parameters,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method,
                @params = parameters
            })
        };
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error))
        {
            document.Dispose();
            throw new SolanaRpcException(sourceName, error.GetRawText());
        }

        return document;
    }

    private static ulong ContextSlot(JsonElement result) =>
        checked((ulong)result
            .GetProperty("context")
            .GetProperty("slot")
            .GetInt64());

    private static BigInteger Amount(JsonElement value, string propertyName)
    {
        var text = value.GetProperty(propertyName).GetString()
                   ?? throw new InvalidDataException(
                       $"Token amount '{propertyName}' is missing.");
        return BigInteger.TryParse(text, out var amount)
            ? amount
            : throw new InvalidDataException(
                $"Token amount '{propertyName}' is invalid.");
    }

    private static string? NullableString(
        JsonElement value,
        string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.GetString();
    }

    private static int ToBasisPoints(
        BigInteger value,
        BigInteger total) =>
        checked((int)(value * 10_000 / total));

    private static TokenAuthorityEvidence AuthorityUnavailable(
        string mintAddress,
        DateTimeOffset observedAt,
        string reason) => new(
        EvidenceAvailability.TemporarilyUnavailable,
        mintAddress,
        null,
        null,
        null,
        null,
        null,
        null,
        observedAt,
        reason);

    private static HolderConcentrationEvidence HoldersUnavailable(
        string mintAddress,
        string? creatorAddress,
        DateTimeOffset observedAt,
        string reason) => new(
        EvidenceAvailability.TemporarilyUnavailable,
        mintAddress,
        creatorAddress,
        null,
        null,
        null,
        null,
        null,
        null,
        observedAt,
        reason);

    public void Dispose() => client.Dispose();
}

public sealed class FallbackSolanaTokenRiskEvidenceSource(
    IReadOnlyList<ISolanaTokenRiskEvidenceSource> sources)
    : ISolanaTokenRiskEvidenceSource, IDisposable
{
    public Task<TokenAuthorityEvidence> GetAuthorityAsync(
        string mintAddress,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            source => source.GetAuthorityAsync(mintAddress, cancellationToken),
            value => value.Availability);

    public Task<HolderConcentrationEvidence> GetHolderConcentrationAsync(
        string mintAddress,
        string? creatorAddress,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            source => source.GetHolderConcentrationAsync(
                mintAddress,
                creatorAddress,
                cancellationToken),
            value => value.Availability);

    private async Task<T> ExecuteAsync<T>(
        Func<ISolanaTokenRiskEvidenceSource, Task<T>> operation,
        Func<T, EvidenceAvailability> availability)
    {
        if (sources.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one Solana token evidence source is required.");
        }

        T? last = default;
        foreach (var source in sources)
        {
            last = await operation(source);
            if (availability(last) != EvidenceAvailability.TemporarilyUnavailable)
            {
                return last;
            }
        }

        return last ?? throw new InvalidOperationException(
            "Solana token evidence source returned no result.");
    }

    public void Dispose()
    {
        foreach (var disposable in sources.OfType<IDisposable>())
        {
            disposable.Dispose();
        }
    }
}
