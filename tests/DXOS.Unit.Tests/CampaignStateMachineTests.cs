using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class CampaignStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HappyPath_MarketerReviewThenOwnerPublish_DoesNotSkipStates()
    {
        var campaign = Campaign.CreateDraft("spring sale", "copy", "mai", Now);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);

        campaign.SubmitReview(ActorRole.Marketer, Now);
        Assert.Equal(CampaignStatus.PendingReview, campaign.Status);
        Assert.Equal("copy", campaign.CopySnapshot);

        campaign.SubmitReview(ActorRole.Marketer, Now);
        Assert.Equal(CampaignStatus.PendingApproval, campaign.Status);

        campaign.Approve(ActorRole.Owner, Now);
        Assert.Equal(CampaignStatus.Published, campaign.Status);
        Assert.Equal(Now, campaign.ApprovedAtUtc);
    }

    [Fact]
    public void Owner_CannotSkipMarketerReview_FromDraftOrPendingReview()
    {
        var draft = Campaign.CreateDraft("topic", "copy", "owner-1", Now);
        var draftEx = Assert.Throws<DomainRuleException>(() => draft.Approve(ActorRole.Owner, Now));
        Assert.Equal("InvalidTransition", draftEx.Code);

        draft.SubmitReview(ActorRole.Marketer, Now);
        Assert.Equal(CampaignStatus.PendingReview, draft.Status);
        var reviewEx = Assert.Throws<DomainRuleException>(() => draft.Approve(ActorRole.Owner, Now));
        Assert.Equal("InvalidTransition", reviewEx.Code);
        Assert.Contains("skip Marketer review", reviewEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void System_CannotApprove()
    {
        var campaign = Campaign.CreateDraft("topic", "copy", "bot", Now);
        campaign.SubmitReview(ActorRole.Marketer, Now);
        campaign.SubmitReview(ActorRole.Marketer, Now);

        var ex = Assert.Throws<DomainRuleException>(() => campaign.Approve(ActorRole.System, Now));
        Assert.Equal("ForbiddenRole", ex.Code);
        Assert.Equal(CampaignStatus.PendingApproval, campaign.Status);
    }

    [Fact]
    public void CannotJump_DraftToPublishedOrPendingApproval()
    {
        Assert.False(Campaign.IsAllowed(CampaignStatus.Draft, CampaignStatus.Published));
        Assert.False(Campaign.IsAllowed(CampaignStatus.Draft, CampaignStatus.PendingApproval));
        Assert.False(Campaign.IsAllowed(CampaignStatus.PendingReview, CampaignStatus.Published));
        Assert.True(Campaign.IsAllowed(CampaignStatus.Draft, CampaignStatus.PendingReview));
        Assert.True(Campaign.IsAllowed(CampaignStatus.PendingReview, CampaignStatus.PendingApproval));
        Assert.True(Campaign.IsAllowed(CampaignStatus.PendingApproval, CampaignStatus.Published));
        Assert.True(Campaign.IsAllowed(CampaignStatus.Published, CampaignStatus.PendingApproval));
    }

    [Fact]
    public void SendToOwner_FromDraft_ReachesPendingApproval_WithoutSkippingOwnerGate()
    {
        var campaign = Campaign.CreateDraft("topic", "copy", "mai", Now);
        campaign.SendToOwner(ActorRole.Marketer, Now);
        Assert.Equal(CampaignStatus.PendingApproval, campaign.Status);
        var skip = Assert.Throws<DomainRuleException>(() => campaign.Approve(ActorRole.Marketer, Now));
        Assert.Equal("ForbiddenRole", skip.Code);
        campaign.Approve(ActorRole.Owner, Now);
        Assert.Equal(CampaignStatus.Published, campaign.Status);
    }

    [Fact]
    public void Owner_CannotSubmitReview()
    {
        var campaign = Campaign.CreateDraft("topic", "copy", "owner-1", Now);
        var ex = Assert.Throws<DomainRuleException>(() => campaign.SubmitReview(ActorRole.Owner, Now));
        Assert.Equal("ForbiddenRole", ex.Code);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
    }

    [Fact]
    public void Reject_WithReason_FromReviewOrApproval_IsTerminal()
    {
        var campaign = Campaign.CreateDraft("topic", "copy", "mai", Now);
        campaign.SubmitReview(ActorRole.Marketer, Now);
        campaign.Reject(ActorRole.Marketer, "Chưa đạt tiêu chuẩn thông điệp", Now);
        Assert.Equal(CampaignStatus.Rejected, campaign.Status);
        Assert.Equal("Chưa đạt tiêu chuẩn thông điệp", campaign.RejectionReason);

        var ex = Assert.Throws<DomainRuleException>(() => campaign.Approve(ActorRole.Owner, Now));
        Assert.Equal("TerminalState", ex.Code);
    }

    [Fact]
    public void Reject_MissingReason_ThrowsInvalidReason()
    {
        var campaign = Campaign.CreateDraft("topic", "copy", "mai", Now);
        campaign.SubmitReview(ActorRole.Marketer, Now);
        var ex = Assert.Throws<DomainRuleException>(() => campaign.Reject(ActorRole.Marketer, "   ", Now));
        Assert.Equal("InvalidReason", ex.Code);
        Assert.Equal(CampaignStatus.PendingReview, campaign.Status);
    }

    [Fact]
    public void UndoApproval_Within15Minutes_RevertsToPendingApproval()
    {
        var campaign = Campaign.CreateDraft("topic", "copy", "mai", Now);
        campaign.SendToOwner(ActorRole.Marketer, Now);
        campaign.Approve(ActorRole.Owner, Now);
        Assert.Equal(CampaignStatus.Published, campaign.Status);

        campaign.UndoApproval(ActorRole.Owner, Now.AddMinutes(14));
        Assert.Equal(CampaignStatus.PendingApproval, campaign.Status);
        Assert.Null(campaign.ApprovedAtUtc);
    }

    [Fact]
    public void UndoApproval_After15Minutes_ThrowsUndoWindowExpired()
    {
        var campaign = Campaign.CreateDraft("topic", "copy", "mai", Now);
        campaign.SendToOwner(ActorRole.Marketer, Now);
        campaign.Approve(ActorRole.Owner, Now);

        var ex = Assert.Throws<DomainRuleException>(() => campaign.UndoApproval(ActorRole.Owner, Now.AddMinutes(15).AddSeconds(1)));
        Assert.Equal("UndoWindowExpired", ex.Code);
        Assert.Equal(CampaignStatus.Published, campaign.Status);
    }

    [Fact]
    public void UndoApproval_NonOwner_ThrowsForbiddenRole()
    {
        var campaign = Campaign.CreateDraft("topic", "copy", "mai", Now);
        campaign.SendToOwner(ActorRole.Marketer, Now);
        campaign.Approve(ActorRole.Owner, Now);

        var ex = Assert.Throws<DomainRuleException>(() => campaign.UndoApproval(ActorRole.Marketer, Now.AddMinutes(5)));
        Assert.Equal("ForbiddenRole", ex.Code);
    }

    [Fact]
    public void ModifyCopy_AfterSubmission_ThrowsInvalidTransition()
    {
        var campaign = Campaign.CreateDraft("topic", "initial copy", "mai", Now);
        campaign.SubmitReview(ActorRole.Marketer, Now);

        var ex = Assert.Throws<DomainRuleException>(() => campaign.UpdateCopy("new modified copy", Now));
        Assert.Equal("InvalidTransition", ex.Code);
        Assert.Equal("initial copy", campaign.Copy);
    }

    [Fact]
    public void SubmitReview_WithBrandProhibitedTerm_ThrowsBrandBlocked()
    {
        var campaign = Campaign.CreateDraft("topic", "Quảng cáo cam kết 100% hiệu quả", "mai", Now);
        var ex = Assert.Throws<DomainRuleException>(() => campaign.SubmitReview(ActorRole.Marketer, Now));
        Assert.Equal("BrandBlocked", ex.Code);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
    }
}
