# Contact form and email setup

The Contact Us page is `/contact`, linked in the footer. It calls `POST /api/contact` with `name`, `email`, `subject`, `message`, and an empty `website` honeypot field. The API validates submissions and sends a plain-text email **to `noreply@pickandroll.io`**. The visitor's email is set as Reply-To. Messages are not saved in the application database, so no migrations are required.

## Gmail for testing

Enable 2-Step Verification on the Google account that will send mail, then create an app password at <https://myaccount.google.com/apppasswords>. Use that app password, not your normal Google password. Availability depends on the account's security settings; see [Google's app password instructions](https://support.google.com/mail/answer/185833).

Add these values to the existing server `.env` file (do not commit the real password):

```dotenv
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_SECURITY=StartTls
SMTP_USERNAME=your-testing-account@gmail.com
SMTP_PASSWORD=your-google-app-password
SMTP_FROM_ADDRESS=your-testing-account@gmail.com
```

The From address should be the authenticated Gmail address. If `SMTP_FROM_ADDRESS` is left empty, the sender uses `SMTP_USERNAME`. The recipient remains `noreply@pickandroll.io`. That address must already have a working mailbox or forwarding destination; sending through Gmail does not create a mailbox for your domain.

For local testing, set the matching `Smtp:Host`, `Smtp:Port`, `Smtp:Security`, `Smtp:Username`, `Smtp:Password`, and `Smtp:FromAddress` settings in local configuration or environment variables. Nested environment variables use double underscores, for example `Smtp__Username`. Keep passwords outside tracked appsettings files.

After deployment, recreate the API container to load changed settings:

```bash
docker compose up -d --build
docker compose logs --tail=100 api
```

Open `/contact` and send a real test message. A successful response means the SMTP server accepted the message, not that it has necessarily arrived in the inbox; check the recipient's spam folder as well. The implementation was not tested against a live mailbox because no credentials were supplied.

## Later: Google Workspace

After setting up Workspace and the domain mailbox, change `SMTP_USERNAME`, `SMTP_PASSWORD`, and `SMTP_FROM_ADDRESS` to the Workspace account values. Gmail SMTP remains `smtp.gmail.com:587` with STARTTLS if the account permits app-password authentication. Follow the Workspace account's current authentication policy and configure the domain's mail DNS records. See [Google SMTP setup](https://support.google.com/a/answer/176600).

## Delivery and abuse handling

- MailKit handles encrypted SMTP delivery. `StartTls` (normally port 587) and `SslOnConnect` (normally port 465) are supported; TLS certificate verification is enabled.
- A successful API call returns 200. Invalid input returns 400, a delivery failure or missing SMTP configuration returns 503, and the contact rate limit returns 429. The rest of the application remains usable without email configured.
- The named contact policy allows ten requests per minute **across this API instance**, intentionally without trusting client-provided forwarded IP headers. It does not throttle the quiz endpoints. This basic limit and the honeypot are not a full CAPTCHA system.
- Request bodies are limited to 32 KB; field lengths and header newlines are validated. SMTP credentials, visitor details, and message contents are not logged by the email sender.
- Failed submissions remain in the frontend so visitors can retry. The API does not automatically retry SMTP sends, which could duplicate a message accepted just before a network interruption.
