using System.Net;

namespace EuroleagueQuiz.Infrastructure.Email;

internal sealed record AccountEmailContent(string Text, string Html);

internal static class AccountEmailTemplates
{
    public static AccountEmailContent Confirmation(string displayName, string confirmationUrl)
    {
        var name = WebUtility.HtmlEncode(displayName);
        var url = WebUtility.HtmlEncode(confirmationUrl);
        var text = $"Hi {displayName},\n\nConfirm your email address to activate your Pick & Roll account:\n{confirmationUrl}\n\nIf you did not create this account, you can ignore this message.";
        var body = $"""
            <p style="margin:0 0 16px;line-height:1.6;color:#172231;">Hi {name},</p>
            <p style="margin:0 0 24px;line-height:1.6;color:#172231;">Thanks for joining Pick &amp; Roll. Confirm your email address to activate your account and start playing ranked EuroLeague challenges.</p>
            {ActionButton(url, "Confirm email address")}
            <div style="margin:26px 0 0;padding:18px 20px;border-radius:10px;background:#fff7ed;color:#613416;line-height:1.5;">
              <strong style="display:block;margin-bottom:5px;">One quick step</strong>
              After confirmation, you can sign in, save your scores and appear on the leaderboard.
            </div>
            <p style="margin:24px 0 0;color:#64748b;font-size:13px;line-height:1.6;">If the button does not work, copy this address into your browser:<br><a href="{url}" style="color:#c9570a;word-break:break-all;">{url}</a></p>
            <p style="margin:20px 0 0;color:#64748b;font-size:13px;line-height:1.6;">Didn’t create a Pick &amp; Roll account? You can safely ignore this email.</p>
            """;
        return new AccountEmailContent(text, Layout(
            "Confirm your Pick & Roll email address",
            "WELCOME TO THE TEAM",
            "Confirm your email",
            "One final step gets you into the game.",
            body));
    }

    public static AccountEmailContent TemporaryPassword(string displayName, string temporaryPassword, string loginUrl)
    {
        var name = WebUtility.HtmlEncode(displayName);
        var password = WebUtility.HtmlEncode(temporaryPassword);
        var url = WebUtility.HtmlEncode(loginUrl);
        var text = $"Hi {displayName},\n\nAn administrator reset your Pick & Roll password.\n\nTemporary password: {temporaryPassword}\n\nSign in here:\n{loginUrl}\n\nYou must choose a new password immediately after signing in. Your previous password and active sessions are no longer valid. If you did not expect this reset, contact Pick & Roll support.";
        var body = $"""
            <p style="margin:0 0 16px;line-height:1.6;color:#172231;">Hi {name},</p>
            <p style="margin:0 0 22px;line-height:1.6;color:#172231;">An administrator created a temporary password for your Pick &amp; Roll account.</p>
            <div style="margin:0 0 24px;padding:20px 24px;border-left:4px solid #ff7a1a;border-radius:10px;background:#f2f5f8;text-align:center;">
              <span style="display:block;color:#64748b;font-size:10px;font-weight:700;letter-spacing:1.5px;">TEMPORARY PASSWORD</span>
              <strong style="display:block;margin:9px 0 6px;color:#172231;font-family:Consolas,'Courier New',monospace;font-size:25px;line-height:1.2;letter-spacing:2px;">{password}</strong>
              <span style="color:#64748b;font-size:12px;">Case-sensitive</span>
            </div>
            {ActionButton(url, "Sign in and change password")}
            <div style="margin:26px 0 0;padding:18px 20px;border-radius:10px;background:#fff7ed;color:#613416;line-height:1.5;">
              <strong style="display:block;margin-bottom:5px;">What happens next?</strong>
              Your current sessions have been signed out. After entering this password, choose a new one that only you know.
            </div>
            <p style="margin:24px 0 0;color:#64748b;font-size:13px;line-height:1.6;">Didn’t expect this email? Don’t use the password. Contact Pick &amp; Roll support so we can secure your account.</p>
            """;
        return new AccountEmailContent(text, Layout(
            "Your temporary Pick & Roll password",
            "ACCOUNT SECURITY",
            "Your temporary password is ready",
            "Use it once to sign in. We’ll ask you to create a private password immediately.",
            body));
    }

    private static string ActionButton(string url, string label) => $"""
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin:0 0 4px;">
          <tr><td align="center" style="border-radius:9px;background:#ff7a1a;">
            <a href="{url}" style="display:block;padding:15px 20px;color:#111827;font-size:15px;font-weight:700;text-align:center;text-decoration:none;">{label}</a>
          </td></tr>
        </table>
        """;

    private static string Layout(string preheader, string eyebrow, string title, string subtitle, string body) => $"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#edf1f5;font-family:Arial,Helvetica,sans-serif;">
          <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{WebUtility.HtmlEncode(preheader)}</div>
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#edf1f5;">
            <tr><td align="center" style="padding:30px 12px;">
              <table role="presentation" width="620" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:620px;background:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 18px 50px rgba(8,24,39,.14);">
                <tr><td style="padding:32px 42px 38px;background:#071827;background-image:linear-gradient(145deg,#071827,#102638);color:#ffffff;">
                  <table role="presentation" cellspacing="0" cellpadding="0" border="0"><tr>
                    <td style="width:42px;height:42px;border-radius:50%;background:#ff7a1a;color:#ffffff;font-size:16px;font-style:italic;font-weight:800;text-align:center;vertical-align:middle;">PR</td>
                    <td style="padding-left:12px;color:#ffffff;font-size:18px;font-weight:700;letter-spacing:.3px;">PICK <span style="color:#ff7a1a;">&amp;</span> ROLL<br><span style="font-size:9px;font-weight:400;letter-spacing:1.2px;color:#aebdca;">EUROLEAGUE QUIZZES</span></td>
                  </tr></table>
                  <p style="margin:36px 0 7px;color:#ff7a1a;font-size:11px;font-weight:700;letter-spacing:1.8px;">{WebUtility.HtmlEncode(eyebrow)}</p>
                  <h1 style="margin:0 0 9px;color:#ffffff;font-size:30px;line-height:1.15;letter-spacing:-.6px;">{WebUtility.HtmlEncode(title)}</h1>
                  <p style="margin:0;color:#c7d3df;font-size:15px;line-height:1.6;">{WebUtility.HtmlEncode(subtitle)}</p>
                </td></tr>
                <tr><td style="padding:34px 42px 28px;background:#ffffff;">{body}</td></tr>
                <tr><td style="padding:20px 42px 24px;background:#f2f5f8;color:#64748b;font-size:12px;line-height:1.5;">
                  <strong style="color:#172231;">Pick &amp; Roll</strong>&nbsp;&nbsp; EuroLeague basketball quizzes<br>
                  <span style="font-size:11px;">Automated account email · Please don’t reply</span>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}
