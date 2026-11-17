using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivateGridWeightsImmediately : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "GridScoringSchedules"
                SET "WeightedFromDate" = LEAST("WeightedFromDate", (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date)
                WHERE "Id" = 1;

                UPDATE "DailyGridChallenges"
                SET "ScoringVersion" = 2
                WHERE "Date" >= (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date
                  AND "ScoringVersion" < 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback keeps saved scoring versions and earned points.
        }
    }
}
