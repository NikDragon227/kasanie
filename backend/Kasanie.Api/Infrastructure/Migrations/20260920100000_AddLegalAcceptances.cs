using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations;

public partial class AddLegalAcceptances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(name: "PrivacyPolicyAcceptedAt", table: "AspNetUsers", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "PrivacyPolicyVersion", table: "AspNetUsers", type: "text", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "TermsAcceptedAt", table: "AspNetUsers", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "TermsVersion", table: "AspNetUsers", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PrivacyPolicyAcceptedAt", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "PrivacyPolicyVersion", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "TermsAcceptedAt", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "TermsVersion", table: "AspNetUsers");
    }
}
