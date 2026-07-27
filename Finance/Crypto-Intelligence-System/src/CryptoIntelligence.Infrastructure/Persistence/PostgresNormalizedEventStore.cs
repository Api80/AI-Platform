using System.Text.Json;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresNormalizedEventStore(
    CryptoIntelligenceDbContext context)
    : INormalizedEventStore
{
    public async Task AppendAsync(
        Guid rawEventId,
        DateTimeOffset eventTime,
        string parserVersion,
        IReadOnlyList<ParsedAdapterEvent> events,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < events.Count; index++)
        {
            var parsed = events[index];
            var exists = await context.NormalizedDomainEvents.AnyAsync(
                value =>
                    value.RawEventId == rawEventId &&
                    value.DomainEventType == parsed.DomainEventType &&
                    value.DomainEventIndex == index &&
                    value.ParserVersion == parserVersion,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            context.NormalizedDomainEvents.Add(new NormalizedDomainEventEntity
            {
                Id = Guid.NewGuid(),
                RawEventId = rawEventId,
                DomainEventType = parsed.DomainEventType,
                DomainEventIndex = index,
                ProgramId = parsed.ProgramId,
                Payload = JsonSerializer.Serialize(parsed),
                EventTime = eventTime,
                ParserVersion = parserVersion,
                SchemaVersion = "normalized-domain-event-v1",
                CreatedTime = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
