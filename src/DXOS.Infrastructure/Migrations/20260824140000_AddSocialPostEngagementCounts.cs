using System;
using DXOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations;

[DbContext(typeof(BootstrapDbContext))]
[Migration("20260824140000_AddSocialPostEngagementCounts")]
public partial class AddSocialPostEngagementCounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ReactionCount",
            table: "social_posts",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "CommentCount",
            table: "social_posts",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<long>(
            name: "ShareCount",
            table: "social_posts",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ReactionCount",
            table: "social_posts");

        migrationBuilder.DropColumn(
            name: "CommentCount",
            table: "social_posts");

        migrationBuilder.DropColumn(
            name: "ShareCount",
            table: "social_posts");
    }
}
