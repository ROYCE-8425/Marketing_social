using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Stage15Enhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "leads",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastRejectionReason",
                table: "leads",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonsJson",
                table: "leads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedByActorsJson",
                table: "leads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoreBreakdownJson",
                table: "leads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourcesJson",
                table: "leads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "leads",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAtUtc",
                table: "campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CopySnapshot",
                table: "campaigns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "campaigns",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "spend_proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromNote = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ToNote = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ProposedByRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProposedByActor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DecidedByActor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spend_proposals", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "spend_proposals");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "LastRejectionReason",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "ReasonsJson",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "RejectedByActorsJson",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "ScoreBreakdownJson",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "SourcesJson",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "CopySnapshot",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "campaigns");
        }
    }
}
