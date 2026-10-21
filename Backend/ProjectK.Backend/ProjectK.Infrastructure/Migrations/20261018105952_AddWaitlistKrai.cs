using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistKrai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KraiCode",
                table: "WaitlistEntries",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_KraiCode",
                table: "WaitlistEntries",
                column: "KraiCode");

            migrationBuilder.AddForeignKey(
                name: "FK_WaitlistEntries_Krais_KraiCode",
                table: "WaitlistEntries",
                column: "KraiCode",
                principalTable: "Krais",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WaitlistEntries_Krais_KraiCode",
                table: "WaitlistEntries");

            migrationBuilder.DropIndex(
                name: "IX_WaitlistEntries_KraiCode",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "KraiCode",
                table: "WaitlistEntries");
        }
    }
}
