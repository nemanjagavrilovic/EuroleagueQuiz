using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EuroleagueQuiz.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EuroleagueDbContext))]
[Migration("20261016143000_AddGoogleSignIn")]
public partial class AddGoogleSignIn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "PasswordHash",
            table: "AppUsers",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(512)",
            oldMaxLength: 512);

        migrationBuilder.AddColumn<string>(
            name: "GoogleSubject",
            table: "AppUsers",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AppUsers_GoogleSubject",
            table: "AppUsers",
            column: "GoogleSubject",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AppUsers_GoogleSubject", table: "AppUsers");
        migrationBuilder.DropColumn(name: "GoogleSubject", table: "AppUsers");
        migrationBuilder.AlterColumn<string>(
            name: "PasswordHash",
            table: "AppUsers",
            type: "character varying(512)",
            maxLength: 512,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(512)",
            oldMaxLength: 512,
            oldNullable: true);
    }
}
