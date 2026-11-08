using EuroleagueQuiz.Api.Authentication;
using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Tests;

public sealed class PasswordSetupServiceTests
{
    private const string ValidPassword = "PortalPassword42";

    [Fact]
    public async Task Verified_setup_adds_a_hashed_password_to_the_existing_google_account()
    {
        using var fixture = new Fixture();
        var id = fixture.User.Id;
        var email = fixture.User.Email;
        var token = await fixture.Service.GenerateTokenAsync(fixture.User);

        Assert.True((await fixture.Service.SetPasswordAsync(fixture.User, token, ValidPassword)).Succeeded);
        Assert.True(await fixture.Users.CheckPasswordAsync(fixture.User, ValidPassword));
        Assert.NotEqual(ValidPassword, fixture.User.PasswordHash);
        Assert.Equal(id, fixture.User.Id);
        Assert.Equal(email, fixture.User.Email);
        var login = Assert.Single(await fixture.Users.GetLoginsAsync(fixture.User));
        Assert.Equal("Google", login.LoginProvider);
        Assert.Equal("original-google-id", login.ProviderKey);
    }

    [Fact]
    public async Task A_setup_link_cannot_be_reused_or_overwrite_an_existing_password()
    {
        using var fixture = new Fixture();
        var token = await fixture.Service.GenerateTokenAsync(fixture.User);
        Assert.True((await fixture.Service.SetPasswordAsync(fixture.User, token, ValidPassword)).Succeeded);

        Assert.False((await fixture.Service.SetPasswordAsync(fixture.User, token, "DifferentPassword43")).Succeeded);
        Assert.True(await fixture.Users.CheckPasswordAsync(fixture.User, ValidPassword));
        Assert.False(await fixture.Users.CheckPasswordAsync(fixture.User, "DifferentPassword43"));
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("confirmation")]
    [InlineData("malformed")]
    public async Task Other_token_purposes_and_malformed_tokens_cannot_create_passwords(string kind)
    {
        using var fixture = new Fixture();
        var token = kind switch
        {
            "reset" => await fixture.Users.GeneratePasswordResetTokenAsync(fixture.User),
            "confirmation" => await fixture.Users.GenerateEmailConfirmationTokenAsync(fixture.User),
            _ => "invalid-token"
        };

        Assert.False((await fixture.Service.SetPasswordAsync(fixture.User, token, ValidPassword)).Succeeded);
        Assert.False(await fixture.Users.HasPasswordAsync(fixture.User));
    }

    [Fact]
    public async Task A_token_is_bound_to_its_original_user()
    {
        using var fixture = new Fixture();
        var token = await fixture.Service.GenerateTokenAsync(fixture.User);
        var other = new ApplicationUser { Id = Guid.NewGuid(), EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString() };

        Assert.False((await fixture.Service.SetPasswordAsync(other, token, ValidPassword)).Succeeded);
        Assert.Null(other.PasswordHash);
        Assert.Null(fixture.User.PasswordHash);
    }

    [Fact]
    public async Task Expired_tokens_are_rejected()
    {
        using var fixture = new Fixture(TimeSpan.FromMinutes(-1));
        var token = await fixture.Service.GenerateTokenAsync(fixture.User);

        Assert.False((await fixture.Service.SetPasswordAsync(fixture.User, token, ValidPassword)).Succeeded);
        Assert.Null(fixture.User.PasswordHash);
    }

    [Fact]
    public async Task Weak_passwords_are_rejected_and_the_owner_can_retry_with_a_strong_password()
    {
        using var fixture = new Fixture();
        var token = await fixture.Service.GenerateTokenAsync(fixture.User);

        Assert.False((await fixture.Service.SetPasswordAsync(fixture.User, token, "weak")).Succeeded);
        Assert.Null(fixture.User.PasswordHash);
        Assert.True((await fixture.Service.SetPasswordAsync(fixture.User, token, ValidPassword)).Succeeded);
    }

