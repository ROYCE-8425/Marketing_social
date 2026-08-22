namespace DXOS.Domain;

public sealed class Lead
{
    private readonly List<LeadSource> _sources = [];
    private readonly List<string> _reasons = [];
    private readonly List<string> _rejectedByActors = [];

    private Lead()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public LeadSource Source { get; private set; }
    public IReadOnlyList<LeadSource> Sources => _sources;
    public int Score { get; private set; }
    public LeadLabel Label { get; private set; }
    public ScoreBreakdown Breakdown { get; private set; } = new(0, 0, 0, 0, 0, 0);
    public IReadOnlyList<string> Reasons => _reasons;
    public Guid? CampaignId { get; private set; }
    public string? AssignedToActor { get; private set; }
    public DateTimeOffset? AssignedAtUtc { get; private set; }
    public string? ClaimedByActor { get; private set; }
    public DateTimeOffset? ClaimedAtUtc { get; private set; }
    public IReadOnlyList<string> RejectedByActors => _rejectedByActors;
    public string? LastRejectionReason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Lead Intake(
        string name,
        string? phone,
        string? email,
        LeadSource source,
        Guid? campaignId,
        string? assignedToActor,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("InvalidName", "Lead name is required.");
        }

        var normalizedPhone = PhoneNormalizer.Normalize(phone);
        var normalizedEmail = EmailValidator.Normalize(email);

        var (score, label, breakdown, reasons) = LeadScoring.Calculate(
            name.Trim(),
            normalizedPhone,
            normalizedEmail,
            source,
            campaignId,
            nowUtc);

