using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <summary>
    /// A third-probe project names its комендант, not a координатор, and no longer keeps the event's
    /// провід. Written by hand: the scaffold paired the columns up wrongly by type and would have
    /// moved names into the wrong place and dropped the contacts.
    /// </summary>
    public partial class ProbeProjectCommandant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventLeads",
                table: "ProbeProjects");

            migrationBuilder.RenameColumn(
                name: "CoordinatorName",
                table: "ProbeProjects",
                newName: "CommandantName");

            migrationBuilder.RenameColumn(
                name: "CoordinatorContact",
                table: "ProbeProjects",
                newName: "CommandantContact");

            migrationBuilder.RenameColumn(
                name: "CoordinatorComment",
                table: "ProbeProjects",
                newName: "CommandantComment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CommandantName",
                table: "ProbeProjects",
                newName: "CoordinatorName");

            migrationBuilder.RenameColumn(
                name: "CommandantContact",
                table: "ProbeProjects",
                newName: "CoordinatorContact");

            migrationBuilder.RenameColumn(
                name: "CommandantComment",
                table: "ProbeProjects",
                newName: "CoordinatorComment");

            migrationBuilder.AddColumn<string>(
                name: "EventLeads",
                table: "ProbeProjects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
