using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations;

public partial class AddTeamTrainingTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TeamTrainingTemplates",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TeamId = table.Column<int>(type: "integer", nullable: false),
                CoachId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamTrainingTemplates", x => x.Id);
                table.ForeignKey(name: "FK_TeamTrainingTemplates_CoachProfiles_CoachId", column: x => x.CoachId, principalTable: "CoachProfiles", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_TeamTrainingTemplates_Teams_TeamId", column: x => x.TeamId, principalTable: "Teams", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TeamTrainingTemplateExercises",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TeamTrainingTemplateId = table.Column<int>(type: "integer", nullable: false),
                ExerciseId = table.Column<int>(type: "integer", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamTrainingTemplateExercises", x => x.Id);
                table.ForeignKey(name: "FK_TeamTrainingTemplateExercises_Exercises_ExerciseId", column: x => x.ExerciseId, principalTable: "Exercises", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_TeamTrainingTemplateExercises_TeamTrainingTemplates_TeamTrainingTemplateId", column: x => x.TeamTrainingTemplateId, principalTable: "TeamTrainingTemplates", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_TeamTrainingTemplates_CoachId", table: "TeamTrainingTemplates", column: "CoachId");
        migrationBuilder.CreateIndex(name: "IX_TeamTrainingTemplates_TeamId_CoachId_Name", table: "TeamTrainingTemplates", columns: new[] { "TeamId", "CoachId", "Name" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_TeamTrainingTemplateExercises_ExerciseId", table: "TeamTrainingTemplateExercises", column: "ExerciseId");
        migrationBuilder.CreateIndex(name: "IX_TeamTrainingTemplateExercises_TeamTrainingTemplateId_ExerciseId", table: "TeamTrainingTemplateExercises", columns: new[] { "TeamTrainingTemplateId", "ExerciseId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TeamTrainingTemplateExercises");
        migrationBuilder.DropTable(name: "TeamTrainingTemplates");
    }
}
