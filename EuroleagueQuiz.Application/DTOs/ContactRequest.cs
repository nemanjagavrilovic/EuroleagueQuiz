using System.ComponentModel.DataAnnotations;

namespace EuroleagueQuiz.Application.DTOs;

public sealed class ContactRequest
{
    [Required, StringLength(100), RegularExpression(@"^[^\r\n]+$")]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254), RegularExpression(@"^[^\r\n]+$")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(150), RegularExpression(@"^[^\r\n]+$")]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(5000, MinimumLength = 10)]
    public string Message { get; set; } = string.Empty;

    // Validated by TurnstileValidator at the API boundary. Keeping this out of
    // DataAnnotations allows ContactService to validate its normalized copy
    // without carrying the one-time security token into the email workflow.
    public string TurnstileToken { get; set; } = string.Empty;

    // Honeypot: real visitors leave this field empty.
    [StringLength(200)]
    public string? Website { get; set; }
}
