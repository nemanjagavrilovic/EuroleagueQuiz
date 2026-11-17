using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GridScoringCutover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GridScoringSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    WeightedFromDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GridScoringSchedules", x => x.Id);
                    table.CheckConstraint("CK_GridScoringSchedules_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.Sql("""
                INSERT INTO "GridScoringSchedules" ("Id", "WeightedFromDate")
                VALUES (1, (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date + 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GridScoringSchedules");
        }
    }
}
