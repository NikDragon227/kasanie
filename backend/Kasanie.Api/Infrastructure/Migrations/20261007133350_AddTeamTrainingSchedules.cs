using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations;

public partial class AddTeamTrainingSchedules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "ScheduleSlotId", table: "TeamTrainings", type: "integer", nullable: true);

        migrationBuilder.CreateTable(
            name: "TeamTrainingScheduleSlots",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TeamId = table.Column<int>(type: "integer", nullable: false),
                CoachId = table.Column<int>(type: "integer", nullable: false),
                TeamTrainingTemplateId = table.Column<int>(type: "integer", nullable: false),
                DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                StartsAtLocal = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                UtcOffsetMinutes = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamTrainingScheduleSlots", x => x.Id);
                table.ForeignKey(name: "FK_TeamTrainingScheduleSlots_CoachProfiles_CoachId", column: x => x.CoachId, principalTable: "CoachProfiles", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_TeamTrainingScheduleSlots_TeamTrainingTemplates_TeamTrainingTemplateId", column: x => x.TeamTrainingTemplateId, principalTable: "TeamTrainingTemplates", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_TeamTrainingScheduleSlots_Teams_TeamId", column: x => x.TeamId, principalTable: "Teams", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_TeamTrainings_ScheduleSlotId_ScheduledAt", table: "TeamTrainings", columns: new[] { "ScheduleSlotId", "ScheduledAt" }, unique: true, filter: "\"ScheduleSlotId\" IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_TeamTrainingScheduleSlots_CoachId", table: "TeamTrainingScheduleSlots", column: "CoachId");
        migrationBuilder.CreateIndex(name: "IX_TeamTrainingScheduleSlots_TeamId_DayOfWeek_StartsAtLocal", table: "TeamTrainingScheduleSlots", columns: new[] { "TeamId", "DayOfWeek", "StartsAtLocal" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_TeamTrainingScheduleSlots_TeamTrainingTemplateId", table: "TeamTrainingScheduleSlots", column: "TeamTrainingTemplateId");

        migrationBuilder.AddForeignKey(name: "FK_TeamTrainings_TeamTrainingScheduleSlots_ScheduleSlotId", table: "TeamTrainings", column: "ScheduleSlotId", principalTable: "TeamTrainingScheduleSlots", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_TeamTrainings_TeamTrainingScheduleSlots_ScheduleSlotId", table: "TeamTrainings");
        migrationBuilder.DropTable(name: "TeamTrainingScheduleSlots");
        migrationBuilder.DropIndex(name: "IX_TeamTrainings_ScheduleSlotId_ScheduledAt", table: "TeamTrainings");
        migrationBuilder.DropColumn(name: "ScheduleSlotId", table: "TeamTrainings");
    }
}
