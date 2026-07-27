using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

internal static class IngestionModelConfiguration
{
    public static void ConfigureIngestion(this ModelBuilder modelBuilder)
    {
        ConfigureRawEvents(modelBuilder);
        ConfigureCheckpoints(modelBuilder);
        ConfigureSlotStates(modelBuilder);
        ConfigureNormalizedEvents(modelBuilder);
    }

    private static void ConfigureRawEvents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RawBlockchainEventEntity>();
        entity.ToTable("raw_blockchain_events");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Id).HasColumnName("id");
        entity.Property(value => value.EventId).HasColumnName("event_id").HasMaxLength(64).IsFixedLength();
        entity.Property(value => value.Chain).HasColumnName("chain").HasMaxLength(32);
        entity.Property(value => value.Network).HasColumnName("network").HasMaxLength(64);
        entity.Property(value => value.Slot).HasColumnName("slot");
        entity.Property(value => value.BlockHash).HasColumnName("block_hash").HasMaxLength(128);
        entity.Property(value => value.TransactionSignature).HasColumnName("transaction_signature").HasMaxLength(128);
        entity.Property(value => value.InstructionIndex).HasColumnName("instruction_index");
        entity.Property(value => value.InnerInstructionIndex).HasColumnName("inner_instruction_index");
        entity.Property(value => value.ProgramId).HasColumnName("program_id").HasMaxLength(64);
        entity.Property(value => value.EventType).HasColumnName("event_type").HasMaxLength(100);
        entity.Property(value => value.EventOrdinal).HasColumnName("event_ordinal");
        entity.Property(value => value.EventTime).HasColumnName("event_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.ObservedTime).HasColumnName("observed_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.FinalizedTime).HasColumnName("finalized_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.CommitmentLevel).HasColumnName("commitment_level").HasMaxLength(32);
        entity.Property(value => value.CanonicalStatus).HasColumnName("canonical_status").HasConversion<string>().HasMaxLength(32);
        entity.Property(value => value.FinalityUpdatedTime).HasColumnName("finality_updated_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.RevertedTime).HasColumnName("reverted_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.RevertReason).HasColumnName("revert_reason").HasMaxLength(2_000);
        entity.Property(value => value.Source).HasColumnName("source").HasMaxLength(100);
        entity.Property(value => value.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb");
        entity.Property(value => value.SchemaVersion).HasColumnName("schema_version").HasMaxLength(50);
        entity.Property(value => value.ProcessingStatus).HasColumnName("processing_status").HasConversion<string>().HasMaxLength(32);
        entity.Property(value => value.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        entity.Property(value => value.LeaseUntil).HasColumnName("lease_until").HasColumnType("timestamp with time zone");
        entity.Property(value => value.RetryCount).HasColumnName("retry_count");
        entity.Property(value => value.FirstFailureTime).HasColumnName("first_failure_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.LastFailureTime).HasColumnName("last_failure_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.LastError).HasColumnName("last_error").HasMaxLength(2_000);
        entity.Property(value => value.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        entity.Property(value => value.CreatedTime).HasColumnName("created_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.UpdatedTime).HasColumnName("updated_time").HasColumnType("timestamp with time zone");

        entity.HasIndex(value => value.EventId).IsUnique().HasDatabaseName("ux_raw_events_event_id");
        entity.HasIndex(value => new
        {
            value.Chain,
            value.Network,
            value.TransactionSignature,
            value.InstructionIndex,
            value.InnerInstructionIndex,
            value.EventType,
            value.EventOrdinal,
            value.SchemaVersion
        })
            .IsUnique()
            .HasDatabaseName("ux_raw_events_chain_identity");
        entity.HasIndex(value => new { value.Network, value.Slot }).HasDatabaseName("ix_raw_events_network_slot");
        entity.HasIndex(value => value.TransactionSignature).HasDatabaseName("ix_raw_events_signature");
        entity.HasIndex(value => new { value.EventType, value.EventTime }).HasDatabaseName("ix_raw_events_type_time");
        entity.HasIndex(value => new { value.ProcessingStatus, value.ObservedTime }).HasDatabaseName("ix_raw_events_processing_time");
        entity.HasIndex(value => new { value.ProgramId, value.EventTime }).HasDatabaseName("ix_raw_events_program_time");
    }

    private static void ConfigureCheckpoints(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<IngestionCheckpointEntity>();
        entity.ToTable("ingestion_checkpoints");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Id).HasColumnName("id");
        entity.Property(value => value.Chain).HasColumnName("chain").HasMaxLength(32);
        entity.Property(value => value.Network).HasColumnName("network").HasMaxLength(64);
        entity.Property(value => value.Source).HasColumnName("source").HasMaxLength(100);
        entity.Property(value => value.SubscriptionType).HasColumnName("subscription_type").HasMaxLength(100);
        entity.Property(value => value.ObservedThroughSlot).HasColumnName("observed_through_slot");
        entity.Property(value => value.PersistedThroughSlot).HasColumnName("persisted_through_slot");
        entity.Property(value => value.ProcessedThroughSlot).HasColumnName("processed_through_slot");
        entity.Property(value => value.FinalizedThroughSlot).HasColumnName("finalized_through_slot");
        entity.Property(value => value.ReconciledThroughSlot).HasColumnName("reconciled_through_slot");
        entity.Property(value => value.LastCompletedSignature).HasColumnName("last_completed_signature").HasMaxLength(128);
        entity.Property(value => value.Status).HasColumnName("status").HasMaxLength(32);
        entity.Property(value => value.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        entity.Property(value => value.LeaseUntil).HasColumnName("lease_until").HasColumnType("timestamp with time zone");
        entity.Property(value => value.UpdatedTime).HasColumnName("updated_time").HasColumnType("timestamp with time zone");
        entity.HasIndex(value => new
        {
            value.Chain,
            value.Network,
            value.Source,
            value.SubscriptionType
        })
            .IsUnique()
            .HasDatabaseName("ux_ingestion_checkpoints_source_subscription");
    }

    private static void ConfigureSlotStates(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<IngestionSlotStateEntity>();
        entity.ToTable("ingestion_slot_states");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Id).HasColumnName("id");
        entity.Property(value => value.CheckpointId).HasColumnName("checkpoint_id");
        entity.Property(value => value.Slot).HasColumnName("slot");
        entity.Property(value => value.Observed).HasColumnName("observed");
        entity.Property(value => value.Persisted).HasColumnName("persisted");
        entity.Property(value => value.Processed).HasColumnName("processed");
        entity.Property(value => value.Finalized).HasColumnName("finalized");
        entity.Property(value => value.Reconciled).HasColumnName("reconciled");
        entity.Property(value => value.HasGap).HasColumnName("has_gap");
        entity.Property(value => value.GapReason).HasColumnName("gap_reason").HasMaxLength(1_000);
        entity.Property(value => value.UpdatedTime).HasColumnName("updated_time").HasColumnType("timestamp with time zone");
        entity.HasIndex(value => new { value.CheckpointId, value.Slot })
            .IsUnique()
            .HasDatabaseName("ux_ingestion_slot_states_checkpoint_slot");
        entity.HasOne<IngestionCheckpointEntity>()
            .WithMany()
            .HasForeignKey(value => value.CheckpointId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureNormalizedEvents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NormalizedDomainEventEntity>();
        entity.ToTable("normalized_domain_events");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Id).HasColumnName("id");
        entity.Property(value => value.RawEventId).HasColumnName("raw_event_id");
        entity.Property(value => value.DomainEventType).HasColumnName("domain_event_type").HasMaxLength(100);
        entity.Property(value => value.DomainEventIndex).HasColumnName("domain_event_index");
        entity.Property(value => value.ProgramId).HasColumnName("program_id").HasMaxLength(64);
        entity.Property(value => value.Payload).HasColumnName("payload").HasColumnType("jsonb");
        entity.Property(value => value.EventTime).HasColumnName("event_time").HasColumnType("timestamp with time zone");
        entity.Property(value => value.ParserVersion).HasColumnName("parser_version").HasMaxLength(100);
        entity.Property(value => value.SchemaVersion).HasColumnName("schema_version").HasMaxLength(50);
        entity.Property(value => value.CreatedTime).HasColumnName("created_time").HasColumnType("timestamp with time zone");
        entity.HasIndex(value => new
        {
            value.RawEventId,
            value.DomainEventType,
            value.DomainEventIndex,
            value.ParserVersion
        })
            .IsUnique()
            .HasDatabaseName("ux_normalized_events_parser_identity");
        entity.HasIndex(value => new { value.ProgramId, value.EventTime })
            .HasDatabaseName("ix_normalized_events_program_time");
        entity.HasOne<RawBlockchainEventEntity>()
            .WithMany()
            .HasForeignKey(value => value.RawEventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
