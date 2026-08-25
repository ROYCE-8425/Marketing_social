using DXOS.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DXOS.Infrastructure.Persistence;

public sealed class BootstrapDbContext : DbContext
{
    public BootstrapDbContext(DbContextOptions<BootstrapDbContext> options)
        : base(options)
    {
    }

    public DbSet<RuntimeProbe> RuntimeProbes => Set<RuntimeProbe>();
    public DbSet<CampaignRecord> Campaigns => Set<CampaignRecord>();
    public DbSet<LeadRecord> Leads => Set<LeadRecord>();
    public DbSet<SalesAssignmentState> SalesAssignment => Set<SalesAssignmentState>();
    public DbSet<TrafficSnapshotRecord> TrafficSnapshots => Set<TrafficSnapshotRecord>();
    public DbSet<SpendProposalRecord> SpendProposals => Set<SpendProposalRecord>();
    public DbSet<WebhookEventRecord> WebhookEvents => Set<WebhookEventRecord>();
    public DbSet<SocialPageRecord> SocialPages => Set<SocialPageRecord>();
    public DbSet<SocialCustomerRecord> SocialCustomers => Set<SocialCustomerRecord>();
    public DbSet<SocialConversationRecord> SocialConversations => Set<SocialConversationRecord>();
    public DbSet<SocialMessageRecord> SocialMessages => Set<SocialMessageRecord>();
    public DbSet<AppUserRecord> AppUsers => Set<AppUserRecord>();
    public DbSet<AppRoleRecord> AppRoles => Set<AppRoleRecord>();
    public DbSet<AppRolePermissionRecord> AppRolePermissions => Set<AppRolePermissionRecord>();
    public DbSet<AppUserRoleRecord> AppUserRoles => Set<AppUserRoleRecord>();
    public DbSet<AppAuditLogRecord> AppAuditLogs => Set<AppAuditLogRecord>();
    public DbSet<SocialPostRecord> SocialPosts => Set<SocialPostRecord>();
    public DbSet<SocialCommentRecord> SocialComments => Set<SocialCommentRecord>();
    public DbSet<SocialPostMetricRecord> SocialPostMetrics => Set<SocialPostMetricRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RuntimeProbe>(entity =>
        {
            entity.ToTable("runtime_probes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProbeName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<CampaignRecord>(entity =>
        {
            entity.ToTable("campaigns");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Topic).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Copy).IsRequired();
            entity.Property(e => e.CopySnapshot);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.RejectionReason).HasMaxLength(512);
            entity.Property(e => e.ApprovedAtUtc);
            entity.Property(e => e.CreatedByActor).IsRequired().HasMaxLength(128);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();

            entity.Property(e => e.Kind).HasMaxLength(64).HasDefaultValue("other");
            entity.Property(e => e.Description);
            entity.Property(e => e.PlatformsJson).HasDefaultValue("[\"facebook\"]");
            entity.Property(e => e.EventStartUtc);
            entity.Property(e => e.EventEndUtc);
            entity.Property(e => e.Location).HasMaxLength(512);
            entity.Property(e => e.ImageUrlsJson).HasDefaultValue("[]");
            entity.Property(e => e.LandingUrl).HasMaxLength(1024);
            entity.Property(e => e.ProductName).HasMaxLength(256);
            entity.Property(e => e.ProductPriceVnd).HasColumnType("numeric(18,0)");
            entity.Property(e => e.ProductSku).HasMaxLength(128);
            entity.Property(e => e.ProductImageUrl).HasMaxLength(1024);
        });

        modelBuilder.Entity<LeadRecord>(entity =>
        {
            entity.ToTable("leads");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(64);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(32);
            entity.Property(e => e.SourcesJson);
            entity.Property(e => e.Score).IsRequired();
            entity.Property(e => e.Label).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ScoreBreakdownJson);
            entity.Property(e => e.ReasonsJson);
            entity.Property(e => e.ScoreModel).HasMaxLength(64);
            entity.Property(e => e.ScoreVersion).HasMaxLength(32);
            entity.Property(e => e.ScoredAtUtc);
            entity.Property(e => e.AssignedToActor).HasMaxLength(128);
            entity.Property(e => e.ClaimedByActor).HasMaxLength(128);
            entity.Property(e => e.ConvertedAtUtc);
            entity.Property(e => e.ConversionRevenueVnd).HasColumnType("numeric(18,0)");
            entity.Property(e => e.RejectedByActorsJson);
            entity.Property(e => e.LastRejectionReason).HasMaxLength(512);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<SalesAssignmentState>(entity =>
        {
            entity.ToTable("sales_assignment_state");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.LastAssignedActor).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SalesActors).IsRequired();
        });

        modelBuilder.Entity<TrafficSnapshotRecord>(entity =>
        {
            entity.ToTable("traffic_snapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CampaignId).IsRequired();
            entity.Property(e => e.PeriodDate).IsRequired();
            entity.Property(e => e.Impressions).IsRequired();
            entity.Property(e => e.Clicks).IsRequired();
            entity.Property(e => e.Visits).IsRequired();
            entity.Property(e => e.SpendVnd).HasColumnType("numeric(18,0)").IsRequired();
            entity.Property(e => e.Source).IsRequired().HasMaxLength(32);
            entity.Property(e => e.RecordedByActor).IsRequired().HasMaxLength(128);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<SpendProposalRecord>(entity =>
        {
            entity.ToTable("spend_proposals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FromNote).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ToNote).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Percent).HasColumnType("numeric(5,2)").IsRequired();
            entity.Property(e => e.Rationale).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.ProposedByRole).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ProposedByActor).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.RejectionReason).HasMaxLength(512);
            entity.Property(e => e.DecidedByActor).HasMaxLength(128);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.DecidedAtUtc);
        });

        modelBuilder.Entity<WebhookEventRecord>(entity =>
        {
            entity.ToTable("webhook_events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ExternalEventId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PayloadHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ReceivedAtUtc).IsRequired();
            entity.HasIndex(e => new { e.Provider, e.ExternalEventId }).IsUnique();
        });

        modelBuilder.Entity<SocialPageRecord>(entity =>
        {
            entity.ToTable("pages", "aiecos_social");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Type).HasColumnName("type");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.TotalConversations).HasColumnName("total_conversations");
            entity.Property(e => e.TotalMessages).HasColumnName("total_messages");
            entity.Property(e => e.LastSyncAt).HasColumnName("last_sync_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<SocialCustomerRecord>(entity =>
        {
            entity.ToTable("customers", "aiecos_social");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.PageId).HasColumnName("page_id");
            entity.Property(e => e.PhoneNumbersJson).HasColumnName("phone_numbers").HasColumnType("jsonb");
            entity.Property(e => e.EmailsJson).HasColumnName("emails").HasColumnType("jsonb");
            entity.Property(e => e.TagsJson).HasColumnName("tags").HasColumnType("jsonb");
            entity.Property(e => e.OrderCount).HasColumnName("order_count");
            entity.Property(e => e.PurchasedAmount).HasColumnName("purchased_amount");
            entity.Property(e => e.FirstSeenAt).HasColumnName("first_seen_at");
            entity.Property(e => e.LastSeenAt).HasColumnName("last_seen_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<SocialConversationRecord>(entity =>
        {
            entity.ToTable("conversations", "aiecos_social");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PageId).HasColumnName("page_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.CustomerName).HasColumnName("customer_name");
            entity.Property(e => e.CustomerPhone).HasColumnName("customer_phone");
            entity.Property(e => e.Snippet).HasColumnName("snippet");
            entity.Property(e => e.MessageCount).HasColumnName("message_count");
            entity.Property(e => e.HasPhone).HasColumnName("has_phone");
            entity.Property(e => e.IsReplied).HasColumnName("is_replied");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.AssignedToActor).HasColumnName("assigned_to_actor");
            entity.Property(e => e.InternalNote).HasColumnName("internal_note");
            entity.Property(e => e.TagsJson).HasColumnName("tags").HasColumnType("jsonb");
            entity.Property(e => e.InsertedAt).HasColumnName("inserted_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.SyncedAt).HasColumnName("synced_at");
        });

        modelBuilder.Entity<SocialMessageRecord>(entity =>
        {
            entity.ToTable("messages", "aiecos_social");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.PageId).HasColumnName("page_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.SenderName).HasColumnName("sender_name");
            entity.Property(e => e.SenderType).HasColumnName("sender_type");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.ContentHtml).HasColumnName("content_html");
            entity.Property(e => e.MessageType).HasColumnName("message_type");
            entity.Property(e => e.AttachmentsJson).HasColumnName("attachments").HasColumnType("jsonb");
            entity.Property(e => e.ReactionsJson).HasColumnName("reactions").HasColumnType("jsonb");
            entity.Property(e => e.IsUnsent).HasColumnName("is_unsent");
            entity.Property(e => e.CreatedTime).HasColumnName("created_time");
            entity.Property(e => e.SyncedAt).HasColumnName("synced_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ── RBAC Entity Configurations ──
        modelBuilder.Entity<AppUserRecord>(entity =>
        {
            entity.ToTable("app_users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActorId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
            entity.HasIndex(e => e.ActorId).IsUnique();
        });

        modelBuilder.Entity<AppRoleRecord>(entity =>
        {
            entity.ToTable("app_roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(256);
            entity.Property(e => e.IsSystem).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<AppRolePermissionRecord>(entity =>
        {
            entity.ToTable("app_role_permissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoleId).IsRequired();
            entity.Property(e => e.Permission).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => new { e.RoleId, e.Permission }).IsUnique();
        });

        modelBuilder.Entity<AppUserRoleRecord>(entity =>
        {
            entity.ToTable("app_user_roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.RoleId).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
        });

        modelBuilder.Entity<AppAuditLogRecord>(entity =>
        {
            entity.ToTable("app_audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActorId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Permission).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(64);
            entity.Property(e => e.EntityId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Details).HasMaxLength(2048);
            entity.Property(e => e.TimestampUtc).IsRequired();
        });

        // ── Social Content Entity Configurations ──
        modelBuilder.Entity<SocialPostRecord>(entity =>
        {
            entity.ToTable("social_posts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PostId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PageId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Message).HasMaxLength(4000);
            entity.Property(e => e.PermalinkUrl).HasMaxLength(1024);
            entity.Property(e => e.FullPicture).HasMaxLength(2048);
            entity.Property(e => e.MediaType).HasMaxLength(32);
            entity.Property(e => e.MediaUrl).HasMaxLength(2048);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(2048);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ScheduledAtUtc);
            entity.Property(e => e.GraphScheduled).IsRequired();
            entity.Property(e => e.ReactionCount);
            entity.Property(e => e.CommentCount);
            entity.Property(e => e.ShareCount);
            entity.Property(e => e.CreatedTimeUtc);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => e.PostId).IsUnique();
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<SocialCommentRecord>(entity =>
        {
            entity.ToTable("social_comments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CommentId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PostId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.FromId).HasMaxLength(128);
            entity.Property(e => e.FromName).HasMaxLength(256);
            entity.Property(e => e.Message).HasMaxLength(4000);
            entity.Property(e => e.ParentCommentId).HasMaxLength(128);
            entity.Property(e => e.IsHidden).IsRequired();
            entity.Property(e => e.CreatedTimeUtc);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => e.CommentId).IsUnique();
            entity.HasIndex(e => e.PostId);
        });

        modelBuilder.Entity<SocialPostMetricRecord>(entity =>
        {
            entity.ToTable("social_post_metrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PostId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Impressions).IsRequired();
            entity.Property(e => e.EngagedUsers).IsRequired();
            entity.Property(e => e.Clicks).IsRequired();
            entity.Property(e => e.Source).IsRequired().HasMaxLength(32);
            entity.Property(e => e.DataFreshness).IsRequired().HasMaxLength(32);
            entity.Property(e => e.FetchedAtUtc).IsRequired();
            entity.HasIndex(e => e.PostId).IsUnique();
        });
    }
}
