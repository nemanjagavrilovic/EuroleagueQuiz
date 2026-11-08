using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LeaderboardScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "DailyGridAttempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "DailyGridAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "DailyGridAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "DailyGameAttempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "DailyGameAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "DailyGameAttempts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                WITH scores AS (
                    SELECT attempt."Id",
                           COUNT(answer) FILTER (WHERE answer IS NOT NULL AND COALESCE((answer ->> 'Revealed')::boolean, false) = false)::integer AS correct
                    FROM "DailyGridAttempts" AS attempt
                    LEFT JOIN LATERAL jsonb_array_elements(COALESCE(attempt."StateJson" -> 'Answers', '[]'::jsonb)) AS answer ON true
                    GROUP BY attempt."Id"
                )
                UPDATE "DailyGridAttempts" AS attempt
                SET "Score" = scores.correct * 10 + CASE WHEN scores.correct = 9 THEN 10 ELSE 0 END,
                    "Completed" = COALESCE((attempt."StateJson" ->> 'Completed')::boolean, false)
                FROM scores
                WHERE scores."Id" = attempt."Id";

                UPDATE "DailyGameAttempts"
                SET "Score" = CASE
                        WHEN "Game" = 'five-player' THEN COALESCE((
                            SELECT SUM(jsonb_array_length(COALESCE(round -> 'GuessedPlayerIds', '[]'::jsonb)))::integer * 4
                            FROM jsonb_array_elements(COALESCE("StateJson" -> 'Rounds', '[]'::jsonb)) AS round
                        ), 0)
                        ELSE COALESCE(("StateJson" ->> 'Score')::integer, 0)
                    END,
                    "Completed" = CASE
                        WHEN "Game" = 'five-player' THEN
                            jsonb_array_length(COALESCE("StateJson" -> 'Rounds', '[]'::jsonb)) = 5
                            AND COALESCE((
                                "StateJson" -> 'Rounds' ->
                                (jsonb_array_length("StateJson" -> 'Rounds') - 1) ->> 'Completed'
                            )::boolean, false)
                        ELSE COALESCE(("StateJson" ->> 'Completed')::boolean, false)
                    END;

                UPDATE "DailyGridAttempts"
                SET "CompletedAtUtc" = "UpdatedAtUtc"
                WHERE "Completed";

                UPDATE "DailyGameAttempts"
                SET "CompletedAtUtc" = "UpdatedAtUtc"
                WHERE "Completed";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_Date_UserId",
                table: "DailyGridAttempts",
                columns: new[] { "Date", "UserId" },
                filter: "\"LeaderboardEligible\" AND \"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGameAttempts_Game_Date_UserId",
                table: "DailyGameAttempts",
                columns: new[] { "Game", "Date", "UserId" },
                filter: "\"LeaderboardEligible\" AND \"UserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_Date_UserId",
                table: "DailyGridAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DailyGameAttempts_Game_Date_UserId",
                table: "DailyGameAttempts");

            migrationBuilder.DropColumn(
                name: "Completed",
                table: "DailyGridAttempts");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "DailyGridAttempts");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "DailyGridAttempts");

            migrationBuilder.DropColumn(
                name: "Completed",
                table: "DailyGameAttempts");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "DailyGameAttempts");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "DailyGameAttempts");
        }
    }
}
