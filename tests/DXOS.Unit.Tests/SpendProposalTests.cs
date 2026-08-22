using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class SpendProposalTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SystemOrMarketer_CanCreateSpendProposal()
    {
        var systemProp = SpendProposal.Propose(
            ActorRole.System,
            "bot-pacing",
            "Chien dich A (CPL cao)",
            "Chien dich B (CPL thap)",
            20m,
            "Toi uu hoa chi phi CPL",
            Now);

        Assert.Equal("Pending", systemProp.Status);
        Assert.Equal(20m, systemProp.Percent);
        Assert.False(systemProp.AdsLive);

        var marketerProp = SpendProposal.Propose(
            ActorRole.Marketer,
            "mai",
            "Chien dich A",
            "Chien dich B",
            15m,
            "Dieu chuyen ngan sach",
            Now);

        Assert.Equal("Pending", marketerProp.Status);
    }

    [Theory]
    [InlineData(ActorRole.Owner)]
    [InlineData(ActorRole.Sales)]
    [InlineData(ActorRole.Content)]
    public void NonSystemOrMarketer_CannotCreateSpendProposal(ActorRole role)
    {
        var ex = Assert.Throws<DomainRuleException>(() => SpendProposal.Propose(
            role,
            "user",
            "A",
            "B",
            20m,
            "Rationale",
            Now));

        Assert.Equal("ForbiddenRole", ex.Code);
    }

    [Fact]
    public void Owner_CanApproveSpendProposal()
    {
        var proposal = SpendProposal.Propose(
            ActorRole.Marketer,
            "mai",
            "A",
            "B",
            25m,
            "Chuyen ngan sach",
            Now);

        proposal.Approve(ActorRole.Owner, "royce", Now.AddMinutes(5));
        Assert.Equal("Accepted", proposal.Status);
        Assert.Equal("royce", proposal.DecidedByActor);
        Assert.False(proposal.AdsLive);
    }

    [Fact]
    public void Owner_CanRejectSpendProposal()
    {
        var proposal = SpendProposal.Propose(
            ActorRole.System,
            "bot",
            "A",
            "B",
            30m,
            "Pacing recommendation",
            Now);

        proposal.Reject(ActorRole.Owner, "royce", "Chua can thiet", Now.AddMinutes(5));
        Assert.Equal("Rejected", proposal.Status);
        Assert.Equal("Chua can thiet", proposal.RejectionReason);
        Assert.Equal("royce", proposal.DecidedByActor);
    }

    [Theory]
    [InlineData(ActorRole.Marketer)]
    [InlineData(ActorRole.Sales)]
    [InlineData(ActorRole.System)]
    public void NonOwner_CannotApproveOrRejectSpendProposal(ActorRole role)
    {
        var proposal = SpendProposal.Propose(
            ActorRole.Marketer,
            "mai",
            "A",
            "B",
            10m,
            "Rationale",
            Now);

        var exApprove = Assert.Throws<DomainRuleException>(() => proposal.Approve(role, "user", Now));
        Assert.Equal("ForbiddenRole", exApprove.Code);

        var exReject = Assert.Throws<DomainRuleException>(() => proposal.Reject(role, "user", "reason", Now));
        Assert.Equal("ForbiddenRole", exReject.Code);
    }
}
