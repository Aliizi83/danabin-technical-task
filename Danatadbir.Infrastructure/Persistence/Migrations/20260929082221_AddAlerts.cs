using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Danatadbir.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RuleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SensorExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartTs = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTs = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeakValue = table.Column<double>(type: "double precision", nullable: false),
                    ReadingCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alerts_rule_series_start",
                table: "alerts",
                columns: new[] { "RuleId", "SensorExternalId", "MetricKey", "StartTs" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alerts_series_start",
                table: "alerts",
                columns: new[] { "SensorExternalId", "MetricKey", "StartTs" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");
        }
    }
}
