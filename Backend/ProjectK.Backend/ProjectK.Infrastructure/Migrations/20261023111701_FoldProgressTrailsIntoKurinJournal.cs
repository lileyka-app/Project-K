using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <summary>
    /// Moves the status history of проби and вмілості into the kurin's journal and drops the two
    /// tables it lived in. Each old row becomes the entry the handlers write today: the kurin and the
    /// person from the progress row, the person's name and гурток as they stand at migration time,
    /// the statuses by name, the record's id as its label (the catalogue is not in the database).
    /// An old row keeps its key, and one the journal already holds — written by both since the
    /// journal arrived — is not copied twice. <see cref="Down"/> brings the tables back empty: the
    /// history stays in the journal.
    /// </summary>
    public partial class FoldProgressTrailsIntoKurinJournal : Migration
    {
        private const string SqliteProvider = "Microsoft.EntityFrameworkCore.Sqlite";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in FoldStatements(migrationBuilder.ActiveProvider))
            {
                migrationBuilder.Sql(statement);
            }

            if (migrationBuilder.ActiveProvider != SqliteProvider)
            {
                migrationBuilder.Sql(LostRowsGuard("ProbeProgressAuditEvents", "ProbeProgressAuditEventKey", "ProbeProgressKey"));
                migrationBuilder.Sql(LostRowsGuard("BadgeProgressAuditEvents", "BadgeProgressAuditEventKey", "BadgeProgressKey"));
            }

            migrationBuilder.DropTable(
                name: "BadgeProgressAuditEvents");

            migrationBuilder.DropTable(
                name: "ProbeProgressAuditEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BadgeProgressAuditEvents",
                columns: table => new
                {
                    BadgeProgressAuditEventKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BadgeProgressKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActorUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeProgressAuditEvents", x => x.BadgeProgressAuditEventKey);
                    table.ForeignKey(
                        name: "FK_BadgeProgressAuditEvents_BadgeProgresses_BadgeProgressKey",
                        column: x => x.BadgeProgressKey,
                        principalTable: "BadgeProgresses",
                        principalColumn: "BadgeProgressKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProbeProgressAuditEvents",
                columns: table => new
                {
                    ProbeProgressAuditEventKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProbeProgressKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ActorUserKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbeProgressAuditEvents", x => x.ProbeProgressAuditEventKey);
                    table.ForeignKey(
                        name: "FK_ProbeProgressAuditEvents_ProbeProgresses_ProbeProgressKey",
                        column: x => x.ProbeProgressKey,
                        principalTable: "ProbeProgresses",
                        principalColumn: "ProbeProgressKey",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeProgressAuditEvents_BadgeProgressKey",
                table: "BadgeProgressAuditEvents",
                column: "BadgeProgressKey");

            migrationBuilder.CreateIndex(
                name: "IX_ProbeProgressAuditEvents_ProbeProgressKey",
                table: "ProbeProgressAuditEvents",
                column: "ProbeProgressKey");
        }

        /// <summary>
        /// The fold's two statements, probe trail first, in the dialect of
        /// <paramref name="activeProvider"/>: SQL Server for the real databases, SQLite for the test
        /// that runs them on seeded rows. The actions mirror the status handlers': the journal action
        /// follows from the pair of statuses, not from the old action's spelling.
        /// </summary>
        public static IReadOnlyList<string> FoldStatements(string activeProvider)
        {
            var dialect = new Dialect(activeProvider == SqliteProvider);

            var probe = new Trail(
                Table: "ProbeProgressAuditEvents",
                Key: "ProbeProgressAuditEventKey",
                Progresses: "ProbeProgresses",
                ProgressKey: "ProbeProgressKey",
                ItemId: "ProbeId",
                Area: 1,
                Statuses: ["NotStarted", "InProgress", "Completed", "Verified"],
                Action: $"""
                    CASE
                        WHEN e.[ToStatus] = 2 AND e.[FromStatus] = 3 THEN 'Probe.Unverified'
                        WHEN e.[ToStatus] = 2 THEN 'Probe.Completed'
                        WHEN e.[ToStatus] = 3 THEN 'Probe.Verified'
                        WHEN e.[ToStatus] = 1 AND e.[FromStatus] IN (2, 3) THEN 'Probe.Reopened'
                        WHEN e.[ToStatus] = 1 THEN 'Probe.Started'
                        ELSE {dialect.Left(dialect.Concat("'Probe.'", "e.[Action]"), 64)}
                    END
                    """);

            var badge = new Trail(
                Table: "BadgeProgressAuditEvents",
                Key: "BadgeProgressAuditEventKey",
                Progresses: "BadgeProgresses",
                ProgressKey: "BadgeProgressKey",
                ItemId: "BadgeId",
                Area: 2,
                Statuses: ["Draft", "Submitted", "Confirmed", "Rejected"],
                Action: $"""
                    CASE
                        WHEN e.[ToStatus] = 1 AND e.[FromStatus] IS NULL THEN 'Badge.Submitted'
                        WHEN e.[ToStatus] = 1 THEN 'Badge.Resubmitted'
                        WHEN e.[ToStatus] = 2 THEN 'Badge.Confirmed'
                        WHEN e.[ToStatus] = 3 AND e.[FromStatus] = 2 THEN 'Badge.ConfirmationRemoved'
                        WHEN e.[ToStatus] = 3 THEN 'Badge.Rejected'
                        ELSE {dialect.Left(dialect.Concat("'Badge.'", "e.[Action]"), 64)}
                    END
                    """);

            return [Fold(probe, dialect), Fold(badge, dialect)];
        }

        private static string Fold(Trail trail, Dialect dialect)
        {
            var from = StatusName(trail, "e.[FromStatus]", dialect);
            var to = StatusName(trail, "e.[ToStatus]", dialect);
            var changes = dialect.Concat(
                "'[{\"field\":\"status\",'",
                $"CASE WHEN e.[FromStatus] IS NULL THEN '' ELSE {dialect.Concat("'\"from\":\"'", from, "'\",'")} END",
                "'\"to\":\"'",
                to,
                "'\"}]'");
            var memberName = dialect.Left(
                $"LTRIM(RTRIM({dialect.Concat("COALESCE(m.[FirstName], '')", "' '", "COALESCE(m.[LastName], '')")}))",
                256);

            return $$"""
                INSERT INTO [KurinAuditEvents] (
                    [KurinAuditEventKey], [KurinKey], [OccurredAtUtc], [ActorUserKey], [ActorName], [ActorRole],
                    [Area], [Action], [SubjectMemberKey], [SubjectMemberName], [SubjectGroupKey], [SubjectKey],
                    [SubjectLabel], [Changes], [Note])
                SELECT
                    e.[{{trail.Key}}], p.[KurinKey], e.[OccurredAtUtc], e.[ActorUserKey], {{dialect.Left("e.[ActorName]", 256)}}, e.[ActorRole],
                    {{trail.Area}}, {{trail.Action}}, p.[MemberKey], NULLIF({{memberName}}, ''), ms.[GroupKey], p.[{{trail.ProgressKey}}],
                    {{dialect.Left($"p.[{trail.ItemId}]", 256)}}, {{changes}}, {{dialect.Left("e.[Note]", 2000)}}
                FROM [{{trail.Table}}] e
                INNER JOIN [{{trail.Progresses}}] p ON p.[{{trail.ProgressKey}}] = e.[{{trail.ProgressKey}}]
                LEFT JOIN [Members] m ON m.[MemberKey] = p.[MemberKey]
                LEFT JOIN [Memberships] ms ON ms.[MemberKey] = p.[MemberKey] AND ms.[KurinKey] = p.[KurinKey] AND ms.[LeftAtUtc] IS NULL
                WHERE NOT EXISTS (
                    SELECT 1 FROM [KurinAuditEvents] k WHERE k.[KurinAuditEventKey] = e.[{{trail.Key}}])
                AND NOT EXISTS (
                    SELECT 1 FROM [KurinAuditEvents] k
                    WHERE k.[SubjectKey] = p.[{{trail.ProgressKey}}]
                      AND k.[Action] = {{trail.Action}}
                      AND k.[OccurredAtUtc] >= {{dialect.AddSeconds("e.[OccurredAtUtc]", -60)}}
                      AND k.[OccurredAtUtc] <= {{dialect.AddSeconds("e.[OccurredAtUtc]", 60)}});
                """;
        }

        // Stops the migration, tables intact, when a trail row has no journal entry: neither its key
        // nor the same subject within the minute the fold treats as already written.
        private static string LostRowsGuard(string table, string key, string progressKey) => $"""
            IF EXISTS (
                SELECT 1 FROM [{table}] e
                WHERE NOT EXISTS (
                    SELECT 1 FROM [KurinAuditEvents] k
                    WHERE k.[KurinAuditEventKey] = e.[{key}]
                       OR (k.[SubjectKey] = e.[{progressKey}]
                           AND k.[OccurredAtUtc] >= DATEADD(second, -60, e.[OccurredAtUtc])
                           AND k.[OccurredAtUtc] <= DATEADD(second, 60, e.[OccurredAtUtc]))))
                THROW 51000, 'FoldProgressTrailsIntoKurinJournal: a row of {table} did not reach KurinAuditEvents; the table is kept.', 1;
            """;

        private static string StatusName(Trail trail, string column, Dialect dialect)
        {
            var cases = string.Join(" ", trail.Statuses.Select((name, value) => $"WHEN {value} THEN '{name}'"));
            return $"CASE {column} {cases} ELSE {dialect.Text(column)} END";
        }

        private sealed record Trail(
            string Table,
            string Key,
            string Progresses,
            string ProgressKey,
            string ItemId,
            int Area,
            string[] Statuses,
            string Action);

        private sealed record Dialect(bool Sqlite)
        {
            public string Concat(params string[] parts) => string.Join(Sqlite ? " || " : " + ", parts);

            public string Left(string expression, int length) =>
                Sqlite ? $"substr({expression}, 1, {length})" : $"LEFT({expression}, {length})";

            public string Text(string expression) =>
                Sqlite ? $"CAST({expression} AS TEXT)" : $"CAST({expression} AS nvarchar(11))";

            public string AddSeconds(string expression, int seconds) =>
                Sqlite
                    ? $"strftime('%Y-%m-%d %H:%M:%f', {expression}, '{seconds:+0;-0} seconds')"
                    : $"DATEADD(second, {seconds}, {expression})";
        }
    }
}
