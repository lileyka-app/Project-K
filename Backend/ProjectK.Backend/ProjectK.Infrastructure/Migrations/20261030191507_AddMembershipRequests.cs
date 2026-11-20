using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MembershipRequests",
                columns: table => new
                {
                    MembershipRequestKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    MemberKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromKurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToKurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipKind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipRequests", x => x.MembershipRequestKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_FromKurinKey",
                table: "MembershipRequests",
                column: "FromKurinKey");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_MemberKey",
                table: "MembershipRequests",
                column: "MemberKey");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_ToKurinKey",
                table: "MembershipRequests",
                column: "ToKurinKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MembershipRequests");
        }
    }
}
