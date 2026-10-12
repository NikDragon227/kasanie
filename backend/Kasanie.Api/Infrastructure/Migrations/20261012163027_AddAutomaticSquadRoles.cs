using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticSquadRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CleanSheets",
                table: "TeamPlayers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SquadRoleOverride",
                table: "TeamPlayers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CleanSheets",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "SquadRoleOverride",
                table: "TeamPlayers");
        }
    }
}
