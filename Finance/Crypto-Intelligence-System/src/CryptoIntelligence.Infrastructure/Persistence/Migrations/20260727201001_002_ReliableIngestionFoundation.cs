using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _002_ReliableIngestionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ingestion_checkpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    network = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subscription_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    observed_through_slot = table.Column<long>(type: "bigint", nullable: false),
                    persisted_through_slot = table.Column<long>(type: "bigint", nullable: false),
                    processed_through_slot = table.Column<long>(type: "bigint", nullable: false),
                    finalized_through_slot = table.Column<long>(type: "bigint", nullable: false),
                    reconciled_through_slot = table.Column<long>(type: "bigint", nullable: false),
                    last_completed_signature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_checkpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "raw_blockchain_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    chain = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    network = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    slot = table.Column<long>(type: "bigint", nullable: false),
                    block_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    transaction_signature = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    instruction_index = table.Column<int>(type: "integer", nullable: false),
                    inner_instruction_index = table.Column<int>(type: "integer", nullable: true),
                    program_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_ordinal = table.Column<int>(type: "integer", nullable: false),
                    event_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    observed_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finalized_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    commitment_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    canonical_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    finality_updated_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reverted_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revert_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: false),
                    schema_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    processing_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    first_failure_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_failure_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_blockchain_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_slot_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    checkpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<long>(type: "bigint", nullable: false),
                    observed = table.Column<bool>(type: "boolean", nullable: false),
                    persisted = table.Column<bool>(type: "boolean", nullable: false),
                    processed = table.Column<bool>(type: "boolean", nullable: false),
                    finalized = table.Column<bool>(type: "boolean", nullable: false),
                    reconciled = table.Column<bool>(type: "boolean", nullable: false),
                    has_gap = table.Column<bool>(type: "boolean", nullable: false),
                    gap_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_slot_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_ingestion_slot_states_ingestion_checkpoints_checkpoint_id",
                        column: x => x.checkpoint_id,
                        principalTable: "ingestion_checkpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_ingestion_checkpoints_source_subscription",
                table: "ingestion_checkpoints",
                columns: new[] { "chain", "network", "source", "subscription_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ingestion_slot_states_checkpoint_slot",
                table: "ingestion_slot_states",
                columns: new[] { "checkpoint_id", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_raw_events_network_slot",
                table: "raw_blockchain_events",
                columns: new[] { "network", "slot" });

            migrationBuilder.CreateIndex(
                name: "ix_raw_events_processing_time",
                table: "raw_blockchain_events",
                columns: new[] { "processing_status", "observed_time" });

            migrationBuilder.CreateIndex(
                name: "ix_raw_events_program_time",
                table: "raw_blockchain_events",
                columns: new[] { "program_id", "event_time" });

            migrationBuilder.CreateIndex(
                name: "ix_raw_events_signature",
                table: "raw_blockchain_events",
                column: "transaction_signature");

            migrationBuilder.CreateIndex(
                name: "ix_raw_events_type_time",
                table: "raw_blockchain_events",
                columns: new[] { "event_type", "event_time" });

            migrationBuilder.CreateIndex(
                name: "ux_raw_events_chain_identity",
                table: "raw_blockchain_events",
                columns: new[] { "chain", "network", "transaction_signature", "instruction_index", "inner_instruction_index", "event_type", "event_ordinal", "schema_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_raw_events_event_id",
                table: "raw_blockchain_events",
                column: "event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_slot_states");

            migrationBuilder.DropTable(
                name: "raw_blockchain_events");

            migrationBuilder.DropTable(
                name: "ingestion_checkpoints");
        }
    }
}
