using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _007_M3AcceptanceAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "automated_assessment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raw_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pool_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    slot = table.Column<long>(type: "bigint", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    deferred_count = table.Column<int>(type: "integer", nullable: false),
                    first_attempt_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_attempt_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automated_assessment_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_automated_assessment_attempts_raw_blockchain_events_raw_eve~",
                        column: x => x.raw_event_id,
                        principalTable: "raw_blockchain_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_automated_assessment_attempts_outcome_time",
                table: "automated_assessment_attempts",
                columns: new[] { "outcome", "last_attempt_time" });

            migrationBuilder.CreateIndex(
                name: "ux_automated_assessment_attempts_raw_event",
                table: "automated_assessment_attempts",
                column: "raw_event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "automated_assessment_attempts");
        }
    }
}
