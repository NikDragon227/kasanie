using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations;

public partial class AddProductAnalyticsEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProductAnalyticsEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<string>(type: "text", nullable: true),
                Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                SessionId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                PagePath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                PropertiesJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductAnalyticsEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductAnalyticsEvents_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProductAnalyticsEvents_CreatedAt_Name",
            table: "ProductAnalyticsEvents",
            columns: new[] { "CreatedAt", "Name" });
        migrationBuilder.CreateIndex(
            name: "IX_ProductAnalyticsEvents_SessionId_CreatedAt",
            table: "ProductAnalyticsEvents",
            columns: new[] { "SessionId", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_ProductAnalyticsEvents_UserId",
            table: "ProductAnalyticsEvents",
            column: "UserId",
            filter: "\"UserId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProductAnalyticsEvents");
    }
}
