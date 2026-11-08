using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace EuroleagueQuiz.Api.Authentication;

public sealed class PasswordSetupService(UserManager<ApplicationUser> users)
{
    private const string Purpose = "PickAndRoll.CreatePortalPassword";

    public Task<string> GenerateTokenAsync(ApplicationUser user) =>
        users.GenerateUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider, Purpose);

    public async Task<IdentityResult> SetPasswordAsync(ApplicationUser user, string token, string password)
    {
        if (!await users.IsEmailConfirmedAsync(user) || await users.HasPasswordAsync(user)
            || !await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider, Purpose, token))
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidPasswordSetupLink",
                Description = "This password creation link is invalid, expired, or already used. Request a new password email."
            });

        // AddPassword preserves external logins and refuses to replace an existing password.
        return await users.AddPasswordAsync(user, password);
    }
}
