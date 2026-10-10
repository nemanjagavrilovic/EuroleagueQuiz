using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Infrastructure.DataProviders.InCrowd;

public sealed class InCrowdImportService : IDataImportService
{
    private const string Source = "InCrowdEuroleagueV2";
    private readonly IBasketballDataProvider _provider;
    private readonly IPlayerRepository _players;
    private readonly ITeamRepository _teams;
    private readonly ISeasonRepository _seasons;
    private readonly IPlayerTeamSeasonRepository _relationships;
    private readonly IUnitOfWork _unitOfWork;

    public InCrowdImportService(
        IBasketballDataProvider provider,
        IPlayerRepository players,
        ITeamRepository teams,
        ISeasonRepository seasons,
        IPlayerTeamSeasonRepository relationships,
        IUnitOfWork unitOfWork)
    {
        _provider = provider;
        _players = players;
        _teams = teams;
        _seasons = seasons;
        _relationships = relationships;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ImportResult>> ImportAllAvailableSeasonsAsync(CancellationToken cancellationToken)
    {
        var seasons = await _provider.GetSeasonsAsync(cancellationToken);
        var results = new List<ImportResult>();
        foreach (var season in seasons.Where(x => x.StartYear >= 2013))
            results.Add(await ImportSeasonAsync(season.ExternalId, cancellationToken));
        return results;
    }

    public async Task<ImportResult> ImportSeasonAsync(string seasonExternalId, CancellationToken cancellationToken)
    {
        var providerSeason = (await _provider.GetSeasonsAsync(cancellationToken))
            .FirstOrDefault(x => x.ExternalId.Equals(seasonExternalId, StringComparison.OrdinalIgnoreCase));

        if (providerSeason is null)
            throw new KeyNotFoundException($"Season '{seasonExternalId}' was not returned by the provider.");

        var season = await _seasons.GetByExternalIdAsync(providerSeason.ExternalId, cancellationToken);
        if (season is null)
        {
            season = new Season(providerSeason.ExternalId, providerSeason.Name, providerSeason.StartYear, providerSeason.EndYear, providerSeason.CompetitionCode);
            await _seasons.AddAsync(season, cancellationToken);
        }
        season.Update(providerSeason.Name, providerSeason.StartYear, providerSeason.EndYear, providerSeason.Alias, providerSeason.StartDate, providerSeason.ActivationDate, providerSeason.EndDate);

        var teamsRead = 0;
        var playersRead = 0;
        var relationshipsCreated = 0;
        var teamCache = new Dictionary<string, Team>(StringComparer.OrdinalIgnoreCase);
        var playerCache = new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
        var pendingRelationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var providerTeams = await _provider.GetTeamsAsync(providerSeason.ExternalId, cancellationToken);
        foreach (var providerTeam in providerTeams)
        {
            cancellationToken.ThrowIfCancellationRequested();
            teamsRead++;

            if (!teamCache.TryGetValue(providerTeam.ExternalId, out var team))
            {
                team = await _teams.GetByExternalIdAsync(providerTeam.ExternalId, cancellationToken)
                    ?? new Team(providerTeam.ExternalId, providerTeam.Name);

                if (team.Id == 0)
                {
                    team.Update(providerTeam.Name, providerTeam.ShortName, providerTeam.Country, providerTeam.CountryCode, providerTeam.TvCode, providerTeam.PermanentName, providerTeam.PermanentAlias, providerTeam.IsVirtual, providerTeam.ImageUrl);
                    await _teams.AddAsync(team, cancellationToken);
                }
                else
                    team.Update(providerTeam.Name, providerTeam.ShortName, providerTeam.Country, providerTeam.CountryCode, providerTeam.TvCode, providerTeam.PermanentName, providerTeam.PermanentAlias, providerTeam.IsVirtual, providerTeam.ImageUrl);

                teamCache[providerTeam.ExternalId] = team;
            }

            var providerPlayers = await _provider.GetTeamPlayersAsync(providerSeason.ExternalId, providerTeam.ExternalId, cancellationToken);
            foreach (var providerPlayer in providerPlayers)
            {
                playersRead++;
                if (!playerCache.TryGetValue(providerPlayer.ExternalId, out var player))
                {
                    player = await _players.GetByExternalIdAsync(providerPlayer.ExternalId, cancellationToken)
                        ?? new Player(providerPlayer.ExternalId, providerPlayer.DisplayName);

                    if (player.Id == 0)
                    {
                        player.Update(providerPlayer.DisplayName, providerPlayer.FirstName, providerPlayer.LastName, providerPlayer.Nationality, providerPlayer.NationalityCode, providerPlayer.DateOfBirth, providerPlayer.ProviderExternalId, providerPlayer.ImageUrl);
                        await _players.AddAsync(player, cancellationToken);
                    }
                    else
                        player.Update(providerPlayer.DisplayName, providerPlayer.FirstName, providerPlayer.LastName, providerPlayer.Nationality, providerPlayer.NationalityCode, providerPlayer.DateOfBirth, providerPlayer.ProviderExternalId, providerPlayer.ImageUrl);

                    playerCache[providerPlayer.ExternalId] = player;
                }

                var relationshipKey = $"{providerPlayer.ExternalId}|{providerTeam.ExternalId}|{providerSeason.ExternalId}";
                if (!pendingRelationships.Add(relationshipKey)) continue;

                var existing = await _relationships.FindAsync(player.Id, team.Id, season.Id, cancellationToken);
                if (existing is null)
                {
                    var relationship = new PlayerTeamSeason(player, team, season, Source);
                    relationship.SetRosterData(providerPlayer.ProviderExternalId, providerPlayer.Active, providerPlayer.StartDate, providerPlayer.EndDate, providerPlayer.JerseyNumber, providerPlayer.Position, providerPlayer.PositionName);
                    await _relationships.AddAsync(relationship, cancellationToken);
                    relationshipsCreated++;
                }
                else
                    existing.SetRosterData(providerPlayer.ProviderExternalId, providerPlayer.Active, providerPlayer.StartDate, providerPlayer.EndDate, providerPlayer.JerseyNumber, providerPlayer.Position, providerPlayer.PositionName);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ImportResult(providerSeason.ExternalId, teamsRead, playersRead, relationshipsCreated);
    }
}
