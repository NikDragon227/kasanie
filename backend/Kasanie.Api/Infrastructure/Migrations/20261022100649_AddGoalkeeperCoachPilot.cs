using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalkeeperCoachPilot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoalkeeperCoachAssignments",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CoachId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalkeeperCoachAssignments", x => new { x.TeamId, x.CoachId });
                    table.ForeignKey(
                        name: "FK_GoalkeeperCoachAssignments_CoachProfiles_CoachId",
                        column: x => x.CoachId,
                        principalTable: "CoachProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalkeeperCoachAssignments_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalkeeperExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CoachId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Equipment = table.Column<string>(type: "text", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalkeeperExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalkeeperExercises_CoachProfiles_CoachId",
                        column: x => x.CoachId,
                        principalTable: "CoachProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalkeeperExercises_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalkeeperPlayerRecords",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CoachId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Goal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalkeeperPlayerRecords", x => new { x.TeamId, x.CoachId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_GoalkeeperPlayerRecords_CoachProfiles_CoachId",
                        column: x => x.CoachId,
                        principalTable: "CoachProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalkeeperPlayerRecords_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalkeeperPlayerRecords_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalkeeperTrainings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CoachId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StateJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalkeeperTrainings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalkeeperTrainings_CoachProfiles_CoachId",
                        column: x => x.CoachId,
                        principalTable: "CoachProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalkeeperTrainings_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalkeeperCoachAssignments_CoachId",
                table: "GoalkeeperCoachAssignments",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalkeeperExercises_CoachId",
                table: "GoalkeeperExercises",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalkeeperExercises_TeamId_CoachId_IsActive",
                table: "GoalkeeperExercises",
                columns: new[] { "TeamId", "CoachId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalkeeperPlayerRecords_CoachId",
                table: "GoalkeeperPlayerRecords",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalkeeperPlayerRecords_PlayerId",
                table: "GoalkeeperPlayerRecords",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalkeeperTrainings_CoachId",
                table: "GoalkeeperTrainings",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalkeeperTrainings_TeamId_CoachId_ScheduledAt",
                table: "GoalkeeperTrainings",
                columns: new[] { "TeamId", "CoachId", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalkeeperCoachAssignments");

            migrationBuilder.DropTable(
                name: "GoalkeeperExercises");

            migrationBuilder.DropTable(
                name: "GoalkeeperPlayerRecords");

            migrationBuilder.DropTable(
                name: "GoalkeeperTrainings");
        }
    }
}
