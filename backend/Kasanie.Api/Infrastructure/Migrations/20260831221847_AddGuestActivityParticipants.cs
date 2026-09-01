using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestActivityParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PublicActivityParticipants",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "GuestContact",
                table: "PublicActivityParticipants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestContactHash",
                table: "PublicActivityParticipants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                table: "PublicActivityParticipants",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicActivityParticipants_PublicActivityId_GuestContactHash",
                table: "PublicActivityParticipants",
                columns: new[] { "PublicActivityId", "GuestContactHash" },
                unique: true,
                filter: "\"GuestContactHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicActivityParticipants_PublicActivityId_GuestContactHash",
                table: "PublicActivityParticipants");

            migrationBuilder.DropColumn(
                name: "GuestContact",
                table: "PublicActivityParticipants");

            migrationBuilder.DropColumn(
                name: "GuestContactHash",
                table: "PublicActivityParticipants");

            migrationBuilder.DropColumn(
                name: "GuestName",
                table: "PublicActivityParticipants");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "PublicActivityParticipants",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
