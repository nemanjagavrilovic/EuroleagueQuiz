namespace EuroleagueQuiz.Application.DTOs;

public sealed record MissingClubChallengeDto(PlayerDto Player, IReadOnlyList<TeamDto> KnownTeams, IReadOnlyList<TeamDto> Options, TeamDto CorrectTeam);
public sealed record DailyMissingClubGuessRequest(long ChallengeId, Guid CommandId, long? TeamId, bool Skip = false);
public sealed record DailyMissingClubDto(long ChallengeId, DateOnly Date, DateTime ResetsAtUtc, MissingClubRoundDto Round,
    int Score, bool LeaderboardEligible, long? SelectedTeamId, string? Feedback);
