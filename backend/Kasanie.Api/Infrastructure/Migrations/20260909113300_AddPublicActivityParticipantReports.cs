using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicActivityParticipantReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicActivityParticipantReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PublicActivityId = table.Column<int>(type: "integer", nullable: false),
                    PublicActivityParticipantId = table.Column<long>(type: "bigint", nullable: true),
                    AuthorOrganizerId = table.Column<string>(type: "text", nullable: false),
                    SubjectUserId = table.Column<string>(type: "text", nullable: true),
                    SubjectGuestContactHash = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicActivityParticipantReports", x => x.Id);
                    table.CheckConstraint("CK_ParticipantReport_OneSubject", "(\"SubjectUserId\" IS NOT NULL) <> (\"SubjectGuestContactHash\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PublicActivityParticipantReports_AspNetUsers_AuthorOrganize~",
                        column: x => x.AuthorOrganizerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicActivityParticipantReports_AspNetUsers_SubjectUserId",
                        column: x => x.SubjectUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicActivityParticipantReports_PublicActivities_PublicAct~",
                        column: x => x.PublicActivityId,
                        principalTable: "PublicActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicActivityParticipantReports_AuthorOrganizerId_PublicAc~",
                table: "PublicActivityParticipantReports",
                columns: new[] { "AuthorOrganizerId", "PublicActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicActivityParticipantReports_PublicActivityId",
                table: "PublicActivityParticipantReports",
                column: "PublicActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicActivityParticipantReports_SubjectGuestContactHash",
                table: "PublicActivityParticipantReports",
                column: "SubjectGuestContactHash",
                filter: "\"SubjectGuestContactHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PublicActivityParticipantReports_SubjectUserId",
                table: "PublicActivityParticipantReports",
                column: "SubjectUserId",
                filter: "\"SubjectUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicActivityParticipantReports");
        }
    }
}
