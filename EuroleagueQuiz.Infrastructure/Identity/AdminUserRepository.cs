using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Identity;

public sealed record AdminUserListRecord(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    bool HasPassword,
    bool MustChangePassword,
    string SubscriptionPlan,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    bool IsDeleted,
    DateTime? DeletedAtUtc,
    string[] Roles);

public sealed record AdminUserPageRecord(IReadOnlyList<AdminUserListRecord> Users, int TotalCount);

public interface IAdminUserRepository
{
    Task<AdminUserPageRecord> GetPageAsync(string? search, int page, int pageSize, bool includeDeleted, CancellationToken ct);
    Task<ApplicationUser?> FindAsync(Guid id, CancellationToken ct);
    Task<string[]> GetRolesAsync(Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class AdminUserRepository(EuroleagueDbContext db) : IAdminUserRepository
{
    public async Task<AdminUserPageRecord> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        bool includeDeleted,
        CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!includeDeleted) query = query.Where(user => !user.IsDeleted);

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(user =>
                EF.Functions.ILike(user.Email ?? string.Empty, pattern)
                || EF.Functions.ILike(user.DisplayName, pattern));
        }

        var totalCount = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(user => user.CreatedAtUtc)
            .ThenBy(user => user.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new
            {
                user.Id,
                Email = user.Email ?? string.Empty,
                user.DisplayName,
                user.EmailConfirmed,
                HasPassword = user.PasswordHash != null,
                user.MustChangePassword,
                user.SubscriptionPlan,
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                user.IsDeleted,
                user.DeletedAtUtc
            })
            .ToListAsync(ct);

        var userIds = users.Select(user => user.Id).ToArray();
        var roleRows = await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, Role = role.Name ?? string.Empty })
            .ToListAsync(ct);
        var roles = roleRows
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Role).Where(role => role.Length > 0).Order().ToArray());

        return new AdminUserPageRecord(users.Select(user => new AdminUserListRecord(
            user.Id,
            user.Email,
            user.DisplayName,
            user.EmailConfirmed,
            user.HasPassword,
            user.MustChangePassword,
            user.SubscriptionPlan,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            user.IsDeleted,
            user.DeletedAtUtc,
            roles.GetValueOrDefault(user.Id, []))).ToList(), totalCount);
    }

    public Task<ApplicationUser?> FindAsync(Guid id, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(user => user.Id == id, ct);

    public async Task<string[]> GetRolesAsync(Guid id, CancellationToken ct) =>
        await (
            from userRole in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == id && role.Name != null
            orderby role.Name
            select role.Name!)
            .ToArrayAsync(ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
