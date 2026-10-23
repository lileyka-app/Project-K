using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreNotHeldOccurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScoreNotHeldOccurrences",
                columns: table => new
                {
                    ScoreNotHeldOccurrenceKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgendaItemKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurrenceStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MarkedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MarkedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreNotHeldOccurrences", x => x.ScoreNotHeldOccurrenceKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreNotHeldOccurrences_KurinKey",
                table: "ScoreNotHeldOccurrences",
                column: "KurinKey");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreNotHeldOccurrences_OnePerOccurrence",
                table: "ScoreNotHeldOccurrences",
                columns: new[] { "AgendaItemKey", "OccurrenceStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreNotHeldOccurrences");
        }
    }
}
