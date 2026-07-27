using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CryptoIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _005_ThemeRiskAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "latest_evaluation_as_of_time",
                table: "token_candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "latest_risk_assessment_id",
                table: "token_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "latest_theme_match_id",
                table: "token_candidates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "risk_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    overall_score = table.Column<int>(type: "integer", nullable: false),
                    risk_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    hard_reject = table.Column<bool>(type: "boolean", nullable: false),
                    rule_results = table.Column<string>(type: "jsonb", nullable: false),
                    reasons = table.Column<string>(type: "jsonb", nullable: false),
                    input_as_of_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    risk_model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_risk_assessments_feature_snapshots_feature_snapshot_id",
                        column: x => x.feature_snapshot_id,
                        principalTable: "feature_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_risk_assessments_tokens_token_id",
                        column: x => x.token_id,
                        principalTable: "tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "theme_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matched = table.Column<bool>(type: "boolean", nullable: false),
                    blocked = table.Column<bool>(type: "boolean", nullable: false),
                    configuration_valid = table.Column<bool>(type: "boolean", nullable: false),
                    theme_score = table.Column<int>(type: "integer", nullable: false),
                    matched_themes = table.Column<string>(type: "jsonb", nullable: false),
                    match_reasons = table.Column<string>(type: "jsonb", nullable: false),
                    input_as_of_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    configuration_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_theme_matches", x => x.id);
                    table.ForeignKey(
                        name: "FK_theme_matches_tokens_token_id",
                        column: x => x.token_id,
                        principalTable: "tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_token_candidates_latest_risk_assessment_id",
                table: "token_candidates",
                column: "latest_risk_assessment_id");

            migrationBuilder.CreateIndex(
                name: "IX_token_candidates_latest_theme_match_id",
                table: "token_candidates",
                column: "latest_theme_match_id");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessments_feature_snapshot_id",
                table: "risk_assessments",
                column: "feature_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessments_token_time",
                table: "risk_assessments",
                columns: new[] { "token_id", "input_as_of_time" });

            migrationBuilder.CreateIndex(
                name: "ux_risk_assessments_token_version_time",
                table: "risk_assessments",
                columns: new[] { "token_id", "risk_model_version", "input_as_of_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_theme_matches_token_time",
                table: "theme_matches",
                columns: new[] { "token_id", "input_as_of_time" });

            migrationBuilder.CreateIndex(
                name: "ux_theme_matches_token_version_time",
                table: "theme_matches",
                columns: new[] { "token_id", "configuration_version", "input_as_of_time" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_token_candidates_risk_assessments_latest_risk_assessment_id",
                table: "token_candidates",
                column: "latest_risk_assessment_id",
                principalTable: "risk_assessments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_token_candidates_theme_matches_latest_theme_match_id",
                table: "token_candidates",
                column: "latest_theme_match_id",
                principalTable: "theme_matches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_token_candidates_risk_assessments_latest_risk_assessment_id",
                table: "token_candidates");

            migrationBuilder.DropForeignKey(
                name: "FK_token_candidates_theme_matches_latest_theme_match_id",
                table: "token_candidates");

            migrationBuilder.DropTable(
                name: "risk_assessments");

            migrationBuilder.DropTable(
                name: "theme_matches");

            migrationBuilder.DropIndex(
                name: "IX_token_candidates_latest_risk_assessment_id",
                table: "token_candidates");

            migrationBuilder.DropIndex(
                name: "IX_token_candidates_latest_theme_match_id",
                table: "token_candidates");

            migrationBuilder.DropColumn(
                name: "latest_evaluation_as_of_time",
                table: "token_candidates");

            migrationBuilder.DropColumn(
                name: "latest_risk_assessment_id",
                table: "token_candidates");

            migrationBuilder.DropColumn(
                name: "latest_theme_match_id",
                table: "token_candidates");
        }
    }
}
