using EuroleagueQuiz.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EuroleagueQuiz.Infrastructure.Email;

public sealed class SmtpAccountEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpAccountEmailSender> logger) : IAccountEmailSender
{
    public Task SendConfirmationAsync(string email, string displayName, string confirmationUrl, CancellationToken cancellationToken) =>
        SendAsync(email, displayName, "Confirm your Pick & Roll account",
            $"Hi {displayName},\n\nConfirm your email address to activate your Pick & Roll account:\n{confirmationUrl}\n\nIf you did not create this account, you can ignore this message.", cancellationToken);

    public Task SendPasswordResetAsync(string email, string displayName, string resetUrl, CancellationToken cancellationToken) =>
        SendAsync(email, displayName, "Reset your Pick & Roll password",
            $"Hi {displayName},\n\nUse this link to reset your Pick & Roll password:\n{resetUrl}\n\nIf you did not request a password reset, you can ignore this message.", cancellationToken);

    private async Task SendAsync(string email, string displayName, string subject, string body, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var fromAddress = string.IsNullOrWhiteSpace(settings.FromAddress) ? settings.Username : settings.FromAddress;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Username)
            || string.IsNullOrWhiteSpace(settings.Password) || settings.Port is < 1 or > 65535
            || !MailboxAddress.TryParse(fromAddress, out var from) || !MailboxAddress.TryParse(email, out var to)
            || settings.Security is not ("StartTls" or "SslOnConnect"))
            throw new AccountEmailDeliveryException();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var client = new SmtpClient { Timeout = 15000 };
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName, from.Address));
            message.To.Add(new MailboxAddress(displayName, to.Address));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };
            var security = settings.Security == "SslOnConnect" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(settings.Host, settings.Port, security, timeout.Token);
            await client.AuthenticateAsync(settings.Username, settings.Password, timeout.Token);
            await client.SendAsync(message, timeout.Token);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Account email delivery failed ({FailureType}): {FailureReason}",
                exception.GetType().Name, exception.GetBaseException().Message);
            throw new AccountEmailDeliveryException();
        }

        try { await client.DisconnectAsync(true, timeout.Token); }
        catch (Exception) { logger.LogDebug("SMTP connection closed after accepting the account message."); }
    }
}
