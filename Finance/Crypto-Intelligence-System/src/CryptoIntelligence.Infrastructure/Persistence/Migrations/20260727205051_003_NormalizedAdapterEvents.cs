using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _003_NormalizedAdapterEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "normalized_domain_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    domain_event_index = table.Column<int>(type: "integer", nullable: false),
                    program_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    event_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    parser_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    schema_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_normalized_domain_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_normalized_domain_events_raw_blockchain_events_raw_event_id",
                        column: x => x.raw_event_id,
                        principalTable: "raw_blockchain_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_normalized_events_program_time",
                table: "normalized_domain_events",
                columns: new[] { "program_id", "event_time" });

            migrationBuilder.CreateIndex(
                name: "ux_normalized_events_parser_identity",
                table: "normalized_domain_events",
                columns: new[] { "raw_event_id", "domain_event_type", "domain_event_index", "parser_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "normalized_domain_events");
        }
    }
}
