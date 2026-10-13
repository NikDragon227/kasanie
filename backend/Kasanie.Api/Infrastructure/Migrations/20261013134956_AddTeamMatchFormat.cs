using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261013134956_AddTeamMatchFormat")]
public partial class AddTeamMatchFormat : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MatchFormat",
            table: "Teams",
            type: "text",
            nullable: false,
            defaultValue: "11v11");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MatchFormat",
            table: "Teams");
    }
}
