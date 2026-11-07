using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKurinArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DissolvedAtUtc",
                table: "Groups",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MembershipGroupStints",
                columns: table => new
                {
                    MembershipGroupStintKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ToUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipGroupStints", x => x.MembershipGroupStintKey);
                    table.ForeignKey(
                        name: "FK_MembershipGroupStints_Memberships_MembershipKey",
                        column: x => x.MembershipKey,
                        principalTable: "Memberships",
                        principalColumn: "MembershipKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipGroupStints_GroupKey",
                table: "MembershipGroupStints",
                column: "GroupKey");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipGroupStints_MembershipKey_ToUtc",
                table: "MembershipGroupStints",
                columns: new[] { "MembershipKey", "ToUtc" });

            // Whoever stands in a гурток now gets a stint with no known start: the record begins here.
            migrationBuilder.Sql("""
                INSERT INTO MembershipGroupStints (MembershipGroupStintKey, MembershipKey, GroupKey, FromUtc, ToUtc)
                SELECT NEWID(), m.MembershipKey, m.GroupKey, NULL, NULL
                FROM Memberships m
                WHERE m.GroupKey IS NOT NULL
                  AND m.LeftAtUtc IS NULL
                  AND NOT EXISTS (SELECT 1 FROM MembershipGroupStints s WHERE s.MembershipKey = m.MembershipKey);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembershipGroupStints");

            migrationBuilder.DropColumn(
                name: "DissolvedAtUtc",
                table: "Groups");
        }
    }
}
