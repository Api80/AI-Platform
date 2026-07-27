using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CryptoIntelligence.Application.Ingestion;

namespace CryptoIntelligence.Infrastructure.Solana;

public sealed class SolanaRpcTransactionSource(
    HttpClient client,
    string sourceName,
    int maximumAttempts = 4,
    TimeSpan? initialRetryDelay = null)
    : ISolanaTransactionSource, IDisposable
{
    private readonly TimeSpan _initialRetryDelay =
        initialRetryDelay ?? TimeSpan.FromMilliseconds(250);

    public async Task<SolanaTransactionPayload?> FetchAsync(
        string signature,
        string commitment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitment);

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
            {
                Content = JsonContent.Create(new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "getTransaction",
                    @params = new object[]
                    {
                        signature,
                        new
                        {
                            commitment,
                            encoding = "json",
                            maxSupportedTransactionVersion = 0
                        }
                    }
                })
            };

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException &&
                !cancellationToken.IsCancellationRequested &&
                attempt < maximumAttempts)
            {
                await Task.Delay(RetryDelay(attempt), cancellationToken);
                continue;
            }

            using (response)
            {
                if (IsRetryable(response.StatusCode) && attempt < maximumAttempts)
                {
                    await Task.Delay(
                        RetryDelay(response, attempt),
                        cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.TryGetProperty("error", out var error))
                {
                    throw new SolanaRpcException(sourceName, error.GetRawText());
                }

                var result = root.GetProperty("result");
                if (result.ValueKind == JsonValueKind.Null)
                {
                    if (attempt < maximumAttempts)
                    {
                        await Task.Delay(
                            RetryDelay(response, attempt),
                            cancellationToken);
                        continue;
                    }

                    return null;
                }

                var slot = checked((ulong)result.GetProperty("slot").GetInt64());
                var eventTime = result.TryGetProperty("blockTime", out var blockTime) &&
                                blockTime.ValueKind == JsonValueKind.Number
                    ? DateTimeOffset.FromUnixTimeSeconds(blockTime.GetInt64())
                    : DateTimeOffset.UtcNow;
                return new SolanaTransactionPayload(
                    signature,
                    slot,
                    eventTime,
                    commitment,
                    sourceName,
                    json);
            }
        }

        return null;
    }

    private TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } retryAfter)
        {
            return retryAfter;
        }

        return RetryDelay(attempt);
    }

    private TimeSpan RetryDelay(int attempt)
    {
        var multiplier = 1 << Math.Min(attempt - 1, 8);
        return TimeSpan.FromMilliseconds(_initialRetryDelay.TotalMilliseconds * multiplier);
    }

    private static bool IsRetryable(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    public void Dispose() => client.Dispose();
}

public sealed class FallbackSolanaTransactionSource(
    IReadOnlyList<ISolanaTransactionSource> sources)
    : ISolanaTransactionSource, IDisposable
{
    public async Task<SolanaTransactionPayload?> FetchAsync(
        string signature,
        string commitment,
        CancellationToken cancellationToken)
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
                var result = await source.FetchAsync(
                    signature,
                    commitment,
                    cancellationToken);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count == sources.Count)
        {
            throw new AggregateException("All Solana RPC sources failed.", failures);
        }

        return null;
    }

    public void Dispose()
    {
        foreach (var disposable in sources.OfType<IDisposable>())
        {
            disposable.Dispose();
        }
    }
}

public sealed class SolanaRpcException(string source, string error)
    : Exception($"Solana RPC source '{source}' returned an error: {error}");
