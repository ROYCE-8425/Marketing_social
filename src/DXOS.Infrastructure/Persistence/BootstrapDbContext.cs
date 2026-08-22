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
            entity.Property(e => e.AssignedToActor).HasMaxLength(128);
            entity.Property(e => e.ClaimedByActor).HasMaxLength(128);
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
    }
}
