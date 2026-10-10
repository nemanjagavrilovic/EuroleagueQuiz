namespace EuroleagueQuiz.Application.DTOs;

public sealed record MissingClubRoundDto(
    Guid RoundId,
    PlayerDto Player,
    IReadOnlyList<TeamDto> KnownTeams,
    IReadOnlyList<TeamDto> Options,
    bool Completed,
    bool? Correct,
    TeamDto? CorrectTeam);

public sealed record MissingClubGuessRequest(Guid RoundId, long? TeamId, bool Skip = false);

public sealed record CareerOrderClubDto(long Id, string Name, string? ImageUrl);

public sealed record CareerOrderRoundDto(
    Guid RoundId,
    PlayerDto Player,
    IReadOnlyList<CareerOrderClubDto> Clubs,
    bool Completed,
    bool? Correct,
    IReadOnlyList<CareerOrderClubDto>? CorrectOrder);

public sealed record CareerOrderGuessRequest(Guid RoundId, IReadOnlyList<long> TeamIds);

public sealed record MysteryPlayerRoundDto(
    Guid RoundId,
    IReadOnlyList<string> RevealedClues,
    int RevealedClueCount,
    int TotalClues,
    bool Completed,
    bool? Correct,
    PlayerDto? CorrectPlayer);

public sealed record MysteryPlayerGuessRequest(Guid RoundId, long? PlayerId, bool Skip = false);
