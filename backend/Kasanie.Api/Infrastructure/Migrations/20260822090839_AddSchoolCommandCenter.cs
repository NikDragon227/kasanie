using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolCommandCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CycleEnd",
                table: "Teams",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CycleStart",
                table: "Teams",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TacticFormation",
                table: "Teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TacticNotes",
                table: "Teams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingCycleStage",
                table: "Teams",
                type: "text",
                nullable: false,
                defaultValue: "Подготовительный этап");

            migrationBuilder.Sql("""
                UPDATE "Teams"
                SET "Name" = 'Первый состав',
                    "AgeGroup" = 'U17',
                    "TrainingCycleStage" = 'Соревновательный этап'
                WHERE "Name" = 'Основная группа';
                """);

            migrationBuilder.CreateTable(
                name: "TeamMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Opponent = table.Column<string>(type: "text", nullable: false),
                    Competition = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Venue = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    GoalsFor = table.Column<int>(type: "integer", nullable: true),
                    GoalsAgainst = table.Column<int>(type: "integer", nullable: true),
                    LineupNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMatches_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamTournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Placement = table.Column<string>(type: "text", nullable: true),
                    EntryFee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TravelCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    AccommodationCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MealCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    EquipmentCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    OtherCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Income = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamTournaments_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamTrainingGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTrainingGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamTrainingGroups_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamTrainingGroupPlayers",
                columns: table => new
                {
                    TeamTrainingGroupId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTrainingGroupPlayers", x => new { x.TeamTrainingGroupId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_TeamTrainingGroupPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamTrainingGroupPlayers_TeamTrainingGroups_TeamTrainingGro~",
                        column: x => x.TeamTrainingGroupId,
                        principalTable: "TeamTrainingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatches_TeamId",
                table: "TeamMatches",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTournaments_TeamId",
                table: "TeamTournaments",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTrainingGroupPlayers_PlayerId",
                table: "TeamTrainingGroupPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTrainingGroups_TeamId",
                table: "TeamTrainingGroups",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamMatches");

            migrationBuilder.DropTable(
                name: "TeamTournaments");

            migrationBuilder.DropTable(
                name: "TeamTrainingGroupPlayers");

            migrationBuilder.DropTable(
                name: "TeamTrainingGroups");

            migrationBuilder.DropColumn(
                name: "CycleEnd",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "CycleStart",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TacticFormation",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TacticNotes",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "TrainingCycleStage",
                table: "Teams");
        }
    }
}
