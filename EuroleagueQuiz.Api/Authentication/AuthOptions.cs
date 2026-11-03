namespace EuroleagueQuiz.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";
    public string Issuer { get; set; } = "PickAndRoll";
    public string Audience { get; set; } = "PickAndRoll.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class GoogleOptions
{
    public const string SectionName = "Authentication:Google";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class ClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:4200";
}

public sealed class IdentityBootstrapOptions
{
    public string[] AdminEmails { get; set; } = [];
}
