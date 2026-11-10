namespace EuroleagueQuiz.Api.Security;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string[] AllowedHostnames { get; set; } = [];
    public bool EnforceAction { get; set; } = true;
}
