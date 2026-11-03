namespace EuroleagueQuiz.Infrastructure.Email;

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@pickandroll.io";
    public string FromName { get; set; } = "Pick & Roll";
}
