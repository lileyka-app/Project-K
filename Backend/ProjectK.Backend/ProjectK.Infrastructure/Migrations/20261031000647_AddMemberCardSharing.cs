using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberCardSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KurinCardSharing",
                columns: table => new
                {
                    KurinCardSharingKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinorsMayShare = table.Column<bool>(type: "bit", nullable: false),
                    MinorFields = table.Column<int>(type: "int", nullable: false),
                    MinorsMayLink = table.Column<bool>(type: "bit", nullable: false),
                    SetByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurinCardSharing", x => x.KurinCardSharingKey);
                });

            migrationBuilder.CreateTable(
                name: "MemberCardShares",
                columns: table => new
                {
                    MemberCardShareKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fields = table.Column<int>(type: "int", nullable: false),
                    LinkToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LinkIssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberCardShares", x => x.MemberCardShareKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KurinCardSharing_KurinKey",
                table: "KurinCardSharing",
                column: "KurinKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberCardShares_LinkToken",
                table: "MemberCardShares",
                column: "LinkToken",
                unique: true,
                filter: "[LinkToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberCardShares_MemberKey",
                table: "MemberCardShares",
                column: "MemberKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KurinCardSharing");

            migrationBuilder.DropTable(
                name: "MemberCardShares");
        }
    }
}
