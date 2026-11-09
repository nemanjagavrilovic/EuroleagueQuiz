using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using EuroleagueQuiz.Api.Authentication;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    TokenService tokens,
    IAccountEmailSender emailSender,
    IOptions<ClientOptions> clientOptions,
    IOptions<GoogleOptions> googleOptions,
    IOptions<IdentityBootstrapOptions> bootstrapOptions,
    IWebHostEnvironment environment,
    TimeProvider timeProvider,
    PasswordSetupService passwordSetup) : ControllerBase
{
    private const string RefreshCookie = "pickandroll.refresh";

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = email, Email = email,
            DisplayName = request.DisplayName.Trim(), SubscriptionPlan = SubscriptionPlans.Free,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return Validation(result);
        var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Free);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return Validation(roleResult);
        }
        if (IsConfiguredAdmin(email))
        {
            var adminResult = await userManager.AddToRoleAsync(user, AppRoles.Admin);
            if (!adminResult.Succeeded) return Validation(adminResult);
        }

        try { await SendConfirmationAsync(user, cancellationToken); }
        catch (AccountEmailDeliveryException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Your account was created, but the confirmation email could not be sent. Use resend confirmation shortly." });
        }
        return StatusCode(StatusCodes.Status201Created,
            new { message = "Account created. Check your email to confirm it before signing in." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.IsDeleted) return UnauthorizedMessage();
        if (await userManager.IsLockedOutAsync(user))
            return StatusCode(StatusCodes.Status423Locked, new { message = "Account temporarily locked. Try again later." });
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return UnauthorizedMessage();
        }
        if (!await userManager.IsEmailConfirmedAsync(user))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Confirm your email before signing in." });

        await userManager.ResetAccessFailedCountAsync(user);
        if (user.MustChangePassword)
        {
            var token = EncodeToken(await userManager.GeneratePasswordResetTokenAsync(user));
            return StatusCode(StatusCodes.Status428PreconditionRequired, new
            {
                message = "You must replace your temporary password before continuing.",
                requiresPasswordChange = true,
                email = user.Email,
                token
            });
        }
        return await CompleteLoginAsync(user, cancellationToken);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized();
        var rotated = await tokens.RotateAsync(refreshToken, cancellationToken);
        if (rotated is null)
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }
        WriteRefreshCookie(rotated.Value.Session);
        return Ok(await ResponseAsync(rotated.Value.User, rotated.Value.Session));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await userManager.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(await UserResponseAsync(user));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (firstName.Length is < 1 or > 50 || lastName.Length is < 1 or > 50)
            return BadRequest(new { message = "Enter a first and last name of up to 50 characters each." });

        user.FirstName = firstName;
        user.LastName = lastName;
        user.DisplayName = $"{firstName} {lastName}";
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? Ok(await UserResponseAsync(user)) : Validation(result);
    }

    [Authorize]
    [HttpPost("password-reset-link")]
    public async Task<IActionResult> RequestPasswordResetLink(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (!await userManager.HasPasswordAsync(user))
            return BadRequest(new { message = "No portal password is set. Choose Create a portal password first." });
        if (!await userManager.IsEmailConfirmedAsync(user))
            return BadRequest(new { message = "Confirm your email before resetting your password." });

        try { await SendPasswordResetAsync(user, cancellationToken); }
        catch (AccountEmailDeliveryException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "The password reset email could not be sent. Please try again shortly." });
        }
        return Ok(new { message = "A password reset link has been sent to your email. Check your inbox and spam folder." });
    }

    [Authorize]
    [HttpPost("password-setup-link")]
    public async Task<IActionResult> RequestPasswordSetupLink(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (await userManager.HasPasswordAsync(user))
            return Conflict(new { message = "A portal password is already set. Use password reset to change it." });
        if (!await userManager.IsEmailConfirmedAsync(user))
            return BadRequest(new { message = "Confirm your email before creating a password." });

        try { await SendPasswordSetupAsync(user, cancellationToken); }
        catch (AccountEmailDeliveryException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "The password creation email could not be sent. Please try again shortly." });
        }
        return Ok(new { message = "A link to create your portal password has been sent to your email. Google sign-in will remain available." });
    }

    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        var token = DecodeToken(request.Token);
        if (user is null || user.IsDeleted || token is null)
            return BadRequest(new { message = "Invalid or expired password creation link. Request a new password email." });
        var result = await passwordSetup.SetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded) return Validation(result);
        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await tokens.RevokeAllAsync(user.Id, cancellationToken);
        DeleteRefreshCookie();
        return Ok(new { message = "Your portal password has been created. Sign in with Google or with your email and new password." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
            await tokens.RevokeAsync(refreshToken, cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpGet("google")]
    public IActionResult Google([FromQuery] string? returnUrl = "/")
    {
        if (string.IsNullOrWhiteSpace(googleOptions.Value.ClientId) || string.IsNullOrWhiteSpace(googleOptions.Value.ClientSecret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Google sign-in is not configured." });
        var callback = Url.ActionLink(nameof(GoogleCallback), values: new { returnUrl = SafeReturnUrl(returnUrl) })!;
        return Challenge(new AuthenticationProperties { RedirectUri = callback }, "Google");
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        var external = await HttpContext.AuthenticateAsync("External");
        if (!external.Succeeded || external.Principal is null) return Redirect(ClientUrl("/login?error=google"));
        var providerKey = external.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = external.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email))
            return Redirect(ClientUrl("/login?error=google-email"));

        var user = await userManager.FindByLoginAsync("Google", providerKey) ?? await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            var displayName = external.Principal.FindFirstValue(ClaimTypes.Name)?.Trim();
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName,
                FirstName = OptionalName(external.Principal.FindFirstValue(ClaimTypes.GivenName)),
                LastName = OptionalName(external.Principal.FindFirstValue(ClaimTypes.Surname)),
                SubscriptionPlan = SubscriptionPlans.Free, CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            var create = await userManager.CreateAsync(user);
            if (!create.Succeeded) return Redirect(ClientUrl("/login?error=account"));
            await userManager.AddToRoleAsync(user, AppRoles.Free);
            if (IsConfiguredAdmin(email)) await userManager.AddToRoleAsync(user, AppRoles.Admin);
        }
        if (user.IsDeleted) return Redirect(ClientUrl("/login?error=account-deleted"));
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
        if ((await userManager.GetLoginsAsync(user)).All(x => x.LoginProvider != "Google" || x.ProviderKey != providerKey))
        {
            var linked = await userManager.AddLoginAsync(user, new UserLoginInfo("Google", providerKey, "Google"));
            if (!linked.Succeeded) return Redirect(ClientUrl("/login?error=account"));
        }

        if (user.MustChangePassword)
        {
            var token = EncodeToken(await userManager.GeneratePasswordResetTokenAsync(user));
            await HttpContext.SignOutAsync("External");
            return Redirect(ClientUrl($"/change-temporary-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}"));
        }

        var session = await tokens.IssueAsync(user, cancellationToken);
        user.LastLoginAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await userManager.UpdateAsync(user);
        WriteRefreshCookie(session);
        await HttpContext.SignOutAsync("External");
        return Redirect(ClientUrl($"/auth/callback?returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}"));
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsDeleted) return BadRequest(new { message = "Invalid confirmation link." });
        var decoded = DecodeToken(token);
        if (decoded is null) return BadRequest(new { message = "Invalid confirmation link." });
        var result = await userManager.ConfirmEmailAsync(user, decoded);
        return result.Succeeded ? Ok(new { message = "Email confirmed. You can now sign in." }) : Validation(result);
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(EmailRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.IsDeleted && !await userManager.IsEmailConfirmedAsync(user))
            try { await SendConfirmationAsync(user, cancellationToken); } catch (AccountEmailDeliveryException) { }
        return Ok(new { message = "If that account needs confirmation, a new email has been sent." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.IsDeleted && await userManager.IsEmailConfirmedAsync(user))
        {
            try
            {
                if (await userManager.HasPasswordAsync(user)) await SendPasswordResetAsync(user, cancellationToken);
                else await SendPasswordSetupAsync(user, cancellationToken);
            }
            catch (AccountEmailDeliveryException) { }
        }
        return Ok(new { message = "If an eligible account exists for that email, a password email has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        var decoded = DecodeToken(request.Token);
        if (user is null || user.IsDeleted || decoded is null) return BadRequest(new { message = "Invalid or expired password reset link." });
        var result = await userManager.ResetPasswordAsync(user, decoded, request.NewPassword);
        if (!result.Succeeded) return Validation(result);
        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await tokens.RevokeAllAsync(user.Id, cancellationToken);
        DeleteRefreshCookie();
        return Ok(new { message = "Password updated. You can now sign in." });
    }

    [HttpPost("change-temporary-password")]
    public async Task<IActionResult> ChangeTemporaryPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        var decoded = DecodeToken(request.Token);
        if (user is null || user.IsDeleted || !user.MustChangePassword || decoded is null)
            return BadRequest(new { message = "This temporary-password challenge is invalid or has expired." });

        var result = await userManager.ResetPasswordAsync(user, decoded, request.NewPassword);
        if (!result.Succeeded) return Validation(result);
        user.MustChangePassword = false;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded) return Validation(update);
        await tokens.RevokeAllAsync(user.Id, cancellationToken);
        DeleteRefreshCookie();
        return Ok(new { message = "Your temporary password has been replaced. You can now sign in." });
    }

    private async Task<IActionResult> CompleteLoginAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.LastLoginAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await userManager.UpdateAsync(user);
        var session = await tokens.IssueAsync(user, cancellationToken);
        WriteRefreshCookie(session);
        return Ok(await ResponseAsync(user, session));
    }

    private async Task<object> ResponseAsync(ApplicationUser user, IssuedSession session) => new
    {
        accessToken = session.AccessToken,
        accessTokenExpiresAtUtc = session.AccessTokenExpiresAtUtc,
        user = await UserResponseAsync(user)
    };

    private async Task<AuthUserResponse> UserResponseAsync(ApplicationUser user) => new(
        user.Id, user.Email, user.DisplayName, user.FirstName, user.LastName,
        (await userManager.GetRolesAsync(user)).ToArray(), user.SubscriptionPlan,
        await userManager.HasPasswordAsync(user));

    private async Task SendPasswordResetAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = EncodeToken(await userManager.GeneratePasswordResetTokenAsync(user));
        var url = ClientUrl($"/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}");
        await emailSender.SendPasswordResetAsync(user.Email!, user.DisplayName, url, cancellationToken);
    }

    private async Task SendPasswordSetupAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = EncodeToken(await passwordSetup.GenerateTokenAsync(user));
        var url = ClientUrl($"/set-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}");
        await emailSender.SendPasswordSetupAsync(user.Email!, user.DisplayName, url, cancellationToken);
    }

    private async Task SendConfirmationAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var token = EncodeToken(await userManager.GenerateEmailConfirmationTokenAsync(user));
        var url = ClientUrl($"/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}");
        await emailSender.SendConfirmationAsync(user.Email!, user.DisplayName, url, cancellationToken);
    }

    private void WriteRefreshCookie(IssuedSession session) => Response.Cookies.Append(RefreshCookie, session.RefreshToken,
        new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict,
            Expires = session.RefreshTokenExpiresAtUtc, IsEssential = true, Path = "/api/auth" });
    private void DeleteRefreshCookie() => Response.Cookies.Delete(RefreshCookie,
        new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/api/auth" });
    private string ClientUrl(string path) => $"{clientOptions.Value.BaseUrl.TrimEnd('/')}{path}";
    private bool IsConfiguredAdmin(string email) => bootstrapOptions.Value.AdminEmails
        .Any(value => string.Equals(value?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
    private static string SafeReturnUrl(string? value) => !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.StartsWith("//") ? value : "/";
    private static string? OptionalName(string? value) => value?.Trim() is { Length: > 0 and <= 50 } name ? name : null;
    private static string EncodeToken(string value) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));
    private static string? DecodeToken(string value) { try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(value)); } catch { return null; } }
    private static IActionResult UnauthorizedMessage() => new UnauthorizedObjectResult(new { message = "Invalid email or password." });
    private static IActionResult Validation(IdentityResult result) => new BadRequestObjectResult(new
        { message = "Please correct the highlighted fields.", errors = result.Errors.Select(x => new { code = x.Code, description = x.Description }) });
}

public sealed record RegisterRequest(
    [Required, StringLength(80, MinimumLength = 2)] string DisplayName,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password);
public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public sealed record EmailRequest([Required, EmailAddress] string Email);
public sealed record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string Token,
    [Required, StringLength(128, MinimumLength = 8)] string NewPassword);
public sealed record UpdateProfileRequest(
    [Required, StringLength(50)] string FirstName,
    [Required, StringLength(50)] string LastName);
public sealed record AuthUserResponse(Guid Id, string? Email, string DisplayName, string? FirstName, string? LastName,
    IReadOnlyList<string> Roles, string SubscriptionPlan, bool HasPassword);
