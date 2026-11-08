namespace EuroleagueQuiz.Application.DTOs;

public sealed record LeaderboardDto(
    string Period,
    string Game,
    DateOnly? FromDate,
    DateOnly ToDate,
    int? MaximumScore,
    IReadOnlyList<LeaderboardEntryDto> Entries,
    LeaderboardEntryDto? CurrentUser,
    int Page,
    int PageSize,
    int TotalEntries,
    int TotalPages);

public sealed record LeaderboardEntryDto(
    int Rank,
    string DisplayName,
    int Score,
    int GamesPlayed,
    int CompletedGames,
    int PerfectGames,
    bool IsCurrentUser);

public sealed record LeaderboardAggregate(
    Guid UserId,
    string DisplayName,
    int Score,
    int GamesPlayed,
    int CompletedGames,
    int PerfectGames,
    DateTime? LastCompletedAtUtc);
