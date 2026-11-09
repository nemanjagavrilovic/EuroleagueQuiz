using System.Text;
using System.Security.Cryptography;
using EuroleagueQuiz.Api.Authentication;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Api.Administration;

public sealed record AdminUserPageResponse(
    IReadOnlyList<AdminUserListRecord> Users,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed class AdminUserActionException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class AdminUserService(
    IAdminUserRepository users,
    UserManager<ApplicationUser> userManager,
    TokenService tokens,
    IAccountEmailSender emailSender,
    IOptions<ClientOptions> clientOptions,
    TimeProvider timeProvider)
{
    public async Task<AdminUserPageResponse> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        bool includeDeleted,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        var result = await users.GetPageAsync(search, page, pageSize, includeDeleted, ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)pageSize));
        if (page > totalPages)
        {
            page = totalPages;
            result = await users.GetPageAsync(search, page, pageSize, includeDeleted, ct);
        }
        return new(result.Users, page, pageSize, result.TotalCount, totalPages);
    }

    public async Task<string> SoftDeleteAsync(Guid administratorId, Guid userId, CancellationToken ct)
    {
        if (administratorId == userId)
            throw new AdminUserActionException(StatusCodes.Status409Conflict, "You cannot delete your own administrator account.");

        var user = await RequiredActiveUserAsync(userId, ct);
        var roles = await users.GetRolesAsync(userId, ct);
        if (roles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase))
            throw new AdminUserActionException(StatusCodes.Status409Conflict, "Administrator accounts cannot be deleted from this page.");

        user.IsDeleted = true;
        user.DeletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        user.DeletedByUserId = administratorId;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        var update = await userManager.UpdateSecurityStampAsync(user);
        if (!update.Succeeded)
            throw new AdminUserActionException(StatusCodes.Status500InternalServerError, "The account could not be deleted.");

        await users.SaveAsync(ct);
        await tokens.RevokeAllAsync(userId, ct);
        return "The account has been logically deleted and all active sessions were revoked.";
    }

    public async Task<string> SendPasswordEmailAsync(Guid userId, CancellationToken ct)
    {
        var user = await RequiredActiveUserAsync(userId, ct);
        if (!user.EmailConfirmed)
            throw new AdminUserActionException(StatusCodes.Status409Conflict, "Confirm the user's email before sending a password link.");
        if (string.IsNullOrWhiteSpace(user.Email))
            throw new AdminUserActionException(StatusCodes.Status409Conflict, "This account does not have an email address.");

        var temporaryPassword = GenerateTemporaryPassword();
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, temporaryPassword);
        if (!reset.Succeeded)
            throw new AdminUserActionException(StatusCodes.Status500InternalServerError, "A temporary password could not be created.");

        user.MustChangePassword = true;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
            throw new AdminUserActionException(StatusCodes.Status500InternalServerError, "The temporary-password requirement could not be saved.");

        await tokens.RevokeAllAsync(user.Id, ct);
        await emailSender.SendTemporaryPasswordAsync(user.Email, user.DisplayName, temporaryPassword, ClientUrl("/login"), ct);
        return "A temporary password has been emailed to the user. They must replace it after signing in.";
    }

    public async Task<string> ResendConfirmationAsync(Guid userId, CancellationToken ct)
    {
        var user = await RequiredActiveUserAsync(userId, ct);
        if (user.EmailConfirmed)
            throw new AdminUserActionException(StatusCodes.Status409Conflict, "This email address is already confirmed.");
        if (string.IsNullOrWhiteSpace(user.Email))
            throw new AdminUserActionException(StatusCodes.Status409Conflict, "This account does not have an email address.");

        var token = EncodeToken(await userManager.GenerateEmailConfirmationTokenAsync(user));
        var url = ClientUrl($"/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}");
        await emailSender.SendConfirmationAsync(user.Email, user.DisplayName, url, ct);
        return "A new confirmation email has been sent to the user.";
    }

    private async Task<ApplicationUser> RequiredActiveUserAsync(Guid id, CancellationToken ct)
    {
        var user = await users.FindAsync(id, ct);
        if (user is null)
            throw new AdminUserActionException(StatusCodes.Status404NotFound, "User not found.");
        if (user.IsDeleted)
            throw new AdminUserActionException(StatusCodes.Status409Conflict, "This account has already been deleted.");
        return user;
    }

    private string ClientUrl(string path) => $"{clientOptions.Value.BaseUrl.TrimEnd('/')}{path}";
    private static string EncodeToken(string value) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;
        var characters = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)]
        };
        while (characters.Count < 14) characters.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        for (var index = characters.Count - 1; index > 0; index--)
        {
            var swap = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swap]) = (characters[swap], characters[index]);
        }
        return new string(characters.ToArray());
    }
}
