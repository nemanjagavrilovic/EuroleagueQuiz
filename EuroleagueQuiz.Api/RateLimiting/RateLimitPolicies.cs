namespace EuroleagueQuiz.Api.RateLimiting;

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Registration = "registration";
    public const string AccountEmail = "account-email";
    public const string Contact = "contact";
    public const string AdminAction = "admin-action";
    public const string Import = "import";
}
