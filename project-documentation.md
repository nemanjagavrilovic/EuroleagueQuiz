# EuroLeague Player Grid

## 1. Project Overview

EuroLeague Player Grid is a web application that stores historical EuroLeague player and club data. The long-term goal is to present customers with a 3×3 club grid. Each grid cell represents two clubs, and the customer must identify a player who played for both clubs.

The first development phase is limited to building a reliable historical data foundation. Quiz entities, quiz generation, scoring, and gameplay rules will be documented in a later phase.

## 2. Main Objective

The system must maintain accurate relationships between:

- players;
- clubs;
- EuroLeague seasons; and
- the clubs for which each player played in each season.

The data must support queries such as:

> Find all players who played for Club A and Club B.

The system must also support future-season updates without duplicating existing players, clubs, or relationships.

## 3. Technology Stack

### Frontend

- Angular
- TypeScript
- Angular HttpClient for API communication

### Backend

- ASP.NET Core Web API
- .NET 8
- C#
- Entity Framework Core
- Repository pattern

### Database

- Microsoft SQL Server
- Entity Framework Core SQL Server provider

### Architecture

```text
Angular frontend
        ↓
ASP.NET Core controllers
        ↓
Application services
        ↓
Repositories
        ↓
Entity Framework Core
        ↓
MySQL
```

## 4. Project Scope

### Included in the initial phase

- EuroLeague season management
- Club management
- Player management
- Player–club–season relationships
- Importing historical data
- Updating data for future seasons
- Data validation and duplicate prevention
- Read-only API endpoints for players, clubs, and seasons

### Not included in the initial phase

- Quiz entities
- Daily quiz generation
- 3×3 grid generation
- User accounts
- Customer scores
- Leaderboards
- Payments

These features will be added after the historical data foundation is complete.

## 5. Layered Architecture

The backend will use the following request flow:

```text
Controller → Service → Repository → Database
```

### API layer

The API layer contains controllers. Controllers are responsible for:

- receiving HTTP requests;
- validating request parameters;
- calling application services; and
- returning HTTP responses.

Controllers must not contain database queries or business logic.

### Application layer

The application layer contains services and DTOs. Services are responsible for:

- coordinating application operations;
- applying validation rules;
- converting entities to DTOs;
- coordinating multiple repositories; and
- hiding persistence details from controllers.

### Domain layer

The domain layer contains:

- domain entities;
- repository interfaces; and
- domain-level rules.

The domain layer must not depend on Entity Framework Core or ASP.NET Core.

### Infrastructure layer

The infrastructure layer contains:

- the EF Core `DbContext`;
- SQL Server configuration;
- repository implementations;
- database migrations; and
- external basketball-data provider integrations.

## 6. Initial Domain Entities

### Player

Represents a basketball player.

Suggested fields:

```text
Id
ExternalId
FirstName
LastName
DisplayName
Nationality
DateOfBirth
CreatedAt
UpdatedAt
```

### Team

Represents a EuroLeague club.

Suggested fields:

```text
Id
ExternalId
Name
ShortName
Country
CreatedAt
UpdatedAt
```

### Season

Represents a EuroLeague season.

Suggested fields:

```text
Id
ExternalId
Name
StartYear
EndYear
CompetitionCode
CreatedAt
UpdatedAt
```

Examples of season names:

```text
2000–01
2013–14
2025–26
```

### PlayerTeamSeason

Represents the fact that a player played for a particular club during a particular season.

Suggested fields:

```text
Id
PlayerId
TeamId
SeasonId
GamesPlayed
Source
CreatedAt
UpdatedAt
```

The database must have a unique constraint on:

```text
PlayerId + TeamId + SeasonId
```

This prevents duplicate relationships when an import is run more than once.

### DataSyncRun

Tracks imports from the external data provider.

Suggested fields:

```text
Id
Provider
SeasonId
StartedAt
FinishedAt
Status
RecordsRead
RecordsCreated
RecordsUpdated
ErrorMessage
```

## 7. External Data Provider

The external data provider must be hidden behind an interface so that the application is not permanently coupled to one API.

