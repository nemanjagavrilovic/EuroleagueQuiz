using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RankedGridAttemptsPerDifficulty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_GuestId_Date",
                table: "DailyGridAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_UserId_Date",
                table: "DailyGridAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_UserId_Date_ChallengeId",
                table: "DailyGridAttempts",
                columns: new[] { "UserId", "Date", "ChallengeId" },
                unique: true,
                filter: "\"Ranked\" AND \"UserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_UserId_Date_ChallengeId",
                table: "DailyGridAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_GuestId_Date",
                table: "DailyGridAttempts",
                columns: new[] { "GuestId", "Date" },
                unique: true,
                filter: "\"Ranked\"");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_UserId_Date",
                table: "DailyGridAttempts",
                columns: new[] { "UserId", "Date" },
                unique: true,
                filter: "\"Ranked\" AND \"UserId\" IS NOT NULL");
        }
    }
}
