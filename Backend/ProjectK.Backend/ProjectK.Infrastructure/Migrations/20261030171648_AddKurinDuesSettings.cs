using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKurinDuesSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KurinDuesSettings",
                columns: table => new
                {
                    KurinDuesSettingsKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChargeTiming = table.Column<int>(type: "int", nullable: false),
                    SetByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurinDuesSettings", x => x.KurinDuesSettingsKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KurinDuesSettings_KurinKey",
                table: "KurinDuesSettings",
                column: "KurinKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KurinDuesSettings");
        }
    }
}
