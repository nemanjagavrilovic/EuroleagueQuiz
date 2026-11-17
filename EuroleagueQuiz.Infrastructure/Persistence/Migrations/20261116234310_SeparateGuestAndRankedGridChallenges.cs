using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateGuestAndRankedGridChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyGridChallenges_Date_Difficulty",
                table: "DailyGridChallenges");

            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_GuestId_Date",
                table: "DailyGridAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_UserId_Date",
                table: "DailyGridAttempts");

            migrationBuilder.AddColumn<bool>(
                name: "Ranked",
                table: "DailyGridChallenges",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Ranked",
                table: "DailyGridAttempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // A legacy guest attempt claimed by a signed-in user remains unranked.
            // Only leaderboard-eligible rows resume through the ranked mode.
            migrationBuilder.Sql("""
                UPDATE "DailyGridAttempts" SET "Ranked" = TRUE WHERE "LeaderboardEligible";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridChallenges_Date_Difficulty_Ranked",
                table: "DailyGridChallenges",
                columns: new[] { "Date", "Difficulty", "Ranked" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_GuestId_Date",
                table: "DailyGridAttempts",
                columns: new[] { "GuestId", "Date" },
                unique: true,
                filter: "\"Ranked\"");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_GuestId_Date_ChallengeId",
                table: "DailyGridAttempts",
                columns: new[] { "GuestId", "Date", "ChallengeId" },
                unique: true,
                filter: "NOT \"Ranked\"");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_UserId_Date",
                table: "DailyGridAttempts",
                columns: new[] { "UserId", "Date" },
                unique: true,
                filter: "\"Ranked\" AND \"UserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyGridChallenges_Date_Difficulty_Ranked",
                table: "DailyGridChallenges");

            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_GuestId_Date",
                table: "DailyGridAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_GuestId_Date_ChallengeId",
                table: "DailyGridAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_UserId_Date",
                table: "DailyGridAttempts");

            migrationBuilder.DropColumn(
                name: "Ranked",
                table: "DailyGridChallenges");

            migrationBuilder.DropColumn(
                name: "Ranked",
                table: "DailyGridAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridChallenges_Date_Difficulty",
                table: "DailyGridChallenges",
                columns: new[] { "Date", "Difficulty" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_GuestId_Date",
                table: "DailyGridAttempts",
                columns: new[] { "GuestId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_UserId_Date",
                table: "DailyGridAttempts",
                columns: new[] { "UserId", "Date" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");
        }
    }
}
