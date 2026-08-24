using System.Text.Json;
using DXOS.Infrastructure.Persistence;
using DXOS.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DXOS.Infrastructure;

public static class AppPermissions
{
    public const string InboxRead = "inbox.read";
    public const string InboxReply = "inbox.reply";
    public const string InboxAssign = "inbox.assign";
    public const string InboxModerate = "inbox.moderate";
    public const string LeadsRead = "leads.read";
    public const string LeadsConvert = "leads.convert";
    public const string PagePostsRead = "page.posts.read";
    public const string PageCommentsRead = "page.comments.read";
    public const string PageCommentsReply = "page.comments.reply";
    public const string PageInsightsRead = "page.insights.read";
    public const string PagePublish = "page.publish";
    public const string CampaignApprove = "campaign.approve";
    public const string SettingsRoles = "settings.roles";
    public const string SettingsIntegrations = "settings.integrations";

    public static readonly IReadOnlyList<string> All =
    [
        InboxRead, InboxReply, InboxAssign, InboxModerate,
        LeadsRead, LeadsConvert,
        PagePostsRead, PageCommentsRead, PageCommentsReply, PageInsightsRead, PagePublish,
        CampaignApprove, SettingsRoles, SettingsIntegrations
    ];

    public static readonly Dictionary<string, IReadOnlyList<string>> SeedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Owner"] = All,
        ["Admin"] = All.Where(p => p != SettingsRoles).ToList(),
        ["Marketer"] = [LeadsRead, CampaignApprove, PagePostsRead, PageInsightsRead, PagePublish],
        ["Content"] = [PagePostsRead, PageCommentsRead, PageCommentsReply, PagePublish],
        ["Sales"] = [InboxRead, InboxReply, InboxAssign, InboxModerate, LeadsRead, LeadsConvert],
        ["Viewer"] = [InboxRead, LeadsRead, PagePostsRead, PageInsightsRead]
    };
}

