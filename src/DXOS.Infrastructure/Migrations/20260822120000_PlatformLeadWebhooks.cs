using System;
using DXOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations;

[DbContext(typeof(BootstrapDbContext))]
[Migration("20260822120000_PlatformLeadWebhooks")]
public partial class PlatformLeadWebhooks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ScoredAtUtc",
            table: "leads",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ScoreModel",
            table: "leads",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ScoreVersion",
            table: "leads",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "webhook_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ExternalEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                LeadId = table.Column<Guid>(type: "uuid", nullable: true),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_webhook_events", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_webhook_events_Provider_ExternalEventId",
            table: "webhook_events",
            columns: new[] { "Provider", "ExternalEventId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "webhook_events");
        migrationBuilder.DropColumn(name: "ScoredAtUtc", table: "leads");
        migrationBuilder.DropColumn(name: "ScoreModel", table: "leads");
        migrationBuilder.DropColumn(name: "ScoreVersion", table: "leads");
    }
}
