using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <summary>
    /// The third probe closes as the край closes it: recommended by the старшина (the former «done»),
    /// submitted for closing, named; frozen aside from that path. A probe already named gets its
    /// submission day from the recommendation, or saving its stages again would be refused as out of
    /// order.
    /// </summary>
    public partial class ThirdProbeClosingStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CompletedOn",
                table: "ThirdProbeFiles",
                newName: "RecommendedOn");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ClosingSubmittedOn",
                table: "ThirdProbeFiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FrozenOn",
                table: "ThirdProbeFiles",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE ThirdProbeFiles SET ClosingSubmittedOn = RecommendedOn WHERE NamedOn IS NOT NULL AND ClosingSubmittedOn IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosingSubmittedOn",
                table: "ThirdProbeFiles");

            migrationBuilder.DropColumn(
                name: "FrozenOn",
                table: "ThirdProbeFiles");

            migrationBuilder.RenameColumn(
                name: "RecommendedOn",
                table: "ThirdProbeFiles",
                newName: "CompletedOn");
        }
    }
}
