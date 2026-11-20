using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedrawMemberPublicIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The public code used to be the first ten hex digits of MemberKey, and that key sits in
            // the address of every card the провід opens: whoever saw the person could compute their
            // code. Every code still in that derived form gets a random one. Nobody holds the old
            // codes — they have been hidden from every screen since 2026-09-14 — and a code already
            // redrawn no longer matches its key, so running this twice changes nothing more.
            migrationBuilder.Sql(@"
                UPDATE m
                SET PublicId = 'PL-'
                    + UPPER(LEFT(REPLACE(CONVERT(char(36), NEWID()), '-', ''), 5))
                    + '-'
                    + UPPER(LEFT(REPLACE(CONVERT(char(36), NEWID()), '-', ''), 5))
                FROM Members m
                WHERE m.PublicId = 'PL-'
                    + UPPER(SUBSTRING(REPLACE(CONVERT(char(36), m.MemberKey), '-', ''), 1, 5))
                    + '-'
                    + UPPER(SUBSTRING(REPLACE(CONVERT(char(36), m.MemberKey), '-', ''), 6, 5));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A drawn code cannot be taken back to the derived one without handing every leader the
            // means to compute it again; Down leaves the codes as they are.
        }
    }
}