        var assigned = (label is LeadLabel.Cold or LeadLabel.Junk) || string.IsNullOrWhiteSpace(assignedToActor)
            ? null
            : assignedToActor.Trim();

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Phone = normalizedPhone,
            Email = normalizedEmail,
            Source = source,
            Score = score,
            Label = label,
            Breakdown = breakdown,
            CampaignId = campaignId,
            AssignedToActor = assigned,
            AssignedAtUtc = assigned is null ? null : nowUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        lead._sources.Add(source);
        lead._reasons.AddRange(reasons);
        return lead;
    }

    public static Lead Restore(
        Guid id,
        string name,
        string? phone,
        string? email,
        LeadSource source,
        IEnumerable<LeadSource>? sources,
        int score,
        LeadLabel label,
        ScoreBreakdown? breakdown,
        IEnumerable<string>? reasons,
        Guid? campaignId,
        string? assignedToActor,
        DateTimeOffset? assignedAtUtc,
        string? claimedByActor,
        DateTimeOffset? claimedAtUtc,
        IEnumerable<string>? rejectedByActors,
        string? lastRejectionReason,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        var lead = new Lead
        {
            Id = id,
            Name = name,
            Phone = phone,
            Email = email,
            Source = source,
            Score = score,
            Label = label,
            Breakdown = breakdown ?? new ScoreBreakdown(0, 0, 0, 0, 0, score),
            CampaignId = campaignId,
            AssignedToActor = assignedToActor,
            AssignedAtUtc = assignedAtUtc,
            ClaimedByActor = claimedByActor,
            ClaimedAtUtc = claimedAtUtc,
            LastRejectionReason = lastRejectionReason,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };

        if (sources != null)
        {
            lead._sources.AddRange(sources);
        }
        else
        {
            lead._sources.Add(source);
        }

        if (reasons != null)
        {
            lead._reasons.AddRange(reasons);
        }

        if (rejectedByActors != null)
        {
            lead._rejectedByActors.AddRange(rejectedByActors);
        }

        return lead;
    }

    public static Lead Restore(
        Guid id,
        string name,
        string? phone,
        string? email,
        LeadSource source,
        int score,
        Guid? campaignId,
        string? assignedToActor,
        DateTimeOffset? assignedAtUtc,
        string? claimedByActor,
        DateTimeOffset? claimedAtUtc,
        DateTimeOffset createdAtUtc)
    {
        var label = score switch
        {
            >= 80 => LeadLabel.Hot,
            >= 50 => LeadLabel.Warm,
            >= 20 => LeadLabel.Cold,
            _ => LeadLabel.Junk
        };

        return Restore(
            id,
            name,
            phone,
            email,
            source,
            [source],
            score,
            label,
            new ScoreBreakdown(0, 0, 0, 0, 0, score),
            [],
            campaignId,
            assignedToActor,
            assignedAtUtc,
            claimedByActor,
            claimedAtUtc,
            [],
            null,
            createdAtUtc,
            createdAtUtc);
    }

    public void AddInteraction(LeadSource newSource, Guid? newCampaignId, string? newName, DateTimeOffset nowUtc)
    {
        if (!_sources.Contains(newSource))
        {
            _sources.Add(newSource);
        }

        if (newCampaignId.HasValue && CampaignId is null)
        {
            CampaignId = newCampaignId;
        }

        if (!string.IsNullOrWhiteSpace(newName) && string.IsNullOrWhiteSpace(Name))
        {
            Name = newName.Trim();
        }

        var (score, label, breakdown, reasons) = LeadScoring.Calculate(
            Name,
            Phone,
            Email,
            newSource,
            CampaignId,
            nowUtc);

        Score = score;
        Label = label;
        Breakdown = breakdown;
        _reasons.Clear();
        _reasons.AddRange(reasons);
        UpdatedAtUtc = nowUtc;
    }

    public void Claim(ActorRole role, string salesActor, DateTimeOffset nowUtc)
    {
        if (role != ActorRole.Sales)
        {
            throw new DomainRuleException("ForbiddenRole", "Only Sales can claim a lead.");
        }

        if (string.IsNullOrWhiteSpace(salesActor))
        {
            throw new DomainRuleException("InvalidActor", "Sales actor is required to claim a lead.");
        }

        ReleaseIfExpired(nowUtc);

        if (!string.IsNullOrWhiteSpace(ClaimedByActor))
        {
            throw new DomainRuleException("AlreadyClaimed", $"Lead is already claimed by {ClaimedByActor}.");
        }

        var actor = salesActor.Trim();
        ClaimedByActor = actor;
        ClaimedAtUtc = nowUtc;
        AssignedToActor = actor;
        AssignedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Reject(ActorRole role, string salesActor, string reason, string? nextSalesActor, DateTimeOffset nowUtc)
    {
        if (role != ActorRole.Sales)
        {
            throw new DomainRuleException("ForbiddenRole", "Only Sales can reject a lead.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("InvalidReason", "Lý do từ chối lead là bắt buộc.");
        }

        var actor = salesActor.Trim();
        if (!_rejectedByActors.Contains(actor, StringComparer.Ordinal))
        {
            _rejectedByActors.Add(actor);
        }

        LastRejectionReason = reason.Trim();
        ClaimedByActor = null;
        ClaimedAtUtc = null;

        if (!string.IsNullOrWhiteSpace(nextSalesActor) && Label is LeadLabel.Hot or LeadLabel.Warm)
        {
            AssignedToActor = nextSalesActor.Trim();
            AssignedAtUtc = nowUtc;
        }
        else
        {
            AssignedToActor = null;
            AssignedAtUtc = null;
        }

        UpdatedAtUtc = nowUtc;
    }

    public bool ReleaseIfExpired(DateTimeOffset nowUtc)
    {
        var startedAt = ClaimedAtUtc ?? AssignedAtUtc;
        if (startedAt is null)
        {
            return false;
        }

        if (!LeadSla.IsExpired(startedAt.Value, nowUtc, Label))
        {
            return false;
        }

        ClaimedByActor = null;
        ClaimedAtUtc = null;
        AssignedToActor = null;
        AssignedAtUtc = null;
        UpdatedAtUtc = nowUtc;
        return true;
    }

    public int? SlaRemainingSeconds(DateTimeOffset nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(ClaimedByActor))
        {
            return null;
        }

        if (AssignedAtUtc is null)
        {
            return null;
        }

        var duration = LeadSla.GetDuration(Label);
        if (duration is null)
        {
            return null;
        }

        var remaining = (int)Math.Ceiling((AssignedAtUtc.Value + duration.Value - nowUtc).TotalSeconds);
        return remaining < 0 ? 0 : remaining;
    }
}
