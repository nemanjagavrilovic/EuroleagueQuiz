# EuroLeague Player Grid

Initial data-foundation implementation for a EuroLeague player grid application.

## Current stack

- ASP.NET Core Web API / .NET 8
- Angular 17
- Entity Framework Core
- PostgreSQL
- Repository pattern: Controller → Service → Repository

## Solution structure

```text
EuroleagueQuiz.Api             HTTP controllers and application startup
EuroleagueQuiz.Application     DTOs, services, and application interfaces
EuroleagueQuiz.Domain          Entities and repository interfaces
EuroleagueQuiz.Infrastructure  EF Core, PostgreSQL, and repository implementations
EuroleagueQuiz.Tests            Automated tests
client                         Angular application
```

## Database setup

The application uses PostgreSQL. Create the database in PostgreSQL or let the EF Core migration create it, then configure `ConnectionStrings:Postgres` in `EuroleagueQuiz.Api/appsettings.json` or through user-secrets/environment configuration.

Example connection string:

```text
Host=localhost;Port=5432;Database=EuroleagueQuiz;Username=postgres;Password=postgres;
```

After NuGet restore succeeds, create the first migration:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api --output-dir Persistence/Migrations
dotnet ef database update --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api
```

## Run the API

```powershell
dotnet restore
dotnet run --project EuroleagueQuiz.Api
```

The API exposes the initial data endpoints:

```text
GET /api/players
GET /api/players/{id}
GET /api/players/common?firstTeamId=1&secondTeamId=2
GET /api/teams
GET /api/teams/{id}
GET /api/seasons
GET /api/seasons/{id}
POST /api/import/seasons/E2013
POST /api/import/seasons
```

The importer uses the InCrowd EuroLeague v2 feed:

```text
https://feeds.incrowdsports.com/provider/euroleague-feeds/v2/
```

`POST /api/import/seasons/E2013` imports one season. `POST /api/import/seasons` imports all provider seasons from 2013 onward. Imports are idempotent and use the unique player/team/season relationship constraint to prevent duplicates.

## Run the Angular client

```powershell
cd client
npm install
npm start
```

The external basketball data provider is intentionally represented by an abstraction and has not been hard-coded. A licensed provider must be selected before implementing the historical and future-season importer.

## Inspect the InCrowd response structure

Before changing the database model, inspect real provider responses without writing to the database:

```powershell
.\scripts\inspect-incrowd-api.ps1 -SeasonCode E2013 -TeamCode red
```

The script saves raw responses and a shape summary under `api-samples`. It calls only the read-only season, clubs, and club-people endpoints.

## Authentication

The client includes email/password registration and login at `/register` and `/login`. The API stores accounts in the `AppUsers` table, hashes passwords with PBKDF2-SHA256, and returns a seven-day bearer token.

Apply the new migration before using registration against an existing database:

```powershell
dotnet ef database update `
  --project EuroleagueQuiz.Infrastructure `
  --startup-project EuroleagueQuiz.Api
```

In Development, the API uses a development-only signing key. For Production, configure `Auth__SigningKey` as a random secret of at least 32 characters, for example with a secret manager or environment variable. Do not commit a production key. The auth endpoints are `POST /api/auth/register`, `POST /api/auth/login`, and authenticated `GET /api/auth/me`.

Google sign-in uses Google Identity Services in the client and verifies the ID token in the API. Create a Web application OAuth client in Google Cloud Console and add the local and deployed frontend origins. Set the same public client ID in `googleClientId` in the Angular environment file and in the API setting `Google__ClientId`. Without this configuration, the Google button displays setup guidance and cannot authenticate users.

The `AddGoogleSignIn` migration adds the Google account identifier and allows Google-only accounts to omit a password hash. Apply all migrations again after adding it.
