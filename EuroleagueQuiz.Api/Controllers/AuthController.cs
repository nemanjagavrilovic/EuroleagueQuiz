using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using EuroleagueQuiz.Api.Authentication;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Infrastructure.Persistence;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private const int PasswordIterations = 210_000;
    private readonly EuroleagueDbContext _db;
    private readonly AuthTokenService _tokens;
    private readonly string? _googleClientId;

    public AuthController(EuroleagueDbContext db, AuthTokenService tokens, IConfiguration configuration)
    {
        _db = db;
        _tokens = tokens;
        _googleClientId = configuration["Google:ClientId"];
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.AppUsers.AnyAsync(user => user.Email == email, cancellationToken))
            return Conflict(new { message = "An account with this email already exists." });

        var user = new AppUser
        {
            DisplayName = request.DisplayName.Trim(),
            Email = email,
            PasswordHash = HashPassword(request.Password),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.AppUsers.Add(user);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        return Ok(CreateSession(user));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.AppUsers.SingleOrDefaultAsync(account => account.Email == email, cancellationToken);
        if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Email or password is incorrect." });

        return Ok(CreateSession(user));
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_googleClientId))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Google sign-in is not configured on the server." });

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleClientId }
            });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { message = "Google could not verify this sign-in. Please try again." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Google sign-in is temporarily unavailable." });
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Subject))
            return Unauthorized(new { message = "A verified Google email address is required." });

        var user = await _db.AppUsers.SingleOrDefaultAsync(account => account.GoogleSubject == payload.Subject, cancellationToken);
        if (user is null)
        {
            var email = payload.Email.Trim().ToLowerInvariant();
            if (await _db.AppUsers.AnyAsync(account => account.Email == email, cancellationToken))
                return Conflict(new { message = "An account with this email already exists. Sign in with your password first." });

            user = new AppUser
            {
                DisplayName = string.IsNullOrWhiteSpace(payload.Name) ? email.Split('@')[0] : payload.Name.Trim(),
                Email = email,
                GoogleSubject = payload.Subject,
                PasswordHash = null,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.AppUsers.Add(user);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return Conflict(new { message = "This Google account or email is already registered." });
            }
        }

        return Ok(CreateSession(user));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
        email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
        displayName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
    });

    private object CreateSession(AppUser user) => new
    {
        accessToken = _tokens.CreateToken(user.Id, user.Email, user.DisplayName),
        user = new { id = user.Id, email = user.Email, displayName = user.DisplayName }
    };

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" ||
            !int.TryParse(parts[1], out var iterations) || iterations < 100_000) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record RegisterRequest(
    [property: Required, StringLength(100, MinimumLength = 2)] string DisplayName,
    [property: Required, EmailAddress, StringLength(320)] string Email,
    [property: Required, MinLength(8), StringLength(128)] string Password);

public sealed record LoginRequest(
    [property: Required, EmailAddress, StringLength(320)] string Email,
    [property: Required, MinLength(8), StringLength(128)] string Password);

public sealed record GoogleLoginRequest([property: Required, MinLength(20)] string Credential);
