using System.ComponentModel.DataAnnotations;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;

namespace EuroleagueQuiz.Application.Services;

public sealed class ContactService(IContactEmailSender sender) : IContactService
{
    public async Task SendAsync(ContactRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Website)) return;

        var normalized = new ContactRequest
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim()
        };
        Validator.ValidateObject(normalized, new ValidationContext(normalized), validateAllProperties: true);

        var email = new ContactEmail(
            "noreply@pickandroll.io", normalized.Email, normalized.Name,
            $"[Pick & Roll Contact] {normalized.Subject}",
            $"Name: {normalized.Name}\nEmail: {normalized.Email}\nSubject: {normalized.Subject}\n\n{normalized.Message}",
            normalized.Subject,
            normalized.Message);
        await sender.SendAsync(email, cancellationToken);
    }
}
