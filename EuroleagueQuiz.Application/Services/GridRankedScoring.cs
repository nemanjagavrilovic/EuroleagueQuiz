using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Services;

public static class GridRankedScoring
{
    public const int WeightedVersion = 2;
    public const int EasyMaximum = 10;
    public const int NormalMaximum = 20;
    public const int HardMaximum = 30;
    public const int LegendMaximum = 40;

    public static int MaximumPoints(string difficulty) => difficulty switch
    {
        nameof(GridDifficulty.Easy) => EasyMaximum,
        nameof(GridDifficulty.Normal) => NormalMaximum,
        nameof(GridDifficulty.Hard) => HardMaximum,
        nameof(GridDifficulty.Legend) => LegendMaximum,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown grid difficulty.")
    };
}
