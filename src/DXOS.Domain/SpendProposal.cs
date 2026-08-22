namespace DXOS.Domain;

public sealed class SpendProposal
{
    private SpendProposal()
    {
    }

    public Guid Id { get; private set; }
    public string FromNote { get; private set; } = string.Empty;
    public string ToNote { get; private set; } = string.Empty;
    public decimal Percent { get; private set; }
    public string Rationale { get; private set; } = string.Empty;
    public ActorRole ProposedByRole { get; private set; }
    public string ProposedByActor { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Pending";
    public string? RejectionReason { get; private set; }
    public string? DecidedByActor { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }
    public bool AdsLive => false;

    public static SpendProposal Propose(
        ActorRole role,
        string actor,
        string fromNote,
        string toNote,
        decimal percent,
        string rationale,
        DateTimeOffset nowUtc)
    {
        if (role is not (ActorRole.System or ActorRole.Marketer))
        {
            throw new DomainRuleException("ForbiddenRole", "Chỉ System hoặc Marketer mới được tạo đề xuất ngân sách.");
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new DomainRuleException("InvalidActor", "Tác nhân đề xuất là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(fromNote) || string.IsNullOrWhiteSpace(toNote))
        {
            throw new DomainRuleException("InvalidProposal", "Ghi chú nguồn và đích là bắt buộc.");
        }

        if (percent <= 0 || percent > 100)
        {
            throw new DomainRuleException("InvalidProposal", "Tỷ lệ phần trăm chuyển ngân sách phải từ 1 đến 100.");
        }

        return new SpendProposal
        {
            Id = Guid.NewGuid(),
            FromNote = fromNote.Trim(),
            ToNote = toNote.Trim(),
            Percent = percent,
            Rationale = rationale?.Trim() ?? string.Empty,
            ProposedByRole = role,
            ProposedByActor = actor.Trim(),
            Status = "Pending",
            CreatedAtUtc = nowUtc
        };
    }

    public static SpendProposal Restore(
        Guid id,
        string fromNote,
        string toNote,
        decimal percent,
        string rationale,
        ActorRole proposedByRole,
        string proposedByActor,
        string status,
        string? rejectionReason,
        string? decidedByActor,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? decidedAtUtc)
    {
        return new SpendProposal
        {
            Id = id,
            FromNote = fromNote,
            ToNote = toNote,
            Percent = percent,
            Rationale = rationale,
            ProposedByRole = proposedByRole,
            ProposedByActor = proposedByActor,
            Status = status,
            RejectionReason = rejectionReason,
            DecidedByActor = decidedByActor,
            CreatedAtUtc = createdAtUtc,
            DecidedAtUtc = decidedAtUtc
        };
    }

    public void Approve(ActorRole role, string actor, DateTimeOffset nowUtc)
    {
        if (role != ActorRole.Owner)
        {
            throw new DomainRuleException("ForbiddenRole", "Chỉ Owner mới có quyền phê duyệt đề xuất ngân sách.");
        }

        if (Status != "Pending")
        {
            throw new DomainRuleException("InvalidTransition", $"Không thể duyệt đề xuất ở trạng thái {Status}.");
        }

        Status = "Accepted";
        DecidedByActor = actor.Trim();
        DecidedAtUtc = nowUtc;
    }

    public void Reject(ActorRole role, string actor, string? reason, DateTimeOffset nowUtc)
    {
        if (role != ActorRole.Owner)
        {
            throw new DomainRuleException("ForbiddenRole", "Chỉ Owner mới có quyền từ chối đề xuất ngân sách.");
        }

        if (Status != "Pending")
        {
            throw new DomainRuleException("InvalidTransition", $"Không thể từ chối đề xuất ở trạng thái {Status}.");
        }

        Status = "Rejected";
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        DecidedByActor = actor.Trim();
        DecidedAtUtc = nowUtc;
    }
}
