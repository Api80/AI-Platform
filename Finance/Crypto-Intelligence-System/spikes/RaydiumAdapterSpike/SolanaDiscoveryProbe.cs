using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaydiumAdapterSpike;

internal sealed record DiscoveryProbeResult(
    string Signature,
    long Slot,
    bool TransactionAvailable,
    string? ConfirmationStatus);

internal static class SolanaDiscoveryProbe
{
    public static async Task<DiscoveryProbeResult> RunAsync(
        string websocketUrl,
        string rpcUrl,
        string programId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var effectiveToken = timeoutSource.Token;

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(websocketUrl), effectiveToken);

        var subscription = JsonSerializer.SerializeToUtf8Bytes(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "logsSubscribe",
            @params = new object[]
            {
                new { mentions = new[] { programId } },
                new { commitment = "confirmed" }
            }
        });
        await socket.SendAsync(
            subscription,
            WebSocketMessageType.Text,
            endOfMessage: true,
            effectiveToken);

        string? signature = null;
        long slot = 0;
        while (signature is null)
        {
            var message = await ReceiveMessageAsync(socket, effectiveToken);
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (!root.TryGetProperty("method", out var method) ||
                method.GetString() != "logsNotification")
            {
                continue;
            }

            var result = root.GetProperty("params").GetProperty("result");
            slot = result.GetProperty("context").GetProperty("slot").GetInt64();
            signature = result.GetProperty("value").GetProperty("signature").GetString();
        }

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        var transactionAvailable = await TransactionIsAvailableAsync(
            client,
            rpcUrl,
            signature,
            effectiveToken);
        var confirmationStatus = await GetConfirmationStatusAsync(
            client,
            rpcUrl,
            signature,
            effectiveToken);

        return new DiscoveryProbeResult(
            signature,
            slot,
            transactionAvailable,
            confirmationStatus);
    }

    private static async Task<string> ReceiveMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var output = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("RPC WebSocket closed before a log notification.");
            }

            output.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static async Task<bool> TransactionIsAvailableAsync(
        HttpClient client,
        string rpcUrl,
        string signature,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "getTransaction",
            @params = new object[]
            {
                signature,
                new
                {
                    encoding = "json",
                    commitment = "confirmed",
                    maxSupportedTransactionVersion = 0
                }
            }
        };

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync(rpcUrl, request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (json?["result"] is not null)
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(attempt * 250), cancellationToken);
        }

        return false;
    }

    private static async Task<string?> GetConfirmationStatusAsync(
        HttpClient client,
        string rpcUrl,
        string signature,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "getSignatureStatuses",
            @params = new object[]
            {
                new[] { signature },
                new { searchTransactionHistory = true }
            }
        };
        using var response = await client.PostAsJsonAsync(rpcUrl, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json?["result"]?["value"]?[0]?["confirmationStatus"]?.GetValue<string>();
    }
}
