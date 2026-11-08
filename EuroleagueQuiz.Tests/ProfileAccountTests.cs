using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EuroleagueQuiz.Api.Authentication;
using EuroleagueQuiz.Api.Controllers;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Tests;

public sealed class ProfileAccountTests
{
    [Fact]
    public async Task Profile_update_changes_only_the_authenticated_users_names()
    {
        var user = Account();
        using var manager = new StubUserManager(user);
        var controller = Controller(manager, new RecordingEmailSender());
        var request = JsonSerializer.Deserialize<UpdateProfileRequest>("""
            {"firstName":"  Nemanja  ","lastName":"Gavrilović  ","email":"other@example.test",
             "id":"00000000-0000-0000-0000-000000000001","roles":["Admin"],"subscriptionPlan":"Paid"}
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var result = Assert.IsType<OkObjectResult>(await controller.UpdateProfile(request));
        var response = Assert.IsType<AuthUserResponse>(result.Value);

        Assert.Equal("Nemanja", response.FirstName);
        Assert.Equal("Gavrilović", response.LastName);
        Assert.Equal("Nemanja Gavrilović", response.DisplayName);
        Assert.Equal(user.Id, response.Id);
        Assert.Equal("player+test@example.test", user.Email);
        Assert.Equal("player+test@example.test", user.UserName);
        Assert.Equal([AppRoles.Free], response.Roles);
        Assert.Equal(SubscriptionPlans.Free, user.SubscriptionPlan);
        Assert.Equal(1, manager.Updates);
    }

    [Theory]
    [InlineData("   ", "Doe")]
    [InlineData("Jane", "   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "Doe")]
    public async Task Invalid_names_are_rejected_without_updating_the_user(string firstName, string lastName)
    {
        var user = Account();
        using var manager = new StubUserManager(user);
        var result = await Controller(manager, new RecordingEmailSender()).UpdateProfile(new(firstName, lastName));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, manager.Updates);
        Assert.Equal("Player", user.DisplayName);
    }

    [Fact]
    public async Task Signed_out_requests_cannot_update_a_profile_or_send_reset_emails()
    {
        using var manager = new StubUserManager(Account());
        var email = new RecordingEmailSender();
        var controller = Controller(manager, email, signedIn: false);

        Assert.IsType<UnauthorizedResult>(await controller.UpdateProfile(new("Jane", "Doe")));
        Assert.IsType<UnauthorizedResult>(await controller.RequestPasswordResetLink(default));
        Assert.Equal(0, manager.Updates);
        Assert.Empty(email.Messages);
    }

    [Fact]
    public async Task Reset_email_goes_to_the_authenticated_account_with_a_url_safe_identity_token()
    {
        var user = Account();
        using var manager = new StubUserManager(user);
        var email = new RecordingEmailSender();

        Assert.IsType<OkObjectResult>(await Controller(manager, email).RequestPasswordResetLink(default));
        var message = Assert.Single(email.Messages);
        Assert.Equal(user.Email, message.Email);
        var url = new Uri(message.Url);
        Assert.Equal("https://pickandroll.test/reset-password", url.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(url.Query);
        Assert.Equal(user.Email, query["email"].ToString());
        Assert.Equal(StubUserManager.ResetToken, Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(query["token"].ToString())));
    }

    [Fact]
    public async Task Google_only_accounts_can_request_password_creation_without_resetting_a_password()
    {
        var user = Account();
        user.PasswordHash = null;
        using var manager = new StubUserManager(user);
        var email = new RecordingEmailSender();
        var controller = Controller(manager, email);

        var me = Assert.IsType<OkObjectResult>(await controller.Me());
        Assert.False(Assert.IsType<AuthUserResponse>(me.Value).HasPassword);
        Assert.IsType<BadRequestObjectResult>(await controller.RequestPasswordResetLink(default));
        Assert.Empty(email.Messages);
        Assert.IsType<OkObjectResult>(await controller.RequestPasswordSetupLink(default));
        var setup = Assert.Single(email.SetupMessages);
        Assert.Equal(user.Email, setup.Email);
        Assert.Equal("/set-password", new Uri(setup.Url).AbsolutePath);
    }

    [Fact]
    public async Task Existing_password_accounts_cannot_request_first_password_creation()
    {
        using var manager = new StubUserManager(Account());
        var email = new RecordingEmailSender();
        Assert.IsType<ConflictObjectResult>(await Controller(manager, email).RequestPasswordSetupLink(default));
        Assert.Empty(email.SetupMessages);
    }

    [Fact]
    public async Task Public_password_recovery_sends_a_setup_email_for_a_confirmed_google_account()
    {
        var user = Account();
        user.PasswordHash = null;
        using var manager = new StubUserManager(user);
        var email = new RecordingEmailSender();
        Assert.IsType<OkObjectResult>(await Controller(manager, email, signedIn: false)
            .ForgotPassword(new(user.Email!), default));
        Assert.Single(email.SetupMessages);
        Assert.Empty(email.Messages);
    }

    [Fact]
    public async Task Reset_email_delivery_failure_returns_a_retryable_error()
    {
        using var manager = new StubUserManager(Account());
        var controller = Controller(manager, new RecordingEmailSender { Fail = true });

        var result = Assert.IsType<ObjectResult>(await controller.RequestPasswordResetLink(default));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    private static ApplicationUser Account() => new()
    {
        Id = Guid.NewGuid(), Email = "player+test@example.test", UserName = "player+test@example.test",
        DisplayName = "Player", EmailConfirmed = true, PasswordHash = "test-hash"
    };

    private static AuthController Controller(StubUserManager manager, IAccountEmailSender sender, bool signedIn = true)
    {
        var identity = signedIn
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, manager.User.Id.ToString())], "test")
            : new ClaimsIdentity();
        return new AuthController(manager, null!, sender,
            Options.Create(new ClientOptions { BaseUrl = "https://pickandroll.test" }),
            Options.Create(new GoogleOptions()), Options.Create(new IdentityBootstrapOptions()), null!, TimeProvider.System,
            new PasswordSetupService(manager))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
    }

    private sealed class RecordingEmailSender : IAccountEmailSender
    {
        public bool Fail { get; init; }
        public List<(string Email, string Url)> Messages { get; } = [];
        public List<(string Email, string Url)> SetupMessages { get; } = [];
        public Task SendConfirmationAsync(string email, string displayName, string confirmationUrl, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string email, string displayName, string resetUrl, CancellationToken cancellationToken)
        {
            if (Fail) throw new AccountEmailDeliveryException();
            Messages.Add((email, resetUrl));
            return Task.CompletedTask;
        }
        public Task SendPasswordSetupAsync(string email, string displayName, string setupUrl, CancellationToken cancellationToken)
        {
            if (Fail) throw new AccountEmailDeliveryException();
            SetupMessages.Add((email, setupUrl));
            return Task.CompletedTask;
        }
    }

    private sealed class StubUserManager(ApplicationUser user) : UserManager<ApplicationUser>(
        new UnusedUserStore(), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
        [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public const string ResetToken = "identity-token/+&=with-special-characters";
        public ApplicationUser User { get; } = user;
        public int Updates { get; private set; }
        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(
            principal.Identity?.IsAuthenticated == true && principal.FindFirstValue(ClaimTypes.NameIdentifier) == User.Id.ToString() ? User : null);
        public override Task<IdentityResult> UpdateAsync(ApplicationUser value) { Updates++; return Task.FromResult(IdentityResult.Success); }
        public override Task<IList<string>> GetRolesAsync(ApplicationUser value) => Task.FromResult<IList<string>>([AppRoles.Free]);
        public override Task<bool> HasPasswordAsync(ApplicationUser value) => Task.FromResult(value.PasswordHash is not null);
        public override Task<bool> IsEmailConfirmedAsync(ApplicationUser value) => Task.FromResult(value.EmailConfirmed);
        public override Task<string> GeneratePasswordResetTokenAsync(ApplicationUser value) => Task.FromResult(ResetToken);
        public override Task<string> GenerateUserTokenAsync(ApplicationUser value, string provider, string purpose) => Task.FromResult("password-setup-token/+&=");
        public override Task<ApplicationUser?> FindByEmailAsync(string email) => Task.FromResult<ApplicationUser?>(
            string.Equals(User.Email, email, StringComparison.OrdinalIgnoreCase) ? User : null);
    }

    private sealed class UnusedUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
