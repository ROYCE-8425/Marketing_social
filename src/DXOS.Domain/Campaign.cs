namespace DXOS.Domain;

public sealed class Campaign
{
    private Campaign()
    {
    }

    public Guid Id { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string Copy { get; private set; } = string.Empty;
    public string? CopySnapshot { get; private set; }
    public CampaignStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public string CreatedByActor { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Campaign CreateDraft(string topic, string copy, string createdByActor, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new DomainRuleException("InvalidTopic", "Campaign topic is required.");
        }

        if (string.IsNullOrWhiteSpace(createdByActor))
        {
            throw new DomainRuleException("InvalidActor", "Campaign actor is required.");
        }

        return new Campaign
        {
            Id = Guid.NewGuid(),
            Topic = topic.Trim(),
            Copy = copy ?? string.Empty,
            CopySnapshot = null,
            Status = CampaignStatus.Draft,
            CreatedByActor = createdByActor.Trim(),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public static Campaign Restore(
        Guid id,
        string topic,
        string copy,
        string? copySnapshot,
        CampaignStatus status,
        string? rejectionReason,
        DateTimeOffset? approvedAtUtc,
        string createdByActor,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new Campaign
        {
            Id = id,
            Topic = topic,
            Copy = copy,
            CopySnapshot = copySnapshot,
            Status = status,
            RejectionReason = rejectionReason,
            ApprovedAtUtc = approvedAtUtc,
            CreatedByActor = createdByActor,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    public static Campaign Restore(
        Guid id,
        string topic,
        string copy,
        CampaignStatus status,
        string createdByActor,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return Restore(id, topic, copy, null, status, null, null, createdByActor, createdAtUtc, updatedAtUtc);
    }

    public void UpdateCopy(string newCopy, DateTimeOffset nowUtc)
    {
        if (Status != CampaignStatus.Draft)
        {
            throw new DomainRuleException("InvalidTransition", "Cannot modify copy after submission. Must create a new draft.");
        }

        Copy = newCopy ?? string.Empty;
        UpdatedAtUtc = nowUtc;
    }

    public void SubmitReview(ActorRole role, DateTimeOffset nowUtc)
    {
        EnsureNotTerminal();
        if (role != ActorRole.Marketer)
        {
            throw new DomainRuleException("ForbiddenRole", "Only Marketer can submit a campaign for review.");
        }

        BrandLite.Validate(Copy);
        CopySnapshot = Copy;

        var next = Status switch
        {
            CampaignStatus.Draft => CampaignStatus.PendingReview,
            CampaignStatus.PendingReview => CampaignStatus.PendingApproval,
            _ => throw new DomainRuleException("InvalidTransition", $"Cannot submit review from {Status}.")
        };

        TransitionTo(next, nowUtc);
    }

    public void SendToOwner(ActorRole role, DateTimeOffset nowUtc)
    {
        if (role != ActorRole.Marketer)
        {
            throw new DomainRuleException("ForbiddenRole", "Only Marketer can send campaign to Owner.");
        }

        BrandLite.Validate(Copy);
        CopySnapshot = Copy;

        if (Status == CampaignStatus.PendingApproval)
        {
            return;
        }

        SubmitReview(role, nowUtc);
        if (Status == CampaignStatus.PendingReview)
        {
            SubmitReview(role, nowUtc);
        }
    }

    public void Approve(ActorRole role, DateTimeOffset nowUtc)
    {
        EnsureNotTerminal();
        if (role == ActorRole.System)
        {
            throw new DomainRuleException("ForbiddenRole", "System/AI cannot approve a campaign.");
        }

        if (role != ActorRole.Owner)
        {
            throw new DomainRuleException("ForbiddenRole", "Only Owner can approve a campaign.");
        }

        if (Status is CampaignStatus.Draft or CampaignStatus.PendingReview)
        {
            throw new DomainRuleException("InvalidTransition", "Owner cannot skip Marketer review.");
        }

        if (Status != CampaignStatus.PendingApproval)
        {
            throw new DomainRuleException("InvalidTransition", $"Cannot approve from {Status}.");
        }

        ApprovedAtUtc = nowUtc;
        TransitionTo(CampaignStatus.Published, nowUtc);
    }

    public void UndoApproval(ActorRole role, DateTimeOffset nowUtc)
    {
        if (role != ActorRole.Owner)
        {
            throw new DomainRuleException("ForbiddenRole", "Only Owner can undo campaign approval.");
        }

        if (Status != CampaignStatus.Published)
        {
            throw new DomainRuleException("InvalidTransition", $"Cannot undo approval when campaign is {Status}.");
        }

        if (ApprovedAtUtc is null || nowUtc - ApprovedAtUtc.Value > TimeSpan.FromMinutes(15))
        {
            throw new DomainRuleException("UndoWindowExpired", "The 15-minute undo window has expired.");
        }

        ApprovedAtUtc = null;
        TransitionTo(CampaignStatus.PendingApproval, nowUtc);
    }

    public void Reject(ActorRole role, string reason, DateTimeOffset nowUtc)
    {
        EnsureNotTerminal();
        if (role is not (ActorRole.Owner or ActorRole.Marketer))
        {
            throw new DomainRuleException("ForbiddenRole", "Only Owner or Marketer can reject a campaign.");
        }

        if (Status is not (CampaignStatus.PendingReview or CampaignStatus.PendingApproval))
        {
            throw new DomainRuleException("InvalidTransition", $"Cannot reject from {Status}.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("InvalidReason", "Rejection reason is required.");
        }

        RejectionReason = reason.Trim();
        TransitionTo(CampaignStatus.Rejected, nowUtc);
    }

    public void Reject(ActorRole role, DateTimeOffset nowUtc)
    {
        Reject(role, "Từ chối không nêu lý do cụ thể", nowUtc);
    }

    private void EnsureNotTerminal()
    {
        if (Status is CampaignStatus.Published or CampaignStatus.Rejected)
        {
            throw new DomainRuleException("TerminalState", $"Campaign is already {Status}.");
        }
    }

    private void TransitionTo(CampaignStatus next, DateTimeOffset nowUtc)
    {
        if (!IsAllowed(Status, next))
        {
            throw new DomainRuleException("InvalidTransition", $"Cannot transition from {Status} to {next}.");
        }

        Status = next;
        UpdatedAtUtc = nowUtc;
    }

    public static bool IsAllowed(CampaignStatus from, CampaignStatus to)
    {
        return (from, to) switch
        {
            (CampaignStatus.Draft, CampaignStatus.PendingReview) => true,
            (CampaignStatus.PendingReview, CampaignStatus.PendingApproval) => true,
            (CampaignStatus.PendingReview, CampaignStatus.Rejected) => true,
            (CampaignStatus.PendingApproval, CampaignStatus.Published) => true,
            (CampaignStatus.PendingApproval, CampaignStatus.Rejected) => true,
            (CampaignStatus.Published, CampaignStatus.PendingApproval) => true,
            _ => false
        };
    }
}
