namespace EuroleagueQuiz.Domain.Entities;

public sealed class GridScoringSchedule
{
    public int Id { get; set; } = 1;
    public DateOnly WeightedFromDate { get; set; }
}
