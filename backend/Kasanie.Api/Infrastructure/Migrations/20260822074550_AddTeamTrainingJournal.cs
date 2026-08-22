using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamTrainingJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamTrainings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CoachId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AttendanceSavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTrainings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamTrainings_CoachProfiles_CoachId",
                        column: x => x.CoachId,
                        principalTable: "CoachProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamTrainings_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamTrainingAttendances",
                columns: table => new
                {
                    TeamTrainingId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTrainingAttendances", x => new { x.TeamTrainingId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_TeamTrainingAttendances_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamTrainingAttendances_TeamTrainings_TeamTrainingId",
                        column: x => x.TeamTrainingId,
                        principalTable: "TeamTrainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamTrainingExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamTrainingId = table.Column<int>(type: "integer", nullable: false),
                    ExerciseId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTrainingExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamTrainingExercises_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamTrainingExercises_TeamTrainings_TeamTrainingId",
                        column: x => x.TeamTrainingId,
                        principalTable: "TeamTrainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamTrainingPlayerResults",
                columns: table => new
                {
                    TeamTrainingExerciseId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    Understood = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTrainingPlayerResults", x => new { x.TeamTrainingExerciseId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_TeamTrainingPlayerResults_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamTrainingPlayerResults_TeamTrainingExercises_TeamTrainin~",
                        column: x => x.TeamTrainingExerciseId,
                        principalTable: "TeamTrainingExercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamTrainingAttendances_PlayerId",
                table: "TeamTrainingAttendances",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTrainingExercises_ExerciseId",
                table: "TeamTrainingExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTrainingExercises_TeamTrainingId_ExerciseId",
                table: "TeamTrainingExercises",
                columns: new[] { "TeamTrainingId", "ExerciseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamTrainingPlayerResults_PlayerId",
                table: "TeamTrainingPlayerResults",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTrainings_CoachId",
                table: "TeamTrainings",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTrainings_TeamId",
                table: "TeamTrainings",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamTrainingAttendances");

            migrationBuilder.DropTable(
                name: "TeamTrainingPlayerResults");

            migrationBuilder.DropTable(
                name: "TeamTrainingExercises");

            migrationBuilder.DropTable(
                name: "TeamTrainings");
        }
    }
}
