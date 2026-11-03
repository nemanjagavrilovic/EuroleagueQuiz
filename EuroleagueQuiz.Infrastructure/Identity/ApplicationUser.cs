using Microsoft.AspNetCore.Identity;

namespace EuroleagueQuiz.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string SubscriptionPlan { get; set; } = SubscriptionPlans.Free;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public ICollection<UserRefreshToken> RefreshTokens { get; set; } = new List<UserRefreshToken>();
}

public static class AppRoles
{
    public const string Free = "Free";
    public const string Admin = "Admin";
    public static readonly string[] All = [Free, Admin];
}

public static class SubscriptionPlans
{
    public const string Free = "Free";
}
