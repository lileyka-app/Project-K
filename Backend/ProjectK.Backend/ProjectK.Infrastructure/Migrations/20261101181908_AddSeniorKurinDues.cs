using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeniorKurinDues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KurinDuesRates_KurinKey_FromQuarter",
                table: "KurinDuesRates");

            migrationBuilder.AddColumn<Guid>(
                name: "TagKey",
                table: "KurinDuesRates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GroupKey",
                table: "DuesCharges",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "RateTagKey",
                table: "DuesCharges",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KurinDuesRates_KurinKey_TagKey_FromQuarter",
                table: "KurinDuesRates",
                columns: new[] { "KurinKey", "TagKey", "FromQuarter" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KurinDuesRates_KurinKey_TagKey_FromQuarter",
                table: "KurinDuesRates");

            migrationBuilder.DropColumn(
                name: "TagKey",
                table: "KurinDuesRates");

            migrationBuilder.DropColumn(
                name: "RateTagKey",
                table: "DuesCharges");

            migrationBuilder.AlterColumn<Guid>(
                name: "GroupKey",
                table: "DuesCharges",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KurinDuesRates_KurinKey_FromQuarter",
                table: "KurinDuesRates",
                columns: new[] { "KurinKey", "FromQuarter" },
                unique: true);
        }
    }
}
