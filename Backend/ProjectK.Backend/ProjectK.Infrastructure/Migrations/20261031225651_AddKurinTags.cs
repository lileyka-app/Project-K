using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKurinTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KurinTags",
                columns: table => new
                {
                    KurinTagKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ColorHex = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurinTags", x => x.KurinTagKey);
                    table.ForeignKey(
                        name: "FK_KurinTags_Kurins_KurinKey",
                        column: x => x.KurinKey,
                        principalTable: "Kurins",
                        principalColumn: "KurinKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KurinTagSettings",
                columns: table => new
                {
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: true),
                    OwnTagsVisible = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurinTagSettings", x => x.KurinKey);
                    table.ForeignKey(
                        name: "FK_KurinTagSettings_Kurins_KurinKey",
                        column: x => x.KurinKey,
                        principalTable: "Kurins",
                        principalColumn: "KurinKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberTags",
                columns: table => new
                {
                    KurinTagKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberTags", x => new { x.KurinTagKey, x.MemberKey });
                    table.ForeignKey(
                        name: "FK_MemberTags_KurinTags_KurinTagKey",
                        column: x => x.KurinTagKey,
                        principalTable: "KurinTags",
                        principalColumn: "KurinTagKey",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberTags_Members_MemberKey",
                        column: x => x.MemberKey,
                        principalTable: "Members",
                        principalColumn: "MemberKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KurinTags_KurinKey_Name",
                table: "KurinTags",
                columns: new[] { "KurinKey", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberTags_MemberKey",
                table: "MemberTags",
                column: "MemberKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KurinTagSettings");

            migrationBuilder.DropTable(
                name: "MemberTags");

            migrationBuilder.DropTable(
                name: "KurinTags");
        }
    }
}
