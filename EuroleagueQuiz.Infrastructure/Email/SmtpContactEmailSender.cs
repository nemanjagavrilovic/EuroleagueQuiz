using EuroleagueQuiz.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EuroleagueQuiz.Infrastructure.Email;

public sealed class SmtpContactEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpContactEmailSender> logger) : IContactEmailSender
{
    public async Task SendAsync(ContactEmail email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var fromAddress = string.IsNullOrWhiteSpace(settings.FromAddress) ? settings.Username : settings.FromAddress;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Username)
            || string.IsNullOrWhiteSpace(settings.Password) || settings.Port is < 1 or > 65535
            || !MailboxAddress.TryParse(fromAddress, out var from)
            || settings.Security is not ("StartTls" or "SslOnConnect"))
        {
            logger.LogWarning("Contact email is unavailable: SMTP settings are incomplete or invalid.");
            throw new ContactEmailDeliveryException();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var client = new SmtpClient { Timeout = 15000 };
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName, from.Address));
            message.To.Add(MailboxAddress.Parse(email.To));
            message.ReplyTo.Add(new MailboxAddress(email.ReplyToName, email.ReplyTo));
            message.Subject = email.Subject;
            message.Body = new TextPart("plain") { Text = email.Body };

            var security = settings.Security == "SslOnConnect" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(settings.Host, settings.Port, security, timeout.Token);
            await client.AuthenticateAsync(settings.Username, settings.Password, timeout.Token);
            await client.SendAsync(message, timeout.Token);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Do not write visitor messages, addresses or SMTP credentials to application logs.
            logger.LogError("Contact email delivery failed ({FailureType}).", exception.GetType().Name);
            throw new ContactEmailDeliveryException();
        }

        // A disconnect failure must not report an already accepted message as unsent.
        try { await client.DisconnectAsync(true, timeout.Token); }
        catch (Exception) { logger.LogDebug("SMTP connection closed after accepting the contact message."); }
    }
}
