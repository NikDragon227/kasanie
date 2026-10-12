using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachRosterManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxRosterSize",
                table: "TeamTournaments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationRequirements",
                table: "TeamTournaments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Appearances",
                table: "TeamPlayers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Assists",
                table: "TeamPlayers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageRating",
                table: "TeamPlayers",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Goals",
                table: "TeamPlayers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsCaptain",
                table: "TeamPlayers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Citizenship",
                table: "Players",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondCitizenship",
                table: "Players",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamTournamentRegistrations",
                columns: table => new
                {
                    TeamTournamentId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTournamentRegistrations", x => new { x.TeamTournamentId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_TeamTournamentRegistrations_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamTournamentRegistrations_TeamTournaments_TeamTournamentId",
                        column: x => x.TeamTournamentId,
                        principalTable: "TeamTournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_TeamId_IsCaptain",
                table: "TeamPlayers",
                columns: new[] { "TeamId", "IsCaptain" },
                unique: true,
                filter: "\"IsCaptain\"");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTournamentRegistrations_PlayerId",
                table: "TeamTournamentRegistrations",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamTournamentRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_TeamPlayers_TeamId_IsCaptain",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "MaxRosterSize",
                table: "TeamTournaments");

            migrationBuilder.DropColumn(
                name: "RegistrationRequirements",
                table: "TeamTournaments");

            migrationBuilder.DropColumn(
                name: "Appearances",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "Assists",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "Goals",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "IsCaptain",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "Citizenship",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SecondCitizenship",
                table: "Players");
        }
    }
}
