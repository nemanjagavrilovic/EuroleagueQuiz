# Pick & Roll authentication

The API uses ASP.NET Core Identity with PostgreSQL, short-lived JWT access tokens and rotating refresh tokens. Angular keeps the access token only in memory; the refresh token is an HTTP-only cookie. Local accounts require email confirmation. Google accounts use Google's OAuth authorization-code flow. Docker persists ASP.NET Core data-protection keys in the `data-protection` volume so confirmation/reset tokens and OAuth cookies survive container replacement.

## Database

Apply the `UserIdentity` migration before starting the updated API:

```powershell
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --configuration Release
```

## Production environment

Set these values in `.env` on the server. Do not commit their values:

```dotenv
JWT_SIGNING_KEY=generate-a-random-secret-of-at-least-32-bytes
GOOGLE_CLIENT_ID=your-google-oauth-client-id
GOOGLE_CLIENT_SECRET=your-google-oauth-client-secret
ADMIN_EMAIL=your-admin-email@example.com
PUBLIC_ORIGIN=https://pickandroll.io
RESEND_API_KEY=re_your_resend_api_key
RESEND_FROM_ADDRESS=noreply@pickandroll.io
```

## Local Resend configuration

Resend's non-secret HTTPS API defaults are in `EuroleagueQuiz.Api/appsettings.json`. Keep the API key out of that tracked file. Store it with .NET user-secrets instead (no local `.env` file is required):

```powershell
dotnet user-secrets set "Resend:ApiKey" "re_your_resend_api_key" --project EuroleagueQuiz.Api
```

The verified Resend domain must permit `noreply@pickandroll.io` as a sender. You can inspect the local secret without exposing its value in source control with `dotnet user-secrets list --project EuroleagueQuiz.Api`.

Generate a signing key, for example:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
```

In Google Cloud Console create an OAuth 2.0 **Web application** client. Add this production redirect URI:

```text
https://pickandroll.io/api/signin-google
```

For local API testing also add:

```text
http://localhost:5248/api/signin-google
```

The configured `ADMIN_EMAIL` is assigned the `Admin` role when the API starts after that account exists. Every account also receives the `Free` role. Subscription plans are stored separately from roles so paid plans can be added later without changing authorization roles.

## Daily modes

Authentication is intentionally introduced independently from the current guest daily services. The next scoring phase will add:

- Guest Daily: one unranked challenge per game and UTC day.
- Ranked Daily: one leaderboard-eligible challenge per authenticated user, game and UTC day.
- Practice: unlimited newly generated combinations that exclude the current daily definition where practical.
- A guest attempt claimed during registration remains visible but unranked and locks that game's ranked attempt until the next UTC reset.
