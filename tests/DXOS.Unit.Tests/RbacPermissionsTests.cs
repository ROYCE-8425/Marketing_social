using DXOS.Infrastructure;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class RbacPermissionsTests
{
    [Fact]
    public void AppPermissions_ContainsAll14RequiredPermissions()
    {
        var all = AppPermissions.All;

        Assert.Equal(14, all.Count);
        Assert.Contains(AppPermissions.InboxRead, all);
        Assert.Contains(AppPermissions.InboxReply, all);
        Assert.Contains(AppPermissions.InboxAssign, all);
        Assert.Contains(AppPermissions.InboxModerate, all);
        Assert.Contains(AppPermissions.LeadsRead, all);
        Assert.Contains(AppPermissions.LeadsConvert, all);
        Assert.Contains(AppPermissions.PagePostsRead, all);
        Assert.Contains(AppPermissions.PageCommentsRead, all);
        Assert.Contains(AppPermissions.PageCommentsReply, all);
        Assert.Contains(AppPermissions.PageInsightsRead, all);
        Assert.Contains(AppPermissions.PagePublish, all);
        Assert.Contains(AppPermissions.CampaignApprove, all);
        Assert.Contains(AppPermissions.SettingsRoles, all);
        Assert.Contains(AppPermissions.SettingsIntegrations, all);
    }

    [Fact]
    public void OwnerRole_HasAllPermissions()
    {
        var ownerPerms = AppPermissions.SeedRoles["Owner"];
        Assert.Equal(14, ownerPerms.Count);
        Assert.Contains(AppPermissions.SettingsRoles, ownerPerms);
        Assert.Contains(AppPermissions.PagePublish, ownerPerms);
        Assert.Contains(AppPermissions.PagePostsRead, ownerPerms);
    }

    [Fact]
    public void AdminRole_HasAllPermissionsExceptSettingsRoles()
    {
        var adminPerms = AppPermissions.SeedRoles["Admin"];
        Assert.Equal(13, adminPerms.Count);
        Assert.DoesNotContain(AppPermissions.SettingsRoles, adminPerms);
        Assert.Contains(AppPermissions.PagePublish, adminPerms);
        Assert.Contains(AppPermissions.CampaignApprove, adminPerms);
        Assert.Contains(AppPermissions.PagePostsRead, adminPerms);
    }

    [Fact]
    public void MarketerRole_HasExpectedPermissions()
    {
        var marketerPerms = AppPermissions.SeedRoles["Marketer"];
        Assert.Contains(AppPermissions.CampaignApprove, marketerPerms);
        Assert.Contains(AppPermissions.PagePublish, marketerPerms);
        Assert.Contains(AppPermissions.PagePostsRead, marketerPerms);
        Assert.Contains(AppPermissions.PageInsightsRead, marketerPerms);
        Assert.Contains(AppPermissions.LeadsRead, marketerPerms);
        Assert.DoesNotContain(AppPermissions.SettingsRoles, marketerPerms);
        Assert.DoesNotContain(AppPermissions.InboxReply, marketerPerms);
    }

    [Fact]
    public void SalesRole_HasInboxAndLeadPermissions_LacksPagePostsRead()
    {
        var salesPerms = AppPermissions.SeedRoles["Sales"];
        Assert.Contains(AppPermissions.InboxRead, salesPerms);
        Assert.Contains(AppPermissions.InboxReply, salesPerms);
        Assert.Contains(AppPermissions.InboxAssign, salesPerms);
        Assert.Contains(AppPermissions.InboxModerate, salesPerms);
        Assert.Contains(AppPermissions.LeadsRead, salesPerms);
        Assert.Contains(AppPermissions.LeadsConvert, salesPerms);
        Assert.DoesNotContain(AppPermissions.PagePublish, salesPerms);
        Assert.DoesNotContain(AppPermissions.PagePostsRead, salesPerms);
        Assert.DoesNotContain(AppPermissions.SettingsRoles, salesPerms);
    }

    [Fact]
    public void ViewerRole_HasReadOnlyPermissions()
    {
        var viewerPerms = AppPermissions.SeedRoles["Viewer"];
        Assert.Contains(AppPermissions.InboxRead, viewerPerms);
        Assert.Contains(AppPermissions.LeadsRead, viewerPerms);
        Assert.Contains(AppPermissions.PagePostsRead, viewerPerms);
        Assert.Contains(AppPermissions.PageInsightsRead, viewerPerms);
        Assert.DoesNotContain(AppPermissions.InboxReply, viewerPerms);
        Assert.DoesNotContain(AppPermissions.PagePublish, viewerPerms);
    }
}
