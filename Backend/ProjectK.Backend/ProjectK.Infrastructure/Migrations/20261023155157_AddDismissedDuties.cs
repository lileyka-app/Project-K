using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDismissedDuties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DismissedDuties",
                columns: table => new
                {
                    DismissedDutyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DutyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DismissedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DismissedDuties", x => x.DismissedDutyKey);
                    table.ForeignKey(
                        name: "FK_DismissedDuties_AspNetUsers_UserKey",
                        column: x => x.UserKey,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DismissedDuties_DismissedAtUtc",
                table: "DismissedDuties",
                column: "DismissedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DismissedDuties_UserKey_DutyKey",
                table: "DismissedDuties",
                columns: new[] { "UserKey", "DutyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DismissedDuties");
        }
    }
}
