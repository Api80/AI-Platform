using System.Net.Http.Json;
using System.Text.Json;
using CryptoIntelligence.Application.Ingestion;

namespace CryptoIntelligence.Infrastructure.Solana;

public sealed class SolanaRpcBackfillSource(
    HttpClient client,
    string sourceName)
    : ISolanaBackfillSource, IDisposable
{
    public async Task<ulong> GetFinalizedSlotAsync(
        CancellationToken cancellationToken)
    {
        using var document = await SendAsync(
            "getSlot",
            [new { commitment = "finalized" }],
            cancellationToken);
        return checked((ulong)document.RootElement
            .GetProperty("result")
            .GetInt64());
    }

    public async Task<SolanaBackfillBatch> ListFinalizedSignaturesAsync(
        string programId,
        ulong fromExclusive,
        ulong toInclusive,
        int maximumSignatures,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programId);
        if (maximumSignatures <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSignatures));
        }

        var values = new List<SolanaBackfillSignature>();
        string? before = null;
        var complete = false;
        while (values.Count < maximumSignatures)
        {
            var pageLimit = Math.Min(1_000, maximumSignatures - values.Count);
            object options = before is null
                ? new
                {
                    commitment = "finalized",
                    limit = pageLimit
                }
                : new
                {
                    commitment = "finalized",
                    limit = pageLimit,
                    before
                };
            using var document = await SendAsync(
                "getSignaturesForAddress",
                [programId, options],
                cancellationToken);
            var page = document.RootElement.GetProperty("result");
            if (page.GetArrayLength() == 0)
            {
                complete = true;
                break;
            }

            var crossedLowerBoundary = false;
            string? lastSignature = null;
            foreach (var item in page.EnumerateArray())
            {
                var signature = item.GetProperty("signature").GetString()
                                ?? throw new InvalidDataException(
                                    "Solana signature result is missing a signature.");
                var slot = checked((ulong)item.GetProperty("slot").GetInt64());
                lastSignature = signature;
                if (slot <= fromExclusive)
                {
                    crossedLowerBoundary = true;
                    continue;
                }

                if (slot > toInclusive)
                {
                    continue;
                }

                var failed = item.TryGetProperty("err", out var error) &&
                             error.ValueKind != JsonValueKind.Null;
                DateTimeOffset? blockTime = null;
                if (item.TryGetProperty("blockTime", out var time) &&
                    time.ValueKind == JsonValueKind.Number)
                {
                    blockTime = DateTimeOffset.FromUnixTimeSeconds(time.GetInt64());
                }

                values.Add(new SolanaBackfillSignature(
                    signature,
                    slot,
                    failed,
                    blockTime));
                if (values.Count == maximumSignatures)
                {
                    break;
                }
            }

            if (crossedLowerBoundary || page.GetArrayLength() < pageLimit)
            {
                complete = true;
                break;
            }

            before = lastSignature;
            if (before is null)
            {
                break;
            }
        }

        return new SolanaBackfillBatch(
            fromExclusive,
            toInclusive,
            complete,
            values
                .OrderBy(value => value.Slot)
                .ThenBy(value => value.Signature, StringComparer.Ordinal)
                .ToArray());
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

    public void Dispose() => client.Dispose();
}

public sealed class FallbackSolanaBackfillSource(
    IReadOnlyList<ISolanaBackfillSource> sources)
    : ISolanaBackfillSource, IDisposable
{
    public Task<ulong> GetFinalizedSlotAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(
            source => source.GetFinalizedSlotAsync(cancellationToken));

    public Task<SolanaBackfillBatch> ListFinalizedSignaturesAsync(
        string programId,
        ulong fromExclusive,
        ulong toInclusive,
        int maximumSignatures,
        CancellationToken cancellationToken) =>
        ExecuteAsync(source => source.ListFinalizedSignaturesAsync(
            programId,
            fromExclusive,
            toInclusive,
            maximumSignatures,
            cancellationToken));

    private async Task<T> ExecuteAsync<T>(Func<ISolanaBackfillSource, Task<T>> action)
    {
        if (sources.Count == 0)
        {
            throw new InvalidOperationException("At least one RPC source is required.");
        }

        var failures = new List<Exception>();
        foreach (var source in sources)
        {
            try
            {
                return await action(source);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(exception);
            }
        }

        throw new AggregateException("All Solana backfill sources failed.", failures);
    }

    public void Dispose()
    {
        foreach (var disposable in sources.OfType<IDisposable>())
        {
            disposable.Dispose();
        }
    }
}
