using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _004_NewTokenRadarProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_natural_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    feature_set_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    as_of_slot = table.Column<long>(type: "bigint", nullable: false),
                    as_of_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    computed_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    values = table.Column<string>(type: "jsonb", nullable: false),
                    source_from_slot = table.Column<long>(type: "bigint", nullable: false),
                    source_to_slot = table.Column<long>(type: "bigint", nullable: false),
                    source_event_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "market_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_index = table.Column<int>(type: "integer", nullable: false),
                    price_in_quote = table.Column<decimal>(type: "numeric(38,18)", precision: 38, scale: 18, nullable: false),
                    base_volume = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    quote_volume = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    buy_count = table.Column<int>(type: "integer", nullable: false),
                    sell_count = table.Column<int>(type: "integer", nullable: false),
                    base_reserve = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    quote_reserve = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    liquidity_in_quote = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    trader_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    price_impact_basis_points = table.Column<int>(type: "integer", nullable: false),
                    as_of_slot = table.Column<long>(type: "bigint", nullable: false),
                    as_of_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    network = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mint_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    lifecycle_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_slot = table.Column<long>(type: "bigint", nullable: false),
                    created_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_observed_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    network = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    first_seen_slot = table.Column<long>(type: "bigint", nullable: false),
                    first_seen_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "liquidity_pools",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chain = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    network = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pool_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dex = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    program_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    base_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_slot = table.Column<long>(type: "bigint", nullable: false),
                    created_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    base_reserve = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    quote_reserve = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    lifecycle_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_observed_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liquidity_pools", x => x.id);
                    table.ForeignKey(
                        name: "FK_liquidity_pools_tokens_base_token_id",
                        column: x => x.base_token_id,
                        principalTable: "tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_liquidity_pools_tokens_quote_token_id",
                        column: x => x.quote_token_id,
                        principalTable: "tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "token_candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    discovered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_token_candidates", x => x.id);
                    table.ForeignKey(
                        name: "FK_token_candidates_tokens_token_id",
                        column: x => x.token_id,
                        principalTable: "tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "liquidity_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    liquidity_index = table.Column<int>(type: "integer", nullable: false),
                    pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    quote_amount = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    base_reserve_after = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    quote_reserve_after = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    slot = table.Column<long>(type: "bigint", nullable: false),
                    event_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liquidity_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_liquidity_events_liquidity_pools_pool_id",
                        column: x => x.pool_id,
                        principalTable: "liquidity_pools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_liquidity_events_raw_blockchain_events_raw_event_id",
                        column: x => x.raw_event_id,
                        principalTable: "raw_blockchain_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "swap_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    swap_index = table.Column<int>(type: "integer", nullable: false),
                    pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trader_wallet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    quote_amount = table.Column<decimal>(type: "numeric(38,0)", precision: 38, scale: 0, nullable: false),
                    price_in_quote = table.Column<decimal>(type: "numeric(38,18)", precision: 38, scale: 18, nullable: false),
                    price_impact_basis_points = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<long>(type: "bigint", nullable: false),
                    event_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    observed_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_swap_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_swap_events_liquidity_pools_pool_id",
                        column: x => x.pool_id,
                        principalTable: "liquidity_pools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_swap_events_raw_blockchain_events_raw_event_id",
                        column: x => x.raw_event_id,
                        principalTable: "raw_blockchain_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_swap_events_tokens_base_token_id",
                        column: x => x.base_token_id,
                        principalTable: "tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_swap_events_tokens_quote_token_id",
                        column: x => x.quote_token_id,
                        principalTable: "tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_swap_events_wallets_trader_wallet_id",
                        column: x => x.trader_wallet_id,
                        principalTable: "wallets",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ux_feature_snapshots_entity_version_slot",
                table: "feature_snapshots",
                columns: new[] { "entity_type", "entity_natural_key", "feature_set_version", "as_of_slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liquidity_events_pool_id",
                table: "liquidity_events",
                column: "pool_id");

            migrationBuilder.CreateIndex(
                name: "ux_liquidity_events_raw_pool_index",
                table: "liquidity_events",
                columns: new[] { "raw_event_id", "pool_id", "liquidity_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liquidity_pools_base_token_id",
                table: "liquidity_pools",
                column: "base_token_id");

            migrationBuilder.CreateIndex(
                name: "IX_liquidity_pools_quote_token_id",
                table: "liquidity_pools",
                column: "quote_token_id");

            migrationBuilder.CreateIndex(
                name: "ux_pools_chain_network_address",
                table: "liquidity_pools",
                columns: new[] { "chain", "network", "pool_address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_market_snapshots_pool_time",
                table: "market_snapshots",
                columns: new[] { "pool_id", "as_of_time" });

            migrationBuilder.CreateIndex(
                name: "ux_market_snapshots_pool_slot_index",
                table: "market_snapshots",
                columns: new[] { "pool_id", "as_of_slot", "event_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_swap_events_base_token_id",
                table: "swap_events",
                column: "base_token_id");

            migrationBuilder.CreateIndex(
                name: "IX_swap_events_quote_token_id",
                table: "swap_events",
                column: "quote_token_id");

            migrationBuilder.CreateIndex(
                name: "IX_swap_events_trader_wallet_id",
                table: "swap_events",
                column: "trader_wallet_id");

            migrationBuilder.CreateIndex(
                name: "ix_swaps_pool_time",
                table: "swap_events",
                columns: new[] { "pool_id", "event_time" });

            migrationBuilder.CreateIndex(
                name: "ux_swaps_raw_pool_index",
                table: "swap_events",
                columns: new[] { "raw_event_id", "pool_id", "swap_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_candidates_status_updated",
                table: "token_candidates",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ux_token_candidates_token",
                table: "token_candidates",
                column: "token_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tokens_chain_network_mint",
                table: "tokens",
                columns: new[] { "chain", "network", "mint_address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_wallets_chain_network_address",
                table: "wallets",
                columns: new[] { "chain", "network", "address" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_snapshots");

            migrationBuilder.DropTable(
                name: "liquidity_events");

            migrationBuilder.DropTable(
                name: "market_snapshots");

            migrationBuilder.DropTable(
                name: "swap_events");

            migrationBuilder.DropTable(
                name: "token_candidates");

            migrationBuilder.DropTable(
                name: "liquidity_pools");

            migrationBuilder.DropTable(
                name: "wallets");

            migrationBuilder.DropTable(
                name: "tokens");
        }
    }
}
