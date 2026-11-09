using System.Net;

namespace EuroleagueQuiz.Infrastructure.Email;

internal static class ContactEmailTemplates
{
    public static string CreateHtml(string name, string email, string subject, string message)
    {
        var safeName = WebUtility.HtmlEncode(name);
        var safeEmail = WebUtility.HtmlEncode(email);
        var safeSubject = WebUtility.HtmlEncode(subject);
        var safeMessage = WebUtility.HtmlEncode(message).Replace("\r\n", "<br>").Replace("\n", "<br>");
        var replyUrl = WebUtility.HtmlEncode($"mailto:{email}?subject={Uri.EscapeDataString($"Re: {subject}")}");

        return $"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:0;background:#edf1f5;font-family:Arial,Helvetica,sans-serif;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">New Pick &amp; Roll contact message from {safeName}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#edf1f5;">
                <tr><td align="center" style="padding:30px 12px;">
                  <table role="presentation" width="620" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:620px;background:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 18px 50px rgba(8,24,39,.14);">
                    <tr><td style="padding:30px 42px 34px;background:#071827;background-image:linear-gradient(145deg,#071827,#102638);color:#ffffff;">
                      <table role="presentation" cellspacing="0" cellpadding="0" border="0"><tr>
                        <td style="width:42px;height:42px;border-radius:50%;background:#ff7a1a;color:#ffffff;font-size:16px;font-style:italic;font-weight:800;text-align:center;vertical-align:middle;">PR</td>
                        <td style="padding-left:12px;color:#ffffff;font-size:18px;font-weight:700;letter-spacing:.3px;">PICK <span style="color:#ff7a1a;">&amp;</span> ROLL<br><span style="font-size:9px;font-weight:400;letter-spacing:1.2px;color:#aebdca;">CONTACT FORM</span></td>
                      </tr></table>
                      <p style="margin:34px 0 7px;color:#ff7a1a;font-size:11px;font-weight:700;letter-spacing:1.8px;">NEW MESSAGE</p>
                      <h1 style="margin:0;color:#ffffff;font-size:28px;line-height:1.2;letter-spacing:-.5px;">Someone got in touch</h1>
                    </td></tr>
                    <tr><td style="padding:32px 42px 28px;background:#ffffff;color:#172231;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-bottom:24px;background:#f2f5f8;border-radius:10px;">
                        <tr><td style="padding:17px 20px;color:#64748b;font-size:11px;font-weight:700;letter-spacing:1px;">FROM</td><td style="padding:17px 20px;color:#172231;font-size:14px;text-align:right;"><strong>{safeName}</strong><br><a href="mailto:{safeEmail}" style="color:#c9570a;text-decoration:none;">{safeEmail}</a></td></tr>
                      </table>
                      <p style="margin:0 0 7px;color:#64748b;font-size:10px;font-weight:700;letter-spacing:1.4px;">SUBJECT</p>
                      <h2 style="margin:0 0 20px;color:#172231;font-size:20px;line-height:1.35;">{safeSubject}</h2>
                      <div style="padding:20px;border-left:4px solid #ff7a1a;border-radius:8px;background:#fff7ed;color:#382619;font-size:15px;line-height:1.65;">{safeMessage}</div>
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="margin-top:24px;">
                        <tr><td align="center" style="border-radius:9px;background:#ff7a1a;">
                          <a href="{replyUrl}" style="display:block;padding:15px 20px;color:#111827;font-size:15px;font-weight:700;text-align:center;text-decoration:none;">Reply to {safeName}</a>
                        </td></tr>
                      </table>
                      <p style="margin:20px 0 0;color:#64748b;font-size:12px;line-height:1.55;">Replying to this email also sends your response directly to {safeEmail}.</p>
                    </td></tr>
                    <tr><td style="padding:19px 42px 22px;background:#f2f5f8;color:#64748b;font-size:11px;line-height:1.5;">
                      Sent from the contact form at <strong style="color:#172231;">pickandroll.io</strong>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
