using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachCommandCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RegistrationDeadline",
                table: "TeamTournaments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "TeamTournaments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodeOfConduct",
                table: "Teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpponentInstructions",
                table: "Teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpponentReportNotes",
                table: "Teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpponentReportUrl",
                table: "Teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetPiecesJson",
                table: "Teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TacticPlanJson",
                table: "Teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentSeasonPlan",
                table: "TeamPlayers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NextSeasonPlan",
                table: "TeamPlayers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TournamentRegistrationStatus",
                table: "TeamPlayers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TwoYearPlan",
                table: "TeamPlayers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "TeamInjuries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpectedReturnOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ClosedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamInjuries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamInjuries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamInjuries_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    AuthorUserId = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMessages_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMessages_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamScheduleEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReminderAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamScheduleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamScheduleEvents_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamPlayers_TeamId_ShirtNumber",
                table: "TeamPlayers",
                columns: new[] { "TeamId", "ShirtNumber" },
                unique: true,
                filter: "\"IsActive\" AND \"ShirtNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInjuries_PlayerId",
                table: "TeamInjuries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamInjuries_TeamId_Status",
                table: "TeamInjuries",
                columns: new[] { "TeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMessages_AuthorUserId",
                table: "TeamMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMessages_TeamId_Channel_CreatedAt",
                table: "TeamMessages",
                columns: new[] { "TeamId", "Channel", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamScheduleEvents_TeamId_StartsAt",
                table: "TeamScheduleEvents",
                columns: new[] { "TeamId", "StartsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamInjuries");

            migrationBuilder.DropTable(
                name: "TeamMessages");

            migrationBuilder.DropTable(
                name: "TeamScheduleEvents");

            migrationBuilder.DropIndex(
                name: "IX_TeamPlayers_TeamId_ShirtNumber",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "RegistrationDeadline",
                table: "TeamTournaments");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "TeamTournaments");

            migrationBuilder.DropColumn(
                name: "CodeOfConduct",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "OpponentInstructions",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "OpponentReportNotes",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "OpponentReportUrl",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "SetPiecesJson",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TacticPlanJson",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CurrentSeasonPlan",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "NextSeasonPlan",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "TournamentRegistrationStatus",
                table: "TeamPlayers");

            migrationBuilder.DropColumn(
                name: "TwoYearPlan",
                table: "TeamPlayers");
        }
    }
}
