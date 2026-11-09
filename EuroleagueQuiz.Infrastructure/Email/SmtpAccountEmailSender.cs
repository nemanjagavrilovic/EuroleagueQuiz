using EuroleagueQuiz.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EuroleagueQuiz.Infrastructure.Email;

public sealed class SmtpAccountEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpAccountEmailSender> logger) : IAccountEmailSender
{
    public Task SendConfirmationAsync(string email, string displayName, string confirmationUrl, CancellationToken cancellationToken)
    {
        var content = AccountEmailTemplates.Confirmation(displayName, confirmationUrl);
        return SendAsync(email, displayName, "Confirm your Pick & Roll account", content.Text, cancellationToken, content.Html);
    }

    public Task SendPasswordResetAsync(string email, string displayName, string resetUrl, CancellationToken cancellationToken) =>
        SendAsync(email, displayName, "Reset your Pick & Roll password",
            $"Hi {displayName},\n\nUse this link to reset your Pick & Roll password:\n{resetUrl}\n\nIf you did not request a password reset, you can ignore this message.", cancellationToken);

    private async Task SendAsync(string email, string displayName, string subject, string body, CancellationToken cancellationToken, string? html = null)
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
            var content = new BodyBuilder { TextBody = body, HtmlBody = html };
            message.Body = content.ToMessageBody();
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

    public Task SendPasswordSetupAsync(string email, string displayName, string setupUrl, CancellationToken cancellationToken) =>
        SendAsync(email, displayName, "Create your Pick & Roll password",
            $"Hi {displayName},\n\nUse this one-time link to create a password for your existing Pick & Roll account:\n{setupUrl}\n\nYou can then sign in with your email and password or continue using Google. This does not change your Google password.\n\nIf you did not request this, ignore this message; your account will not change.", cancellationToken);

    public Task SendTemporaryPasswordAsync(string email, string displayName, string temporaryPassword, string loginUrl, CancellationToken cancellationToken)
    {
        var content = AccountEmailTemplates.TemporaryPassword(displayName, temporaryPassword, loginUrl);
        return SendAsync(email, displayName, "Your temporary Pick & Roll password", content.Text, cancellationToken, content.Html);
    }
}
