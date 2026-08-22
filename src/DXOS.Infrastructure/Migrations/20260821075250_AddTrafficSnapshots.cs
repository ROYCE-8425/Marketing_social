using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrafficSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "traffic_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Impressions = table.Column<long>(type: "bigint", nullable: false),
                    Clicks = table.Column<long>(type: "bigint", nullable: false),
                    Visits = table.Column<long>(type: "bigint", nullable: false),
                    SpendVnd = table.Column<decimal>(type: "numeric(18,0)", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordedByActor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_traffic_snapshots", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "traffic_snapshots");
        }
    }
}
