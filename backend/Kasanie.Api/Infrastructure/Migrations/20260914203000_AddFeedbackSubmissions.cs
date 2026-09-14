using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations;

public partial class AddFeedbackSubmissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FeedbackSubmissions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<string>(type: "text", nullable: true),
                Category = table.Column<int>(type: "integer", nullable: false),
                Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                PagePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                TechnicalContext = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                Priority = table.Column<int>(type: "integer", nullable: false),
                ResolutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FeedbackSubmissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_FeedbackSubmissions_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FeedbackSubmissions_Status_Priority_CreatedAt",
            table: "FeedbackSubmissions",
            columns: new[] { "Status", "Priority", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_FeedbackSubmissions_UserId",
            table: "FeedbackSubmissions",
            column: "UserId",
            filter: "\"UserId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FeedbackSubmissions");
    }
}