public sealed record ActorAuthProfile(
    string ActorId,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed class RbacService
{
    private readonly BootstrapDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<RbacService> _logger;

    public RbacService(BootstrapDbContext db, IConfiguration config, ILogger<RbacService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task EnsureSeedRolesAsync(CancellationToken ct = default)
    {
        foreach (var (roleName, perms) in AppPermissions.SeedRoles)
        {
            var role = await _db.AppRoles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
            if (role is null)
            {
                role = new AppRoleRecord
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    Description = $"Hệ thống mặc định cho vai trò {roleName}",
                    IsSystem = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                _db.AppRoles.Add(role);
                await _db.SaveChangesAsync(ct);
            }

            var existingPerms = await _db.AppRolePermissions
                .Where(p => p.RoleId == role.Id)
                .Select(p => p.Permission)
                .ToListAsync(ct);

            var toAdd = perms.Except(existingPerms, StringComparer.OrdinalIgnoreCase);
            foreach (var perm in toAdd)
            {
                _db.AppRolePermissions.Add(new AppRolePermissionRecord
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    Permission = perm
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        await EnsureSeedUsersAsync(ct);
    }

    public async Task EnsureSeedUsersAsync(CancellationToken ct = default)
    {
        var seedUsers = new (string ActorId, string DisplayName, string RoleName)[]
        {
            ("royce", "Chủ Doanh Nghiệp (Royce)", "Owner"),
            ("admin_user", "Quản trị viên (Admin)", "Admin"),
            ("marketer_bob", "Bob Marketer", "Marketer"),
            ("content_carol", "Carol Content", "Content"),
            ("sales_alice", "Alice Sales", "Sales"),
            ("viewer_dan", "Dan Viewer", "Viewer"),
        };

        foreach (var (actorId, displayName, roleName) in seedUsers)
        {
            var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.ActorId == actorId, ct);
            if (user is null)
            {
                user = new AppUserRecord
                {
                    Id = Guid.NewGuid(),
                    ActorId = actorId,
                    DisplayName = displayName,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                _db.AppUsers.Add(user);
                await _db.SaveChangesAsync(ct);
            }

            var hasRoles = await _db.AppUserRoles.AnyAsync(ur => ur.UserId == user.Id, ct);
            if (!hasRoles)
            {
                var role = await _db.AppRoles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
                if (role is not null)
                {
                    _db.AppUserRoles.Add(new AppUserRoleRecord
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        RoleId = role.Id
                    });
                    await _db.SaveChangesAsync(ct);
                }
            }
        }
    }

    public async Task<ActorAuthProfile> ResolveActorProfileAsync(string? actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return new ActorAuthProfile(string.Empty, "Anonymous", [], []);
        }

        await EnsureSeedRolesAsync(ct);

        var cleanActorId = actorId.Trim();
        var ownerActorEnv = _config["DXOS_OWNER_ACTOR"] ?? "royce";

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.ActorId == cleanActorId, ct);

        // Auto-bootstrap Owner ONLY when actorId matches DXOS_OWNER_ACTOR and user is not yet in app_users
        if (user is null && string.Equals(cleanActorId, ownerActorEnv, StringComparison.OrdinalIgnoreCase))
        {
            user = new AppUserRecord
            {
                Id = Guid.NewGuid(),
                ActorId = cleanActorId,
                DisplayName = string.Equals(cleanActorId, "royce", StringComparison.OrdinalIgnoreCase) ? "Chủ Doanh Nghiệp (Royce)" : cleanActorId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.AppUsers.Add(user);
            await _db.SaveChangesAsync(ct);

            var ownerRole = await _db.AppRoles.FirstOrDefaultAsync(r => r.Name == "Owner", ct);
            if (ownerRole is not null)
            {
                _db.AppUserRoles.Add(new AppUserRoleRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RoleId = ownerRole.Id
                });
                await _db.SaveChangesAsync(ct);
            }
        }

        if (user is not null)
        {
            var roleIds = await _db.AppUserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleId)
                .ToListAsync(ct);

            var roles = await _db.AppRoles
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToListAsync(ct);

            var permissions = await _db.AppRolePermissions
                .Where(p => roleIds.Contains(p.RoleId))
                .Select(p => p.Permission)
                .Distinct()
                .ToListAsync(ct);

            return new ActorAuthProfile(user.ActorId, user.DisplayName, roles, permissions);
        }

        // Unknown actor -> Viewer permissions in-memory, DO NOT insert into DB as Owner
        var viewerRole = await _db.AppRoles.FirstOrDefaultAsync(r => r.Name == "Viewer", ct);
        var viewerPerms = viewerRole is not null
            ? await _db.AppRolePermissions.Where(p => p.RoleId == viewerRole.Id).Select(p => p.Permission).ToListAsync(ct)
            : AppPermissions.SeedRoles["Viewer"];

        return new ActorAuthProfile(cleanActorId, cleanActorId, ["Viewer"], viewerPerms);
    }

    public async Task<bool> HasPermissionAsync(string? actorId, string permission, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return false;
        }

        var profile = await ResolveActorProfileAsync(actorId, ct);
        return profile.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    public async Task LogAuditAsync(string actorId, string permission, string action, string entityId, string? details = null, CancellationToken ct = default)
    {
        try
        {
            var log = new AppAuditLogRecord
            {
                Id = Guid.NewGuid(),
                ActorId = actorId,
                Permission = permission,
                Action = action,
                EntityId = entityId,
                Details = details,
                TimestampUtc = DateTimeOffset.UtcNow
            };
            _db.AppAuditLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for actor {Actor}", actorId);
        }
    }

    public async Task<List<object>> ListRolesAsync(CancellationToken ct = default)
    {
        await EnsureSeedRolesAsync(ct);
        var roles = await _db.AppRoles.OrderBy(r => r.Name).ToListAsync(ct);
        var roleIds = roles.Select(r => r.Id).ToList();

        var perms = await _db.AppRolePermissions
            .Where(p => roleIds.Contains(p.RoleId))
            .ToListAsync(ct);

        return roles.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            description = r.Description,
            is_system = r.IsSystem,
            permissions = perms.Where(p => p.RoleId == r.Id).Select(p => p.Permission).ToList(),
            created_at = r.CreatedAtUtc
        }).Cast<object>().ToList();
    }

    public async Task<AppRoleRecord> CreateRoleAsync(string name, string description, IEnumerable<string> permissions, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên vai trò không được để trống.", nameof(name));

        var cleanName = name.Trim();
        var exists = await _db.AppRoles.AnyAsync(r => r.Name.ToLower() == cleanName.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"Vai trò '{cleanName}' đã tồn tại.");

        var role = new AppRoleRecord
        {
            Id = Guid.NewGuid(),
            Name = cleanName,
            Description = description?.Trim() ?? string.Empty,
            IsSystem = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        _db.AppRoles.Add(role);
        await _db.SaveChangesAsync(ct);

        foreach (var p in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (AppPermissions.All.Contains(p, StringComparer.OrdinalIgnoreCase))
            {
                _db.AppRolePermissions.Add(new AppRolePermissionRecord
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    Permission = p
                });
            }
        }
        await _db.SaveChangesAsync(ct);
        return role;
    }

    public async Task UpdateRolePermissionsAsync(Guid roleId, IEnumerable<string> permissions, CancellationToken ct = default)
    {
        var role = await _db.AppRoles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null)
            throw new KeyNotFoundException($"Không tìm thấy vai trò với ID {roleId}");

        var permList = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Protect Owner role from removing settings.roles
        if (string.Equals(role.Name, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            if (!permList.Contains(AppPermissions.SettingsRoles, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Không thể gỡ quyền settings.roles khỏi vai trò Owner.");
            }
        }

        var existing = await _db.AppRolePermissions.Where(p => p.RoleId == roleId).ToListAsync(ct);
        _db.AppRolePermissions.RemoveRange(existing);

        foreach (var p in permList)
        {
            if (AppPermissions.All.Contains(p, StringComparer.OrdinalIgnoreCase))
            {
                _db.AppRolePermissions.Add(new AppRolePermissionRecord
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.Id,
                    Permission = p
                });
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AppUserRecord> AssignUserRolesAsync(string actorId, string? displayName, IEnumerable<string> roleNames, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("ActorId không được để trống.", nameof(actorId));

        var cleanActor = actorId.Trim();
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.ActorId == cleanActor, ct);
        if (user is null)
        {
            user = new AppUserRecord
            {
                Id = Guid.NewGuid(),
                ActorId = cleanActor,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? cleanActor : displayName.Trim(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            _db.AppUsers.Add(user);
            await _db.SaveChangesAsync(ct);
        }
        else if (!string.IsNullOrWhiteSpace(displayName))
        {
            user.DisplayName = displayName.Trim();
            user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        var existingUserRoles = await _db.AppUserRoles.Where(ur => ur.UserId == user.Id).ToListAsync(ct);
        _db.AppUserRoles.RemoveRange(existingUserRoles);

        var roles = await _db.AppRoles
            .Where(r => roleNames.Contains(r.Name))
            .ToListAsync(ct);

        foreach (var r in roles)
        {
            _db.AppUserRoles.Add(new AppUserRoleRecord
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleId = r.Id
            });
        }

        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<List<object>> ListUsersAsync(CancellationToken ct = default)
    {
        await EnsureSeedRolesAsync(ct);
        var users = await _db.AppUsers.OrderBy(u => u.ActorId).ToListAsync(ct);
        var userIds = users.Select(u => u.Id).ToList();

        var userRoles = await _db.AppUserRoles.Where(ur => userIds.Contains(ur.UserId)).ToListAsync(ct);
        var roleIds = userRoles.Select(ur => ur.RoleId).Distinct().ToList();
        var roles = await _db.AppRoles.Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        return users.Select(u =>
        {
            var uRoles = userRoles.Where(ur => ur.UserId == u.Id)
                .Select(ur => roles.TryGetValue(ur.RoleId, out var rName) ? rName : null)
                .Where(rName => rName != null)
                .ToList();

            return (object)new
            {
                id = u.Id,
                actor_id = u.ActorId,
                display_name = u.DisplayName,
                roles = uRoles,
                created_at = u.CreatedAtUtc,
                updated_at = u.UpdatedAtUtc
            };
        }).ToList();
    }
}
