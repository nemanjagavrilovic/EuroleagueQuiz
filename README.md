# EuroLeague Player Grid

Initial data-foundation implementation for a EuroLeague player grid application.

## Current stack

- ASP.NET Core Web API / .NET 8
- Angular 17
- Entity Framework Core
- Microsoft SQL Server
- Repository pattern: Controller → Service → Repository

## Solution structure

```text
EuroleagueQuiz.Api             HTTP controllers and application startup
EuroleagueQuiz.Application     DTOs, services, and application interfaces
EuroleagueQuiz.Domain          Entities and repository interfaces
EuroleagueQuiz.Infrastructure  EF Core, SQL Server, and repository implementations
EuroleagueQuiz.Tests            Automated tests
client                         Angular application
```

## Database setup

The application uses SQL Server with Windows Authentication. Create the database in SQL Server or let the EF Core migration create it, then configure `ConnectionStrings:SqlServer` in `EuroleagueQuiz.Api/appsettings.json` or through user-secrets/environment configuration.

Example connection string:

```text
Server=localhost;Database=EuroleagueQuiz;Trusted_Connection=True;TrustServerCertificate=True;
```

After NuGet restore succeeds, create the first migration:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project EuroleagueQuiz.Infrastructure --startup-project EuroleagueQuiz.Api
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
