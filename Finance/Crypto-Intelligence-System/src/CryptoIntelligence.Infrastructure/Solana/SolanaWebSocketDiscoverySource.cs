using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CryptoIntelligence.Application.Ingestion;

namespace CryptoIntelligence.Infrastructure.Solana;

public sealed class SolanaWebSocketDiscoverySource(
    Uri endpoint,
    IReadOnlyList<string> programIds,
    string commitment,
    IDiscoveryConnectionObserver? observer = null,
    TimeProvider? timeProvider = null,
    int deduplicationCapacity = 20_000)
    : ISolanaDiscoverySource
{
    private readonly IDiscoveryConnectionObserver _observer =
        observer ?? new NullDiscoveryConnectionObserver();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly HashSet<string> _seenSignatures = new(StringComparer.Ordinal);
    private readonly Queue<string> _seenOrder = new();

    public async IAsyncEnumerable<SolanaSignatureNotification> DiscoverAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<SolanaSignatureNotification>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });
        _ = RunAsync(channel.Writer, cancellationToken);
        await foreach (var notification in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return notification;
        }
    }

    private async Task RunAsync(
        ChannelWriter<SolanaSignatureNotification> writer,
        CancellationToken cancellationToken)
    {
        var reconnectAttempt = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var socket = new ClientWebSocket();
                try
                {
                    await socket.ConnectAsync(endpoint, cancellationToken);
                    await _observer.ConnectedAsync(
                        endpoint.ToString(),
                        _timeProvider.GetUtcNow(),
                        cancellationToken);
                    reconnectAttempt = 0;

                    var requestPrograms = await SubscribeAsync(socket, cancellationToken);
                    var subscriptions = new Dictionary<long, string>();
                    while (socket.State == WebSocketState.Open &&
                           !cancellationToken.IsCancellationRequested)
                    {
                        var json = await ReceiveMessageAsync(socket, cancellationToken);
                        var parsed = SolanaWebSocketMessageParser.Parse(
                            json,
                            requestPrograms,
                            subscriptions,
                            _timeProvider.GetUtcNow());
                        if (parsed is not null && Remember(parsed.Signature))
                        {
                            await writer.WriteAsync(parsed, cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception) when (exception is WebSocketException or
                                                       IOException or
                                                       JsonException)
                {
                    reconnectAttempt++;
                    await _observer.DisconnectedAsync(
                        endpoint.ToString(),
                        exception.Message,
                        _timeProvider.GetUtcNow(),
                        cancellationToken);
                }

                var delay = TimeSpan.FromMilliseconds(
                    Math.Min(30_000, 250 * Math.Pow(2, Math.Min(reconnectAttempt, 7))));
                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
            return;
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task<IReadOnlyDictionary<long, string>> SubscribeAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var requests = new Dictionary<long, string>();
        for (var index = 0; index < programIds.Count; index++)
        {
            var requestId = index + 1L;
            var programId = programIds[index];
            requests.Add(requestId, programId);
            var json = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = requestId,
                method = "logsSubscribe",
                @params = new object[]
                {
                    new { mentions = new[] { programId } },
                    new { commitment }
                }
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }

        return requests;
    }

    private bool Remember(string signature)
    {
        if (!_seenSignatures.Add(signature))
        {
            return false;
        }

        _seenOrder.Enqueue(signature);
        while (_seenOrder.Count > deduplicationCapacity)
        {
            _seenSignatures.Remove(_seenOrder.Dequeue());
        }

        return true;
    }

    private static async Task<string> ReceiveMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("Solana WebSocket closed the connection.");
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
            }
        }
    }
}

public static class SolanaWebSocketMessageParser
{
    public static SolanaSignatureNotification? Parse(
        string json,
        IReadOnlyDictionary<long, string> requestPrograms,
        IDictionary<long, string> subscriptions,
        DateTimeOffset observedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("id", out var idElement) &&
            root.TryGetProperty("result", out var resultElement) &&
            resultElement.ValueKind == JsonValueKind.Number)
        {
            var requestId = idElement.GetInt64();
            if (requestPrograms.TryGetValue(requestId, out var programId))
            {
                subscriptions[resultElement.GetInt64()] = programId;
            }

            return null;
        }

        if (!root.TryGetProperty("method", out var method) ||
            method.GetString() != "logsNotification")
        {
            return null;
        }

        var parameters = root.GetProperty("params");
        var subscription = parameters.GetProperty("subscription").GetInt64();
        if (!subscriptions.TryGetValue(subscription, out var subscribedProgram))
        {
            return null;
        }

        var result = parameters.GetProperty("result");
        var slot = checked((ulong)result.GetProperty("context").GetProperty("slot").GetInt64());
        var value = result.GetProperty("value");
        var signature = value.GetProperty("signature").GetString()
            ?? throw new JsonException("logsNotification has no signature.");
        var failed = value.TryGetProperty("err", out var error) &&
                     error.ValueKind != JsonValueKind.Null;
        return new SolanaSignatureNotification(
            subscribedProgram,
            signature,
            slot,
            failed,
            observedAt);
    }
}
