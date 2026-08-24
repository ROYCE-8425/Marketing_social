using System;
using DXOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DXOS.Infrastructure.Migrations;

[DbContext(typeof(BootstrapDbContext))]
[Migration("20260824120000_RbacInboxAndPageContent")]
public partial class RbacInboxAndPageContent : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 2a. Raw SQL: Create aiecos_social schema if not exists
        migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS aiecos_social;");

        // 2b. Raw SQL: Create aiecos_social tables if not exists
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS aiecos_social.pages (
  id                    text PRIMARY KEY,
  name                  text,
  type                  text DEFAULT 'facebook',
  is_active             boolean DEFAULT true,
  total_conversations   integer DEFAULT 0,
  total_messages        integer DEFAULT 0,
  last_sync_at          timestamptz,
  created_at            timestamptz DEFAULT now(),
  updated_at            timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS aiecos_social.customers (
  id                    text PRIMARY KEY,
  name                  text,
  page_id               text REFERENCES aiecos_social.pages(id),
  phone_numbers         jsonb DEFAULT '[]'::jsonb,
  emails                jsonb DEFAULT '[]'::jsonb,
  tags                  jsonb DEFAULT '[]'::jsonb,
  order_count           integer DEFAULT 0,
  purchased_amount      numeric DEFAULT 0,
  first_seen_at         timestamptz,
  last_seen_at          timestamptz,
  created_at            timestamptz DEFAULT now(),
  updated_at            timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS aiecos_social.conversations (
  id                    text PRIMARY KEY,
  page_id               text REFERENCES aiecos_social.pages(id),
  customer_id           text REFERENCES aiecos_social.customers(id),
  customer_name         text,
  customer_phone        text,
  snippet               text,
  message_count         integer DEFAULT 0,
  has_phone             boolean DEFAULT false,
  is_replied            boolean DEFAULT false,
  status                text NOT NULL DEFAULT 'open',
  assigned_to_actor     text,
  internal_note         text,
  tags                  jsonb DEFAULT '[]'::jsonb,
  inserted_at           timestamptz DEFAULT now(),
  updated_at            timestamptz DEFAULT now(),
  synced_at             timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS aiecos_social.messages (
  id                    text PRIMARY KEY,
  conversation_id       text REFERENCES aiecos_social.conversations(id),
  page_id               text REFERENCES aiecos_social.pages(id),
  sender_id             text,
  sender_name           text,
  sender_type           text,
  content               text,
  content_html          text,
  message_type          text DEFAULT 'text',
  attachments           jsonb DEFAULT '[]'::jsonb,
  reactions             jsonb DEFAULT '[]'::jsonb,
  is_unsent             boolean DEFAULT false,
  created_time          timestamptz,
  synced_at             timestamptz DEFAULT now(),
  created_at            timestamptz DEFAULT now()
);
");

        // 2c. Raw SQL: Idempotent column additions for existing conversations table
        migrationBuilder.Sql(@"
ALTER TABLE aiecos_social.conversations ADD COLUMN IF NOT EXISTS status text NOT NULL DEFAULT 'open';
ALTER TABLE aiecos_social.conversations ADD COLUMN IF NOT EXISTS assigned_to_actor text;
ALTER TABLE aiecos_social.conversations ADD COLUMN IF NOT EXISTS internal_note text;
ALTER TABLE aiecos_social.conversations ADD COLUMN IF NOT EXISTS customer_phone text;
");

        // 2d. EF CreateTable for RBAC and Social Content tables
        migrationBuilder.CreateTable(
            name: "app_users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_app_users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "app_roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_app_roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "app_role_permissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                Permission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_app_role_permissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "app_user_roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_app_user_roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "app_audit_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Permission = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Details = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_app_audit_logs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "social_posts",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                PostId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                PageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                PermalinkUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_posts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "social_comments",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                CommentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                PostId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                FromId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                FromName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                ParentCommentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                CreatedTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_comments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "social_post_metrics",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                PostId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Impressions = table.Column<long>(type: "bigint", nullable: false),
                EngagedUsers = table.Column<long>(type: "bigint", nullable: false),
                Clicks = table.Column<long>(type: "bigint", nullable: false),
                Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DataFreshness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                FetchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_social_post_metrics", x => x.Id);
            });

        // Indexes
        migrationBuilder.CreateIndex(
            name: "IX_app_users_ActorId",
            table: "app_users",
            column: "ActorId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_app_roles_Name",
            table: "app_roles",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_app_role_permissions_RoleId_Permission",
            table: "app_role_permissions",
            columns: new[] { "RoleId", "Permission" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_app_user_roles_UserId_RoleId",
            table: "app_user_roles",
            columns: new[] { "UserId", "RoleId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_social_posts_PostId",
            table: "social_posts",
            column: "PostId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_social_comments_CommentId",
            table: "social_comments",
            column: "CommentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_social_comments_PostId",
            table: "social_comments",
            column: "PostId");

        migrationBuilder.CreateIndex(
            name: "IX_social_post_metrics_PostId",
            table: "social_post_metrics",
            column: "PostId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "social_post_metrics");
        migrationBuilder.DropTable(name: "social_comments");
        migrationBuilder.DropTable(name: "social_posts");
        migrationBuilder.DropTable(name: "app_audit_logs");
        migrationBuilder.DropTable(name: "app_user_roles");
        migrationBuilder.DropTable(name: "app_role_permissions");
        migrationBuilder.DropTable(name: "app_roles");
        migrationBuilder.DropTable(name: "app_users");
    }
}
