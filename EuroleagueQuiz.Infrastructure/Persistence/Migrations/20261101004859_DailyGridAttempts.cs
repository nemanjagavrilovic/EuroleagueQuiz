using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyGridAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyGridChallenges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    ScoringVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyGridChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyGridAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ChallengeId = table.Column<long>(type: "bigint", nullable: false),
                    StateJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyGridAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyGridAttempts_DailyGridChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "DailyGridChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_ChallengeId",
                table: "DailyGridAttempts",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridAttempts_GuestId_Date",
                table: "DailyGridAttempts",
                columns: new[] { "GuestId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyGridChallenges_Date_Difficulty",
                table: "DailyGridChallenges",
                columns: new[] { "Date", "Difficulty" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyGridAttempts");

            migrationBuilder.DropTable(
                name: "DailyGridChallenges");
        }
    }
}
