using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Danatadbir.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRuleResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rule_results",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SensorExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    RuleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RuleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_results", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rule_results_reading",
                table: "rule_results",
                columns: new[] { "SensorExternalId", "MetricKey", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_rule_results_reading_rule",
                table: "rule_results",
                columns: new[] { "SensorExternalId", "MetricKey", "Timestamp", "Seq", "RuleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rule_results");
        }
    }
}
