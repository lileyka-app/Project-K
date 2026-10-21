using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDuesConcessionGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DuesConcessionGrants",
                columns: table => new
                {
                    DuesConcessionGrantKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConcessionKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Parts = table.Column<int>(type: "int", nullable: false),
                    FromQuarter = table.Column<int>(type: "int", nullable: false),
                    ToQuarter = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SetByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuesConcessionGrants", x => x.DuesConcessionGrantKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuesConcessionGrants_KurinKey",
                table: "DuesConcessionGrants",
                column: "KurinKey");

            migrationBuilder.CreateIndex(
                name: "IX_DuesConcessionGrants_MembershipKey",
                table: "DuesConcessionGrants",
                column: "MembershipKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DuesConcessionGrants");
        }
    }
}
