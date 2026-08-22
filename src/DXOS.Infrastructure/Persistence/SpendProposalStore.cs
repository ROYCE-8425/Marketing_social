using DXOS.Application;
using DXOS.Domain;
using DXOS.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DXOS.Infrastructure.Persistence;

public sealed class SpendProposalStore : ISpendProposalStore
{
    private readonly BootstrapDbContext _db;

    public SpendProposalStore(BootstrapDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(SpendProposal proposal, CancellationToken cancellationToken)
    {
        _db.SpendProposals.Add(ToRecord(proposal));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SpendProposal?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await _db.SpendProposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<SpendProposal>> ListAsync(CancellationToken cancellationToken)
    {
        var records = await _db.SpendProposals
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return records.Select(ToDomain).ToList();
    }

    public async Task UpdateAsync(SpendProposal proposal, CancellationToken cancellationToken)
    {
        var record = await _db.SpendProposals.FirstOrDefaultAsync(p => p.Id == proposal.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Spend proposal '{proposal.Id}' was not found.");
        record.FromNote = proposal.FromNote;
        record.ToNote = proposal.ToNote;
        record.Percent = proposal.Percent;
        record.Rationale = proposal.Rationale;
        record.ProposedByRole = proposal.ProposedByRole.ToString();
        record.ProposedByActor = proposal.ProposedByActor;
        record.Status = proposal.Status;
        record.RejectionReason = proposal.RejectionReason;
        record.DecidedByActor = proposal.DecidedByActor;
        record.CreatedAtUtc = proposal.CreatedAtUtc;
        record.DecidedAtUtc = proposal.DecidedAtUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SpendProposalRecord ToRecord(SpendProposal proposal)
    {
        return new SpendProposalRecord
        {
            Id = proposal.Id,
            FromNote = proposal.FromNote,
            ToNote = proposal.ToNote,
            Percent = proposal.Percent,
            Rationale = proposal.Rationale,
            ProposedByRole = proposal.ProposedByRole.ToString(),
            ProposedByActor = proposal.ProposedByActor,
            Status = proposal.Status,
            RejectionReason = proposal.RejectionReason,
            DecidedByActor = proposal.DecidedByActor,
            CreatedAtUtc = proposal.CreatedAtUtc,
            DecidedAtUtc = proposal.DecidedAtUtc
        };
    }

    private static SpendProposal ToDomain(SpendProposalRecord record)
    {
        return SpendProposal.Restore(
            record.Id,
            record.FromNote,
            record.ToNote,
            record.Percent,
            record.Rationale,
            Enum.Parse<ActorRole>(record.ProposedByRole),
            record.ProposedByActor,
            record.Status,
            record.RejectionReason,
            record.DecidedByActor,
            record.CreatedAtUtc,
            record.DecidedAtUtc);
    }
}
