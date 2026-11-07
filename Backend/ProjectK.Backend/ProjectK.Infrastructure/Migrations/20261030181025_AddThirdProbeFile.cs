using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThirdProbeFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProbeProjects",
                columns: table => new
                {
                    ProbeProjectKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProbeId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Area = table.Column<int>(type: "int", nullable: true),
                    Reach = table.Column<int>(type: "int", nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    SharedWith = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsSpare = table.Column<bool>(type: "bit", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EventLeads = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CoordinatorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CoordinatorContact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Goal = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Results = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CoordinatorComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbeProjects", x => x.ProbeProjectKey);
                });

            migrationBuilder.CreateTable(
                name: "ThirdProbeFiles",
                columns: table => new
                {
                    ThirdProbeFileKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProbeId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MentorZvyazkovyi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MentorVporiadnyk = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MentorReferent = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReflectionOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReflectionWith = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReflectionConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReflectionConfirmedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReflectionConfirmedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CampKind = table.Column<int>(type: "int", nullable: true),
                    CampName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CampFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    CampTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CampCertificateBlobName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CampCertificateContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CampCertificateFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CampCertificateBytes = table.Column<long>(type: "bigint", nullable: false),
                    PhysicalBadgeId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Resume = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    MotivationLetterOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SelfReflectionOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ApplicationSubmittedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    OpenedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    NamedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ArchivedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    StagesUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StagesUpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedByUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThirdProbeFiles", x => x.ThirdProbeFileKey);
                });

            migrationBuilder.CreateTable(
                name: "ProbeProjectImages",
                columns: table => new
                {
                    ProbeProjectImageKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProbeProjectKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlobName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Bytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbeProjectImages", x => x.ProbeProjectImageKey);
                    table.ForeignKey(
                        name: "FK_ProbeProjectImages_ProbeProjects_ProbeProjectKey",
                        column: x => x.ProbeProjectKey,
                        principalTable: "ProbeProjects",
                        principalColumn: "ProbeProjectKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProbeProjectImages_ProbeProjectKey",
                table: "ProbeProjectImages",
                column: "ProbeProjectKey");

            migrationBuilder.CreateIndex(
                name: "IX_ProbeProjects_MemberKey_ProbeId",
                table: "ProbeProjects",
                columns: new[] { "MemberKey", "ProbeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ThirdProbeFiles_MemberKey_ProbeId",
                table: "ThirdProbeFiles",
                columns: new[] { "MemberKey", "ProbeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProbeProjectImages");

            migrationBuilder.DropTable(
                name: "ThirdProbeFiles");

            migrationBuilder.DropTable(
                name: "ProbeProjects");
        }
    }
}
