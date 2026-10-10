using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamFirstPlayerHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerTeamSeasons_TeamId",
                table: "PlayerTeamSeasons");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTeamSeasons_TeamId_PlayerId_SeasonId",
                table: "PlayerTeamSeasons",
                columns: new[] { "TeamId", "PlayerId", "SeasonId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerTeamSeasons_TeamId_PlayerId_SeasonId",
                table: "PlayerTeamSeasons");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTeamSeasons_TeamId",
                table: "PlayerTeamSeasons",
                column: "TeamId");
        }
    }
}
