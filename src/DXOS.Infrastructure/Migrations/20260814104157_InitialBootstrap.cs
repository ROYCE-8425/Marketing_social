using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialBootstrap : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "runtime_probes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProbeName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_runtime_probes", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "runtime_probes");
    }
}
