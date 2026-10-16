using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EuroleagueQuiz.Api.Authentication;

public sealed class AuthTokenService
{
    private const string Issuer = "PickAndRoll";
    private readonly byte[] _key;

    public AuthTokenService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredKey = configuration["Auth:SigningKey"];
        if (string.IsNullOrWhiteSpace(configuredKey) && environment.IsDevelopment())
        {
            configuredKey = "PickAndRoll-Development-Only-Key-Change-Before-Production-2026";
        }

        if (string.IsNullOrWhiteSpace(configuredKey) || Encoding.UTF8.GetByteCount(configuredKey) < 32)
        {
            throw new InvalidOperationException("Configure Auth:SigningKey with a random secret of at least 32 characters.");
        }

        _key = Encoding.UTF8.GetBytes(configuredKey);
    }

    public string CreateToken(long userId, string email, string displayName)
    {
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var now = DateTimeOffset.UtcNow;
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = Issuer,
            sub = userId.ToString(),
            email,
            name = displayName,
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddDays(7).ToUnixTimeSeconds()
        }));
        var unsignedToken = $"{header}.{payload}";
        using var hmac = new HMACSHA256(_key);
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken)));
        return $"{unsignedToken}.{signature}";
    }

    public bool TryValidateToken(string token, out ClaimsPrincipal? principal)
    {
        principal = null;
        var parts = token.Split('.');
        if (parts.Length != 3) return false;

        try
        {
            var unsignedToken = $"{parts[0]}.{parts[1]}";
            using var hmac = new HMACSHA256(_key);
            var expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedToken));
            var actualSignature = Base64UrlDecode(parts[2]);
            if (actualSignature.Length != expectedSignature.Length ||
                !CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature)) return false;

            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var root = payload.RootElement;
            if (root.GetProperty("iss").GetString() != Issuer ||
                root.GetProperty("exp").GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, root.GetProperty("sub").GetString() ?? string.Empty),
                new Claim(ClaimTypes.Email, root.GetProperty("email").GetString() ?? string.Empty),
                new Claim(ClaimTypes.Name, root.GetProperty("name").GetString() ?? string.Empty)
            };
            principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
