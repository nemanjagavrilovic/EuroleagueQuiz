using EuroleagueQuiz.Application.Interfaces;

namespace EuroleagueQuiz.Infrastructure.Email;

public sealed class ResendContactEmailSender(ResendEmailClient client) : IContactEmailSender
{
    public async Task SendAsync(ContactEmail email, CancellationToken cancellationToken)
    {
        try { await client.SendAsync(email.To, email.Subject, email.Body, email.ReplyTo, cancellationToken); }
        catch (ResendEmailException) { throw new ContactEmailDeliveryException(); }
    }
}
