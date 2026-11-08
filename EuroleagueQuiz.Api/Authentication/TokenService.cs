using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EuroleagueQuiz.Infrastructure.Identity;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EuroleagueQuiz.Api.Authentication;

public sealed record IssuedSession(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

public sealed class TokenService(
    UserManager<ApplicationUser> userManager,
    EuroleagueDbContext db,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider)
{
    private readonly JwtOptions settings = options.Value;

    public async Task<IssuedSession> IssueAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var accessExpires = now.AddMinutes(Math.Clamp(settings.AccessTokenMinutes, 5, 60));
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new("plan", user.SubscriptionPlan)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, accessExpires,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshExpires = now.AddDays(Math.Clamp(settings.RefreshTokenDays, 1, 90));
        db.UserRefreshTokens.Add(new UserRefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = Hash(refreshToken),
            CreatedAtUtc = now, ExpiresAtUtc = refreshExpires
        });
        await db.SaveChangesAsync(cancellationToken);
        return new(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    public async Task<(ApplicationUser User, IssuedSession Session)?> RotateAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await db.UserRefreshTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (existing is null || existing.RevokedAtUtc is not null || existing.ExpiresAtUtc <= now)
            return null;

        var claimed = await db.UserRefreshTokens
            .Where(x => x.Id == existing.Id && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevokedAtUtc, now), cancellationToken);
        if (claimed != 1) return null;
        var session = await IssueAsync(existing.User, cancellationToken);
        existing.ReplacedByTokenHash = Hash(session.RefreshToken);
        await db.SaveChangesAsync(cancellationToken);
        return (existing.User, session);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var token = await db.UserRefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (token is null || token.RevokedAtUtc is not null) return;
        token.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await db.UserRefreshTokens.Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAtUtc, now), cancellationToken);
    }
}
