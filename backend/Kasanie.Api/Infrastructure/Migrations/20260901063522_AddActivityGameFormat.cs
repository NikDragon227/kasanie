using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityGameFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GameFormat",
                table: "PublicActivities",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameFormat",
                table: "PublicActivities");
        }
    }
}
