using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kasanie.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCancellationToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestCancellationTokenHash",
                table: "PublicActivityParticipants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicActivityParticipants_GuestCancellationTokenHash",
                table: "PublicActivityParticipants",
                column: "GuestCancellationTokenHash",
                unique: true,
                filter: "\"GuestCancellationTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PublicActivityParticipants_GuestCancellationTokenHash",
                table: "PublicActivityParticipants");

            migrationBuilder.DropColumn(
                name: "GuestCancellationTokenHash",
                table: "PublicActivityParticipants");
        }
    }
}
