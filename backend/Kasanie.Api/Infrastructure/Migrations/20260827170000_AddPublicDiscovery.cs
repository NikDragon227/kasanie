using System;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260827170000_AddPublicDiscovery")]
public partial class AddPublicDiscovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Sports",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Slug = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Sports", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SportsVenues",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Slug = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                Country = table.Column<string>(type: "text", nullable: false),
                Region = table.Column<string>(type: "text", nullable: false),
                City = table.Column<string>(type: "text", nullable: false),
                District = table.Column<string>(type: "text", nullable: true),
                Address = table.Column<string>(type: "text", nullable: false),
                Latitude = table.Column<double>(type: "double precision", nullable: false),
                Longitude = table.Column<double>(type: "double precision", nullable: false),
                Indoor = table.Column<bool>(type: "boolean", nullable: false),
                SurfaceType = table.Column<string>(type: "text", nullable: true),
                HasChangingRooms = table.Column<bool>(type: "boolean", nullable: false),
                HasLighting = table.Column<bool>(type: "boolean", nullable: false),
                HasParking = table.Column<bool>(type: "boolean", nullable: false),
                ContactPhone = table.Column<string>(type: "text", nullable: true),
                Website = table.Column<string>(type: "text", nullable: true),
                IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SportsVenues", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PublicActivities",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Slug = table.Column<string>(type: "text", nullable: false),
                SportId = table.Column<int>(type: "integer", nullable: false),
                EventType = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "text", nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                OrganizerId = table.Column<string>(type: "text", nullable: false),
                SportsVenueId = table.Column<int>(type: "integer", nullable: false),
                StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TimeZone = table.Column<string>(type: "text", nullable: false),
                IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                RecurrenceRule = table.Column<string>(type: "text", nullable: true),
                Capacity = table.Column<int>(type: "integer", nullable: false),
                WaitlistCapacity = table.Column<int>(type: "integer", nullable: false),
                Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "text", nullable: false),
                SkillLevel = table.Column<string>(type: "text", nullable: false),
                MinimumAge = table.Column<int>(type: "integer", nullable: false),
                MaximumAge = table.Column<int>(type: "integer", nullable: true),
                GenderPolicy = table.Column<string>(type: "text", nullable: false),
                EquipmentRequirements = table.Column<string>(type: "text", nullable: true),
                Rules = table.Column<string>(type: "text", nullable: true),
                CancellationPolicy = table.Column<string>(type: "text", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                Visibility = table.Column<int>(type: "integer", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RegistrationDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublicActivities", x => x.Id);
                table.ForeignKey("FK_PublicActivities_AspNetUsers_OrganizerId", x => x.OrganizerId, "AspNetUsers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PublicActivities_SportsVenues_SportsVenueId", x => x.SportsVenueId, "SportsVenues", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PublicActivities_Sports_SportId", x => x.SportId, "Sports", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PublicActivityParticipants",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                PublicActivityId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CheckedInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Source = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublicActivityParticipants", x => x.Id);
                table.ForeignKey("FK_PublicActivityParticipants_AspNetUsers_UserId", x => x.UserId, "AspNetUsers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PublicActivityParticipants_PublicActivities_PublicActivityId", x => x.PublicActivityId, "PublicActivities", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_Sports_Slug", "Sports", "Slug", unique: true);
        migrationBuilder.CreateIndex("IX_SportsVenues_City_District", "SportsVenues", new[] { "City", "District" });
        migrationBuilder.CreateIndex("IX_SportsVenues_Slug", "SportsVenues", "Slug", unique: true);
        migrationBuilder.CreateIndex("IX_PublicActivities_OrganizerId", "PublicActivities", "OrganizerId");
        migrationBuilder.CreateIndex("IX_PublicActivities_Slug", "PublicActivities", "Slug", unique: true);
        migrationBuilder.CreateIndex("IX_PublicActivities_SportId_SportsVenueId_StartAt", "PublicActivities", new[] { "SportId", "SportsVenueId", "StartAt" });
        migrationBuilder.CreateIndex("IX_PublicActivities_SportsVenueId", "PublicActivities", "SportsVenueId");
        migrationBuilder.CreateIndex("IX_PublicActivities_Status_Visibility_StartAt", "PublicActivities", new[] { "Status", "Visibility", "StartAt" });
        migrationBuilder.CreateIndex("IX_PublicActivityParticipants_PublicActivityId_Status_JoinedAt", "PublicActivityParticipants", new[] { "PublicActivityId", "Status", "JoinedAt" });
        migrationBuilder.CreateIndex("IX_PublicActivityParticipants_PublicActivityId_UserId", "PublicActivityParticipants", new[] { "PublicActivityId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_PublicActivityParticipants_UserId", "PublicActivityParticipants", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("PublicActivityParticipants");
        migrationBuilder.DropTable("PublicActivities");
        migrationBuilder.DropTable("SportsVenues");
        migrationBuilder.DropTable("Sports");
    }
}