```csharp
public interface IBasketballDataProvider
{
    Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(
        string seasonExternalId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerDto>> GetTeamPlayersAsync(
        string seasonExternalId,
        string teamExternalId,
        CancellationToken cancellationToken);
}
```

The provider implementation may use an authorized EuroLeague/InCrowd feed or another licensed data provider. The provider must be replaceable without changing controllers, services, or domain entities.

## 8. Repository Pattern

Repository interfaces belong in the Domain layer. Implementations belong in Infrastructure.

Example:

```csharp
public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken);

    Task<Player?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Player>> GetAllAsync(
        CancellationToken cancellationToken);

    Task AddAsync(
        Player player,
        CancellationToken cancellationToken);
}
```

The service layer uses repositories rather than accessing `DbContext` directly.

```csharp
public sealed class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _playerRepository;

    public PlayerService(IPlayerRepository playerRepository)
    {
        _playerRepository = playerRepository;
    }

    public async Task<IReadOnlyList<PlayerDto>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var players = await _playerRepository
            .GetAllAsync(cancellationToken);

        return players
            .Select(PlayerDto.FromEntity)
            .ToList();
    }
}
```

## 9. Data Import Process

The import process must be idempotent. Running the same import repeatedly must not create duplicate records.

```text
1. Retrieve available seasons.
2. Create or update the season record.
3. Retrieve clubs for the season.
4. Create or update club records.
5. Retrieve players for each club.
6. Create or update player records.
7. Create the PlayerTeamSeason relationship if it does not exist.
8. Validate imported records.
9. Store synchronization results.
```

Historical seasons will be imported once. Future seasons will be synchronized periodically, for example once per day or once per week during the season.

## 10. Data Rules

- External IDs must be stored for players, teams, and seasons.
- External IDs must be unique per provider.
- Player names must not be used as the primary identifier.
- Team names may change over time, so historical relationships must reference stable team IDs where available.
- A player can belong to multiple teams in one season.
- A player can return to the same team in different seasons.
- Importing the same source record twice must not create duplicates.
- Coaches and staff must not be stored as players.
- The system must record the source of imported data.

## 11. API Endpoints for the Initial Phase

Example endpoints:

```text
GET /api/seasons
GET /api/seasons/{id}
GET /api/teams
GET /api/teams/{id}
GET /api/players
GET /api/players/{id}
GET /api/players/{id}/teams
GET /api/teams/{id}/players
GET /api/player-team-seasons
```

The endpoint that will later support the 3×3 grid is conceptually:

```text
GET /api/players/common?teamAId={id}&teamBId={id}
```

This endpoint belongs to the data-access foundation. The later quiz layer will decide how to present and score the result.

## 12. Future 3×3 Grid Requirement

The final application will display three clubs across the top and three clubs down the side. Each cell represents one pair of clubs.

```text
                 Club A | Club B | Club C
------------------------------------------
Club D              ?   |   ?   |   ?
Club E              ?   |   ?   |   ?
Club F              ?   |   ?   |   ?
```

For each cell, the system must find players who have a relationship with both clubs. This requires accurate and complete `PlayerTeamSeason` data.

Quiz-specific entities and rules will be documented separately once the gameplay requirements are defined.

## 13. Security and Configuration

- External API keys must be stored in configuration or a secret manager.
- API keys must never be exposed to Angular.
- Import operations must run on the backend.
- Database credentials must not be committed to source control.
- External provider requests should use timeouts, retries, and rate limiting.
- Import logs must not expose API keys or sensitive configuration values.

## 14. Testing Requirements

The system should include tests for:

- creating and updating players;
- creating and updating teams;
- creating and updating seasons;
- preventing duplicate `PlayerTeamSeason` records;
- importing the same season more than once;
- handling players who change teams;
- finding players common to two teams;
- handling provider errors and incomplete responses.

At least the following seasons should be validated during the initial import:

```text
2000–01
2005–06
2010–11
2015–16
2020–21
latest available season
```

## 15. Open Decisions

The following decisions remain open:

- final data provider and license;
- exact definition of “played for a team”;
- whether preseason or only official EuroLeague games are included;
- whether player nationality and biography data are required;
- Angular UI design;
- authentication and user accounts;
- quiz generation and scoring rules.
