using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260828133500_MakePlayerLocationOptional")]
public partial class MakePlayerLocationOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "MunicipalityId",
            table: "Players",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Players"
            SET "MunicipalityId" = (SELECT "Id" FROM "Municipalities" ORDER BY "Id" LIMIT 1)
            WHERE "MunicipalityId" IS NULL;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "MunicipalityId",
            table: "Players",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
    }
}
