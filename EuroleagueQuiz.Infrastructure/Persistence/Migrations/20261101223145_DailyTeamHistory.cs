using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyTeamHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyGameChallenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Game = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    ScoringVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyGameChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyGameAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Game = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ChallengeId = table.Column<long>(type: "bigint", nullable: false),
                    StateJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyGameAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyGameAttempts_DailyGameChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "DailyGameChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyGameAttempts_ChallengeId",
                table: "DailyGameAttempts",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGameAttempts_GuestId_Game_Date",
                table: "DailyGameAttempts",
                columns: new[] { "GuestId", "Game", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyGameChallenges_Game_Date",
                table: "DailyGameChallenges",
                columns: new[] { "Game", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyGameAttempts");

            migrationBuilder.DropTable(
                name: "DailyGameChallenges");
        }
    }
}
