using System;
using DXOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations;

[DbContext(typeof(BootstrapDbContext))]
[Migration("20260824150000_AddSocialPostMediaAndScheduling")]
public partial class AddSocialPostMediaAndScheduling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FullPicture",
            table: "social_posts",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MediaType",
            table: "social_posts",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MediaUrl",
            table: "social_posts",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ThumbnailUrl",
            table: "social_posts",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ScheduledAtUtc",
            table: "social_posts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "GraphScheduled",
            table: "social_posts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_social_posts_Status",
            table: "social_posts",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_social_posts_Status",
            table: "social_posts");

        migrationBuilder.DropColumn(
            name: "FullPicture",
            table: "social_posts");

        migrationBuilder.DropColumn(
            name: "MediaType",
            table: "social_posts");

        migrationBuilder.DropColumn(
            name: "MediaUrl",
            table: "social_posts");

        migrationBuilder.DropColumn(
            name: "ThumbnailUrl",
            table: "social_posts");

        migrationBuilder.DropColumn(
            name: "ScheduledAtUtc",
            table: "social_posts");

        migrationBuilder.DropColumn(
            name: "GraphScheduled",
            table: "social_posts");
    }
}
