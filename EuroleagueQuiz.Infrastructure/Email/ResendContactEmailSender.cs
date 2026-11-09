using EuroleagueQuiz.Application.Interfaces;

namespace EuroleagueQuiz.Infrastructure.Email;

public sealed class ResendContactEmailSender(ResendEmailClient client) : IContactEmailSender
{
    public async Task SendAsync(ContactEmail email, CancellationToken cancellationToken)
    {
        var html = ContactEmailTemplates.CreateHtml(
            email.ReplyToName,
            email.ReplyTo,
            email.SubmittedSubject,
            email.SubmittedMessage);

        try { await client.SendAsync(email.To, email.Subject, email.Body, email.ReplyTo, cancellationToken, html); }
        catch (ResendEmailException) { throw new ContactEmailDeliveryException(); }
    }
}
