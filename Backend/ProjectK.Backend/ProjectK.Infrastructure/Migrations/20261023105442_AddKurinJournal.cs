using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKurinJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KurinAuditEvents",
                columns: table => new
                {
                    KurinAuditEventKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KurinKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SubjectMemberKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectMemberName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubjectGroupKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurinAuditEvents", x => x.KurinAuditEventKey);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KurinAuditEvents_ActorUserKey",
                table: "KurinAuditEvents",
                column: "ActorUserKey");

            migrationBuilder.CreateIndex(
                name: "IX_KurinAuditEvents_KurinKey_OccurredAtUtc",
                table: "KurinAuditEvents",
                columns: new[] { "KurinKey", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_KurinAuditEvents_OccurredAtUtc",
                table: "KurinAuditEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_KurinAuditEvents_SubjectKey",
                table: "KurinAuditEvents",
                column: "SubjectKey");

            migrationBuilder.CreateIndex(
                name: "IX_KurinAuditEvents_SubjectMemberKey_OccurredAtUtc",
                table: "KurinAuditEvents",
                columns: new[] { "SubjectMemberKey", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KurinAuditEvents");
        }
    }
}
