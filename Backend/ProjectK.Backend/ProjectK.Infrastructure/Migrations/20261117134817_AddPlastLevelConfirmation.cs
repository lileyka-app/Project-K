using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectK.Infrastructure.Migrations
{
    /// <summary>
    /// A senior ступінь a person enters for themselves waits for the Звʼязковий. Every row already
    /// written stays confirmed: the column arrives as 0 for each of them.
    /// </summary>
    public partial class AddPlastLevelConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('PlastLevelHistories', 'AwaitsConfirmation') IS NULL
                    ALTER TABLE [PlastLevelHistories] ADD [AwaitsConfirmation] bit NOT NULL
                        CONSTRAINT [DF_PlastLevelHistories_AwaitsConfirmation] DEFAULT CAST(0 AS bit);
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('PlastLevelHistories', 'ConfirmedAtUtc') IS NULL
                    ALTER TABLE [PlastLevelHistories] ADD [ConfirmedAtUtc] datetime2 NULL;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('PlastLevelHistories', 'ConfirmedByUserKey') IS NULL
                    ALTER TABLE [PlastLevelHistories] ADD [ConfirmedByUserKey] uniqueidentifier NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID('DF_PlastLevelHistories_AwaitsConfirmation', 'D') IS NOT NULL
                    ALTER TABLE [PlastLevelHistories] DROP CONSTRAINT [DF_PlastLevelHistories_AwaitsConfirmation];
                IF COL_LENGTH('PlastLevelHistories', 'AwaitsConfirmation') IS NOT NULL
                    ALTER TABLE [PlastLevelHistories] DROP COLUMN [AwaitsConfirmation];
                IF COL_LENGTH('PlastLevelHistories', 'ConfirmedAtUtc') IS NOT NULL
                    ALTER TABLE [PlastLevelHistories] DROP COLUMN [ConfirmedAtUtc];
                IF COL_LENGTH('PlastLevelHistories', 'ConfirmedByUserKey') IS NOT NULL
                    ALTER TABLE [PlastLevelHistories] DROP COLUMN [ConfirmedByUserKey];
                """);
        }
    }
}
