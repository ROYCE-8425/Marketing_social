using System;
using DXOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations;

[DbContext(typeof(BootstrapDbContext))]
[Migration("20260825120000_AddCampaignStudioFields")]
public partial class AddCampaignStudioFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "campaigns",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "other");

        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "campaigns",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlatformsJson",
            table: "campaigns",
            type: "text",
            nullable: false,
            defaultValue: "[\"facebook\"]");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EventStartUtc",
            table: "campaigns",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EventEndUtc",
            table: "campaigns",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Location",
            table: "campaigns",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ImageUrlsJson",
            table: "campaigns",
            type: "text",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "LandingUrl",
            table: "campaigns",
            type: "character varying(1024)",
            maxLength: 1024,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProductName",
            table: "campaigns",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "ProductPriceVnd",
            table: "campaigns",
            type: "numeric(18,0)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProductSku",
            table: "campaigns",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProductImageUrl",
            table: "campaigns",
            type: "character varying(1024)",
            maxLength: 1024,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Kind",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "Description",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "PlatformsJson",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "EventStartUtc",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "EventEndUtc",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "Location",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "ImageUrlsJson",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "LandingUrl",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "ProductName",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "ProductPriceVnd",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "ProductSku",
            table: "campaigns");

        migrationBuilder.DropColumn(
            name: "ProductImageUrl",
            table: "campaigns");
    }
}