    [Fact]
    public async Task Unconfirmed_email_accounts_cannot_add_a_password()
    {
        using var fixture = new Fixture();
        fixture.User.EmailConfirmed = false;
        var token = await fixture.Service.GenerateTokenAsync(fixture.User);

        Assert.False((await fixture.Service.SetPasswordAsync(fixture.User, token, ValidPassword)).Succeeded);
        Assert.Null(fixture.User.PasswordHash);
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationUser User { get; } = new()
        {
            Id = Guid.NewGuid(), UserName = "google@example.test", Email = "google@example.test",
            EmailConfirmed = true, DisplayName = "Google Player", SecurityStamp = Guid.NewGuid().ToString()
        };
        public UserManager<ApplicationUser> Users { get; }
        public PasswordSetupService Service { get; }

        public Fixture(TimeSpan? lifetime = null)
        {
            var options = new IdentityOptions();
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            Users = new UserManager<ApplicationUser>(new MemoryStore(User), Options.Create(options), new PasswordHasher<ApplicationUser>(),
                [], [new PasswordValidator<ApplicationUser>()], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
                NullLogger<UserManager<ApplicationUser>>.Instance);
            Users.RegisterTokenProvider(options.Tokens.PasswordResetTokenProvider, new DataProtectorTokenProvider<ApplicationUser>(
                new EphemeralDataProtectionProvider(), Options.Create(new DataProtectionTokenProviderOptions
                { TokenLifespan = lifetime ?? TimeSpan.FromHours(1) }), NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance));
            Service = new PasswordSetupService(Users);
        }

        public void Dispose() => Users.Dispose();
    }

    private sealed class MemoryStore(ApplicationUser user) : IUserPasswordStore<ApplicationUser>,
        IUserSecurityStampStore<ApplicationUser>, IUserEmailStore<ApplicationUser>, IUserLoginStore<ApplicationUser>
    {
        private readonly List<UserLoginInfo> logins = [new("Google", "original-google-id", "Google")];
        public void Dispose() { }
        public Task<string> GetUserIdAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.Id.ToString());
        public Task<string?> GetUserNameAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.UserName);
        public Task SetUserNameAsync(ApplicationUser value, string? name, CancellationToken ct) { value.UserName = name; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser value, string? name, CancellationToken ct) { value.NormalizedUserName = name; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser value, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult<ApplicationUser?>(user.Id.ToString() == id ? user : null);
        public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult<ApplicationUser?>(user.NormalizedUserName == name ? user : null);
        public Task SetPasswordHashAsync(ApplicationUser value, string? hash, CancellationToken ct) { value.PasswordHash = hash; return Task.CompletedTask; }
        public Task<string?> GetPasswordHashAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.PasswordHash);
        public Task<bool> HasPasswordAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.PasswordHash is not null);
        public Task SetSecurityStampAsync(ApplicationUser value, string stamp, CancellationToken ct) { value.SecurityStamp = stamp; return Task.CompletedTask; }
        public Task<string?> GetSecurityStampAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.SecurityStamp);
        public Task SetEmailAsync(ApplicationUser value, string? email, CancellationToken ct) { value.Email = email; return Task.CompletedTask; }
        public Task<string?> GetEmailAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.Email);
        public Task<bool> GetEmailConfirmedAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.EmailConfirmed);
        public Task SetEmailConfirmedAsync(ApplicationUser value, bool confirmed, CancellationToken ct) { value.EmailConfirmed = confirmed; return Task.CompletedTask; }
        public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken ct) => Task.FromResult<ApplicationUser?>(user.NormalizedEmail == email ? user : null);
        public Task<string?> GetNormalizedEmailAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult(value.NormalizedEmail);
        public Task SetNormalizedEmailAsync(ApplicationUser value, string? email, CancellationToken ct) { value.NormalizedEmail = email; return Task.CompletedTask; }
        public Task AddLoginAsync(ApplicationUser value, UserLoginInfo login, CancellationToken ct) { logins.Add(login); return Task.CompletedTask; }
        public Task RemoveLoginAsync(ApplicationUser value, string provider, string key, CancellationToken ct) { logins.RemoveAll(login => login.LoginProvider == provider && login.ProviderKey == key); return Task.CompletedTask; }
        public Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser value, CancellationToken ct) => Task.FromResult<IList<UserLoginInfo>>(logins);
        public Task<ApplicationUser?> FindByLoginAsync(string provider, string key, CancellationToken ct) => Task.FromResult<ApplicationUser?>(
            logins.Any(login => login.LoginProvider == provider && login.ProviderKey == key) ? user : null);
    }
}
