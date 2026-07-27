using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Domain.Radar;

namespace CryptoIntelligence.Application.Tests;

public sealed class RadarProjectionTests
{
    [Fact]
    public async Task Pool_and_swap_build_eligible_candidate_and_features()
    {
        var store = new InMemoryRadarStore();
        var handler = new RadarProjectionHandler(store, Configuration());
        var created = DateTimeOffset.Parse("2026-07-28T00:00:00Z");

        await handler.HandleAsync(
            Event(
                "PoolCreated",
                created,
                100,
                new Dictionary<string, string>
                {
                    ["pool_address"] = "pool",
                    ["base_mint"] = "base",
                    ["quote_mint"] = "quote",
                    ["base_reserve"] = "1000",
                    ["quote_reserve"] = "2000"
                }),
            CancellationToken.None);
        await handler.HandleAsync(
            Event(
                "SwapObserved",
                created.AddSeconds(30),
                101,
                new Dictionary<string, string>
                {
                    ["pool_address"] = "pool",
                    ["base_mint"] = "base",
                    ["quote_mint"] = "quote",
                    ["base_amount"] = "10",
                    ["quote_amount"] = "30",
                    ["side"] = "Buy",
                    ["trader"] = "wallet"
                }),
            CancellationToken.None);

        Assert.Equal(CandidateStatus.Eligible, store.Candidates["base"].Status);
        Assert.Single(store.Features);
        Assert.Equal(1, store.Features[0].Values.BuyCount);
    }

    [Fact]
    public async Task Replay_orders_by_event_time_slot_and_ordinal()
    {
        var handler = new RecordingProjectionHandler();
        var clock = new ReplayClock();
        var replay = new ReplayEngine([handler], clock);
        var time = DateTimeOffset.Parse("2026-07-28T00:00:00Z");
        ProjectionEvent[] events =
        [
            Event("SwapObserved", time.AddSeconds(2), 3, new Dictionary<string, string>()),
            Event("PoolCreated", time, 2, new Dictionary<string, string>()),
            Event("MintCreated", time, 1, new Dictionary<string, string>())
        ];

        await replay.ReplayAsync(events, CancellationToken.None);

        Assert.Equal(
            ["MintCreated", "PoolCreated", "SwapObserved"],
            handler.EventTypes);
        Assert.Equal(time.AddSeconds(2), clock.UtcNow);
    }

    [Fact]
    public async Task Replay_produces_the_same_candidate_and_features()
    {
        var time = DateTimeOffset.Parse("2026-07-28T00:00:00Z");
        ProjectionEvent[] events =
        [
            Event(
                "SwapObserved",
                time.AddSeconds(30),
                101,
                new Dictionary<string, string>
                {
                    ["pool_address"] = "pool",
                    ["base_mint"] = "base",
                    ["quote_mint"] = "quote",
                    ["base_amount"] = "10",
                    ["quote_amount"] = "30",
                    ["side"] = "Buy",
                    ["trader"] = "wallet"
                }),
            Event(
                "PoolCreated",
                time,
                100,
                new Dictionary<string, string>
                {
                    ["pool_address"] = "pool",
                    ["base_mint"] = "base",
                    ["quote_mint"] = "quote",
                    ["base_reserve"] = "1000",
                    ["quote_reserve"] = "2000"
                })
        ];

        var first = await ReplayAsync(events);
        var second = await ReplayAsync(events.AsEnumerable().Reverse());

        Assert.Equal(first.Candidates["base"], second.Candidates["base"]);
        Assert.Equal(first.Features, second.Features);
    }

    private static async Task<InMemoryRadarStore> ReplayAsync(
        IEnumerable<ProjectionEvent> events)
    {
        var store = new InMemoryRadarStore();
        var replay = new ReplayEngine(
            [new RadarProjectionHandler(store, Configuration())],
            new ReplayClock());

        await replay.ReplayAsync(events, CancellationToken.None);

        return store;
    }

    private static MvpConfiguration Configuration() => new()
    {
        Radar = new RadarConfiguration
        {
            MinimumObservationSeconds = 30,
            MaximumCandidateAgeSeconds = 600,
            MaximumEntryAgeSeconds = 300,
            FeatureWindowsSeconds = [60]
        }
    };

    private static ProjectionEvent Event(
        string eventType,
        DateTimeOffset time,
        ulong slot,
        IReadOnlyDictionary<string, string> attributes) => new(
        Guid.NewGuid(),
        slot,
        time,
        time,
        new ParsedAdapterEvent(
            "program",
            eventType,
            0,
            null,
            0,
            eventType,
            "test",
            $"{eventType}-{slot}",
            attributes));

    private sealed class InMemoryRadarStore : IRadarProjectionStore
    {
        public Dictionary<string, CandidateProjection> Candidates { get; } = [];
        public List<FeatureProjection> Features { get; } = [];
        private List<MarketObservation> Observations { get; } = [];

        public Task UpsertTokenAsync(
            TokenProjection token,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpsertPoolAsync(
            PoolProjection pool,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AppendSwapAsync(
            SwapProjection swap,
            CancellationToken cancellationToken)
        {
            Observations.Add(new MarketObservation(
                swap.Slot,
                swap.EventTime,
                swap.PriceInQuote,
                swap.Side,
                swap.BaseAmount,
                swap.QuoteAmount,
                swap.TraderWallet,
                4_000m,
                swap.PriceImpactBasisPoints));
            return Task.CompletedTask;
        }

        public Task AppendLiquidityAsync(
            LiquidityProjection liquidity,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<CandidateProjection?> GetCandidateAsync(
            string tokenAddress,
            CancellationToken cancellationToken)
        {
            Candidates.TryGetValue(tokenAddress, out var candidate);
            return Task.FromResult(candidate);
        }

        public Task UpsertCandidateAsync(
            CandidateProjection candidate,
            CancellationToken cancellationToken)
        {
            Candidates[candidate.TokenAddress] = candidate;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MarketObservation>> LoadMarketObservationsAsync(
            string poolAddress,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MarketObservation>>(
                Observations.Where(value =>
                        value.EventTime > from && value.EventTime <= to)
                    .ToArray());

        public Task AppendFeatureAsync(
            FeatureProjection feature,
            CancellationToken cancellationToken)
        {
            Features.Add(feature);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProjectionHandler : IProjectionEventHandler
    {
        public List<string> EventTypes { get; } = [];

        public Task HandleAsync(
            ProjectionEvent projectionEvent,
            CancellationToken cancellationToken)
        {
            EventTypes.Add(projectionEvent.Event.DomainEventType);
            return Task.CompletedTask;
        }
    }

    private sealed class ReplayClock : IReplayClock
    {
        public DateTimeOffset UtcNow { get; private set; }

        public void AdvanceTo(DateTimeOffset timestamp)
        {
            if (timestamp < UtcNow)
            {
                throw new InvalidOperationException("Replay clock cannot move backwards.");
            }

            UtcNow = timestamp;
        }
    }
}
