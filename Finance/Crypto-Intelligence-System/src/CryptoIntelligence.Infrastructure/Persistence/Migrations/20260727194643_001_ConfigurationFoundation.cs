using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _001_ConfigurationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuration_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    configuration_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    canonical_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuration_snapshots_version_created",
                table: "configuration_snapshots",
                columns: new[] { "configuration_version", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_configuration_snapshots_hash",
                table: "configuration_snapshots",
                column: "configuration_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuration_snapshots");
        }
    }
}
