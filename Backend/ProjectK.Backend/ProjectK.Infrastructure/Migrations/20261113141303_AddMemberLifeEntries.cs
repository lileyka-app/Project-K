using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberLifeEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberLifeEntries",
                columns: table => new
                {
                    MemberLifeEntryKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Course = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Number = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Place = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Commandant = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberLifeEntries", x => x.MemberLifeEntryKey);
                    table.ForeignKey(
                        name: "FK_MemberLifeEntries_Members_MemberKey",
                        column: x => x.MemberKey,
                        principalTable: "Members",
                        principalColumn: "MemberKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberLifeEntries_MemberKey",
                table: "MemberLifeEntries",
                column: "MemberKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberLifeEntries");
        }
    }
}
