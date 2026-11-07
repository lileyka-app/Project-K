using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeniorKurins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Kurins_KraiCode_Number",
                table: "Kurins");

            migrationBuilder.AddColumn<int>(
                name: "ClaimedUpsNumber",
                table: "WaitlistEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClaimedUspNumber",
                table: "WaitlistEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmblemBlobName",
                table: "Kurins",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoleNames",
                table: "Kurins",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ScoreEnabled",
                table: "Kurins",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KurinBranches",
                columns: table => new
                {
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Branch = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: true),
                    KraiCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurinBranches", x => new { x.KurinKey, x.Branch });
                    table.ForeignKey(
                        name: "FK_KurinBranches_Kurins_KurinKey",
                        column: x => x.KurinKey,
                        principalTable: "Kurins",
                        principalColumn: "KurinKey",
                        onDelete: ReferentialAction.Cascade);
                });

            // Every kurin so far has one гілка and one number; they become its first row here, with the
            // kurin's край, and Kurins.Branch/Number stay as the mirror of that row.
            migrationBuilder.Sql("""
                INSERT INTO [KurinBranches] ([KurinKey], [Branch], [Number], [KraiCode])
                SELECT k.[KurinKey], k.[Branch], k.[Number], k.[KraiCode]
                FROM [Kurins] k
                WHERE NOT EXISTS (SELECT 1 FROM [KurinBranches] b WHERE b.[KurinKey] = k.[KurinKey]);
                """);

            migrationBuilder.CreateTable(
                name: "KurinBranchRequests",
                columns: table => new
                {
                    KurinBranchRequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UspNumber = table.Column<int>(type: "int", nullable: true),
                    UpsNumber = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurinBranchRequests", x => x.KurinBranchRequestKey);
                    table.ForeignKey(
                        name: "FK_KurinBranchRequests_Kurins_KurinKey",
                        column: x => x.KurinKey,
                        principalTable: "Kurins",
                        principalColumn: "KurinKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kurins_KraiCode_Branch_Number",
                table: "Kurins",
                columns: new[] { "KraiCode", "Branch", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KurinBranches_KraiCode_Branch_Number",
                table: "KurinBranches",
                columns: new[] { "KraiCode", "Branch", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KurinBranchRequests_KurinKey_Pending",
                table: "KurinBranchRequests",
                column: "KurinKey",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_KurinBranchRequests_KurinKey_Status",
                table: "KurinBranchRequests",
                columns: new[] { "KurinKey", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KurinBranches");

            migrationBuilder.DropTable(
                name: "KurinBranchRequests");

            migrationBuilder.DropIndex(
                name: "IX_Kurins_KraiCode_Branch_Number",
                table: "Kurins");

            migrationBuilder.DropColumn(
                name: "ClaimedUpsNumber",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "ClaimedUspNumber",
                table: "WaitlistEntries");

            migrationBuilder.DropColumn(
                name: "EmblemBlobName",
                table: "Kurins");

            migrationBuilder.DropColumn(
                name: "RoleNames",
                table: "Kurins");

            migrationBuilder.DropColumn(
                name: "ScoreEnabled",
                table: "Kurins");

            migrationBuilder.CreateIndex(
                name: "IX_Kurins_KraiCode_Number",
                table: "Kurins",
                columns: new[] { "KraiCode", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL");
        }
    }
}
