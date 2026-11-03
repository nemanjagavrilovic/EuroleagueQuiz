using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Api.Authentication;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configured = scope.ServiceProvider.GetRequiredService<IOptions<IdentityBootstrapOptions>>().Value.AdminEmails;
        foreach (var email in configured.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var user = await users.FindByEmailAsync(email.Trim());
            if (user is not null && !await users.IsInRoleAsync(user, AppRoles.Admin))
                await users.AddToRoleAsync(user, AppRoles.Admin);
        }
    }
}
