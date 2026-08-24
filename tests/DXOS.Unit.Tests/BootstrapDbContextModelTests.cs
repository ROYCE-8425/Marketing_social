using DXOS.Infrastructure.Persistence;
using DXOS.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class BootstrapDbContextModelTests
{
    private static BootstrapDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseNpgsql("Host=localhost;Database=fake;Username=u;Password=p")
            .Options;

        return new BootstrapDbContext(options);
    }

    [Fact]
    public void Model_MapsRuntimeProbe_ToRuntimeProbesTable()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(RuntimeProbe));

        Assert.NotNull(entityType);
        Assert.Equal("runtime_probes", entityType.GetTableName());
    }

    [Fact]
    public void Model_RuntimeProbe_HasPrimaryKeyOnId()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(RuntimeProbe));

        Assert.NotNull(entityType);
        var primaryKey = entityType.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey.Properties);
        Assert.Equal(nameof(RuntimeProbe.Id), primaryKey.Properties[0].Name);
    }

    [Fact]
    public void Model_RuntimeProbe_RequiresProbeNameAndSetsMaxLength128()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(RuntimeProbe));

        Assert.NotNull(entityType);
        var property = entityType.FindProperty(nameof(RuntimeProbe.ProbeName));
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal(128, property.GetMaxLength());
    }

    [Fact]
    public void Model_RuntimeProbe_RequiresStatusAndSetsMaxLength64()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(RuntimeProbe));

        Assert.NotNull(entityType);
        var property = entityType.FindProperty(nameof(RuntimeProbe.Status));
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal(64, property.GetMaxLength());
    }

    [Fact]
    public void Model_RuntimeProbe_RequiresCreatedAtUtc()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(RuntimeProbe));

        Assert.NotNull(entityType);
        var property = entityType.FindProperty(nameof(RuntimeProbe.CreatedAtUtc));
        Assert.NotNull(property);
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void Model_MapsRbacAndSocialContentEntities()
    {
        using var context = CreateContext();

        var appRoleType = context.Model.FindEntityType(typeof(AppRoleRecord));
        Assert.NotNull(appRoleType);
        Assert.Equal("app_roles", appRoleType.GetTableName());

        var appUserType = context.Model.FindEntityType(typeof(AppUserRecord));
        Assert.NotNull(appUserType);
        Assert.Equal("app_users", appUserType.GetTableName());

        var postType = context.Model.FindEntityType(typeof(SocialPostRecord));
        Assert.NotNull(postType);
        Assert.Equal("social_posts", postType.GetTableName());

        var commentType = context.Model.FindEntityType(typeof(SocialCommentRecord));
        Assert.NotNull(commentType);
        Assert.Equal("social_comments", commentType.GetTableName());

        var convType = context.Model.FindEntityType(typeof(SocialConversationRecord));
        Assert.NotNull(convType);
        Assert.NotNull(convType.FindProperty(nameof(SocialConversationRecord.Status)));
        Assert.NotNull(convType.FindProperty(nameof(SocialConversationRecord.AssignedToActor)));
        Assert.NotNull(convType.FindProperty(nameof(SocialConversationRecord.InternalNote)));
        Assert.NotNull(convType.FindProperty(nameof(SocialConversationRecord.CustomerPhone)));
    }
}
