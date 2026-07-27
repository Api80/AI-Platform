using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _006_AutomatedRiskEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "evidence",
                table: "risk_assessments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "amm_config_address",
                table: "liquidity_pools",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "base_token_program_id",
                table: "liquidity_pools",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "creator_address",
                table: "liquidity_pools",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quote_token_program_id",
                table: "liquidity_pools",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "evidence",
                table: "risk_assessments");

            migrationBuilder.DropColumn(
                name: "amm_config_address",
                table: "liquidity_pools");

            migrationBuilder.DropColumn(
                name: "base_token_program_id",
                table: "liquidity_pools");

            migrationBuilder.DropColumn(
                name: "creator_address",
                table: "liquidity_pools");

            migrationBuilder.DropColumn(
                name: "quote_token_program_id",
                table: "liquidity_pools");
        }
    }
}
