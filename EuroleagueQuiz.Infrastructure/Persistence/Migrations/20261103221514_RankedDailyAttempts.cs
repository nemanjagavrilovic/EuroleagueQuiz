using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RankedDailyAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LeaderboardEligible",
                table: "DailyGridAttempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "DailyGridAttempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LeaderboardEligible",
                table: "DailyGameAttempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "DailyGameAttempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_UserId_Date",
                table: "DailyGridAttempts",
                columns: new[] { "UserId", "Date" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGameAttempts_UserId_Game_Date",
                table: "DailyGameAttempts",
                columns: new[] { "UserId", "Game", "Date" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyGameAttempts_AspNetUsers_UserId",
                table: "DailyGameAttempts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyGridAttempts_AspNetUsers_UserId",
                table: "DailyGridAttempts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyGameAttempts_AspNetUsers_UserId",
                table: "DailyGameAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyGridAttempts_AspNetUsers_UserId",
                table: "DailyGridAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DailyGridAttempts_UserId_Date",
                table: "DailyGridAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DailyGameAttempts_UserId_Game_Date",
                table: "DailyGameAttempts");

            migrationBuilder.DropColumn(
                name: "LeaderboardEligible",
                table: "DailyGridAttempts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "DailyGridAttempts");

            migrationBuilder.DropColumn(
                name: "LeaderboardEligible",
                table: "DailyGameAttempts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "DailyGameAttempts");
        }
    }
}
