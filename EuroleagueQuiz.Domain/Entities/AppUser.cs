namespace EuroleagueQuiz.Domain.Entities;

public sealed class AppUser
{
    public long Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? GoogleSubject { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
