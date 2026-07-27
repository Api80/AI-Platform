using System.Net;
using System.Text;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Infrastructure.Solana;

namespace CryptoIntelligence.Infrastructure.Tests;

public sealed class SolanaSourceTests
{
    [Fact]
    public void WebSocket_parser_maps_subscription_to_program_and_notification()
    {
        var requests = new Dictionary<long, string> { [1] = "program-a" };
        var subscriptions = new Dictionary<long, string>();

        Assert.Null(SolanaWebSocketMessageParser.Parse(
            """{"jsonrpc":"2.0","result":42,"id":1}""",
            requests,
            subscriptions,
            DateTimeOffset.UnixEpoch));

        var notification = SolanaWebSocketMessageParser.Parse(
            """
            {
              "jsonrpc":"2.0",
              "method":"logsNotification",
              "params":{
                "result":{
                  "context":{"slot":123},
                  "value":{"signature":"signature-a","err":null,"logs":[]}
                },
                "subscription":42
              }
            }
            """,
            requests,
            subscriptions,
            DateTimeOffset.UnixEpoch);

        Assert.NotNull(notification);
        Assert.Equal("program-a", notification.ProgramId);
        Assert.Equal("signature-a", notification.Signature);
        Assert.Equal(123UL, notification.Slot);
        Assert.False(notification.Failed);
    }

    [Fact]
    public async Task Rpc_source_retries_temporary_null_then_returns_transaction()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, """{"jsonrpc":"2.0","result":null,"id":1}"""),
            Json(
                HttpStatusCode.OK,
                """
                {
                  "jsonrpc":"2.0",
                  "result":{"slot":123,"blockTime":1000,"meta":{},"transaction":{}},
                  "id":1
                }
                """));
        var source = new SolanaRpcTransactionSource(
            new HttpClient(handler) { BaseAddress = new Uri("https://rpc.example/") },
            "primary",
            maximumAttempts: 2,
            initialRetryDelay: TimeSpan.Zero);

        var result = await source.FetchAsync(
            "signature",
            "confirmed",
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(123UL, result.Slot);
        Assert.Equal("primary", result.Source);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Fallback_source_uses_second_source_after_primary_failure()
    {
        var expected = new SolanaTransactionPayload(
            "signature",
            10,
            DateTimeOffset.UnixEpoch,
            "confirmed",
            "fallback",
            "{}");
        var source = new FallbackSolanaTransactionSource(
            [
                new StubSource(exception: new HttpRequestException("primary down")),
                new StubSource(expected)
            ]);

        var result = await source.FetchAsync(
            "signature",
            "confirmed",
            CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Backfill_source_filters_slot_range_and_orders_oldest_first()
    {
        var handler = new SequenceHandler(
            Json(HttpStatusCode.OK, """{"jsonrpc":"2.0","result":105,"id":1}"""),
            Json(
                HttpStatusCode.OK,
                """
                {
                  "jsonrpc":"2.0",
                  "result":[
                    {"signature":"newer","slot":104,"err":null,"blockTime":1004},
                    {"signature":"inside-b","slot":103,"err":null,"blockTime":1003},
                    {"signature":"inside-a","slot":102,"err":null,"blockTime":1002},
                    {"signature":"boundary","slot":100,"err":null,"blockTime":1000}
                  ],
                  "id":1
                }
                """));
        var source = new SolanaRpcBackfillSource(
            new HttpClient(handler) { BaseAddress = new Uri("https://rpc.example/") },
            "primary");

        var head = await source.GetFinalizedSlotAsync(CancellationToken.None);
        var result = await source.ListFinalizedSignaturesAsync(
            "program",
            fromExclusive: 100,
            toInclusive: 103,
            maximumSignatures: 10,
            CancellationToken.None);

        Assert.Equal(105UL, head);
        Assert.True(result.Complete);
        Assert.Equal(["inside-a", "inside-b"], result.Signatures.Select(value => value.Signature));
    }

    private static HttpResponseMessage Json(
        HttpStatusCode statusCode,
        string json) => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class SequenceHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class StubSource(
        SolanaTransactionPayload? result = null,
        Exception? exception = null)
        : ISolanaTransactionSource
    {
        public Task<SolanaTransactionPayload?> FetchAsync(
            string signature,
            string commitment,
            CancellationToken cancellationToken) =>
            exception is null
                ? Task.FromResult(result)
                : Task.FromException<SolanaTransactionPayload?>(exception);
    }
}
