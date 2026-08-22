using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class LeadToCpl : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "campaigns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Topic = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Copy = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedByActor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_campaigns", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "leads",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Score = table.Column<int>(type: "integer", nullable: false),
                CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                AssignedToActor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ClaimedByActor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_leads", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "sales_assignment_state",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false),
                LastAssignedActor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                SalesActors = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sales_assignment_state", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "campaigns");

        migrationBuilder.DropTable(
            name: "leads");

        migrationBuilder.DropTable(
            name: "sales_assignment_state");
    }
}
