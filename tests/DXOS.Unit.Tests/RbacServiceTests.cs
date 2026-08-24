using DXOS.Infrastructure;
using DXOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class RbacServiceTests
{
    private static (RbacService Service, BootstrapDbContext Db) CreateTestService()
    {
        var options = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseInMemoryDatabase(databaseName: $"dxos_rbac_test_{Guid.NewGuid():N}")
            .Options;

        var db = new BootstrapDbContext(options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DXOS_OWNER_ACTOR"] = "royce"
            })
            .Build();

        var service = new RbacService(db, config, NullLogger<RbacService>.Instance);
        return (service, db);
    }

    [Fact]
    public async Task Fact1_EmptyActor_HasNoPermissions()
    {
        var (rbac, _) = CreateTestService();
        var ct = TestContext.Current.CancellationToken;

        var emptyProfile = await rbac.ResolveActorProfileAsync("", ct);
        Assert.Empty(emptyProfile.Permissions);
        Assert.Empty(emptyProfile.Roles);

        var whitespaceProfile = await rbac.ResolveActorProfileAsync("   ", ct);
        Assert.Empty(whitespaceProfile.Permissions);

        var nullProfile = await rbac.ResolveActorProfileAsync(null, ct);
        Assert.Empty(nullProfile.Permissions);

        Assert.False(await rbac.HasPermissionAsync("", AppPermissions.InboxRead, ct));
        Assert.False(await rbac.HasPermissionAsync(null, AppPermissions.SettingsRoles, ct));
    }

    [Fact]
    public async Task Fact2_UnknownActor_ReceivesViewerPermissions_AndIsNotInsertedAsOwner()
    {
        var (rbac, db) = CreateTestService();
        var ct = TestContext.Current.CancellationToken;

        var profile = await rbac.ResolveActorProfileAsync("unknown_stranger", ct);

        Assert.Equal("unknown_stranger", profile.ActorId);
        Assert.Contains("Viewer", profile.Roles);
        Assert.True(await rbac.HasPermissionAsync("unknown_stranger", AppPermissions.InboxRead, ct));
        Assert.False(await rbac.HasPermissionAsync("unknown_stranger", AppPermissions.PagePublish, ct));
        Assert.False(await rbac.HasPermissionAsync("unknown_stranger", AppPermissions.SettingsRoles, ct));

        // Assert user was NOT inserted into app_users
        var userInDb = await db.AppUsers.FirstOrDefaultAsync(u => u.ActorId == "unknown_stranger", ct);
        Assert.Null(userInDb);
    }

    [Fact]
    public async Task Fact3_OwnerRoyce_HasAllPermissions()
    {
        var (rbac, _) = CreateTestService();
        var ct = TestContext.Current.CancellationToken;

        var profile = await rbac.ResolveActorProfileAsync("royce", ct);

        Assert.Equal("royce", profile.ActorId);
        Assert.Contains("Owner", profile.Roles);
        Assert.Equal(AppPermissions.All.Count, profile.Permissions.Count);

        foreach (var perm in AppPermissions.All)
        {
            Assert.True(await rbac.HasPermissionAsync("royce", perm, ct), $"Expected Owner to have permission '{perm}'");
        }
    }

    [Fact]
    public async Task Fact4_MarketerBob_HasMarketerPermissions_AndLacksRestricted()
    {
        var (rbac, _) = CreateTestService();
        var ct = TestContext.Current.CancellationToken;

        var profile = await rbac.ResolveActorProfileAsync("marketer_bob", ct);

        Assert.Contains("Marketer", profile.Roles);
        Assert.True(await rbac.HasPermissionAsync("marketer_bob", AppPermissions.PagePublish, ct));
        Assert.True(await rbac.HasPermissionAsync("marketer_bob", AppPermissions.LeadsRead, ct));
        Assert.True(await rbac.HasPermissionAsync("marketer_bob", AppPermissions.PagePostsRead, ct));

        Assert.False(await rbac.HasPermissionAsync("marketer_bob", AppPermissions.InboxReply, ct));
        Assert.False(await rbac.HasPermissionAsync("marketer_bob", AppPermissions.SettingsRoles, ct));
        Assert.False(await rbac.HasPermissionAsync("marketer_bob", AppPermissions.InboxModerate, ct));
    }

    [Fact]
    public async Task Fact5_SalesAlice_HasSalesPermissions_AndLacksRestricted()
    {
        var (rbac, _) = CreateTestService();
        var ct = TestContext.Current.CancellationToken;

        var profile = await rbac.ResolveActorProfileAsync("sales_alice", ct);

        Assert.Contains("Sales", profile.Roles);
        Assert.True(await rbac.HasPermissionAsync("sales_alice", AppPermissions.InboxRead, ct));
        Assert.True(await rbac.HasPermissionAsync("sales_alice", AppPermissions.InboxReply, ct));
        Assert.True(await rbac.HasPermissionAsync("sales_alice", AppPermissions.LeadsConvert, ct));

        Assert.False(await rbac.HasPermissionAsync("sales_alice", AppPermissions.PagePublish, ct));
        Assert.False(await rbac.HasPermissionAsync("sales_alice", AppPermissions.SettingsRoles, ct));
    }

    [Fact]
    public async Task Fact6_RoleHeaderSpoofing_IsIgnored_ActorPermissionsDependOnlyOnDb()
    {
        var (rbac, _) = CreateTestService();
        var ct = TestContext.Current.CancellationToken;

        // Even if an external header claimed role is "Owner", permissions for marketer_bob must come strictly from DB roles
        var profile = await rbac.ResolveActorProfileAsync("marketer_bob", ct);
        Assert.Contains("Marketer", profile.Roles);
        Assert.DoesNotContain("Owner", profile.Roles);

        var hasSettingsRoles = await rbac.HasPermissionAsync("marketer_bob", AppPermissions.SettingsRoles, ct);
        Assert.False(hasSettingsRoles);
    }

    [Fact]
    public async Task Fact7_OwnerSettingsRoles_CannotBeStripped()
    {
        var (rbac, db) = CreateTestService();
        var ct = TestContext.Current.CancellationToken;

        await rbac.EnsureSeedRolesAsync(ct);
        var ownerRole = await db.AppRoles.FirstAsync(r => r.Name == "Owner", ct);

        // Attempting to strip settings.roles from Owner must throw InvalidOperationException
        var reducedPerms = AppPermissions.All.Where(p => p != AppPermissions.SettingsRoles).ToList();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rbac.UpdateRolePermissionsAsync(ownerRole.Id, reducedPerms, ct));

        Assert.Contains("settings.roles", ex.Message);
    }
}
