using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Seeding;
using Spectre.Console;

namespace ProjectK.API.Hosting;

/// <summary>What runs once before the host starts serving: migrations, seeding, the photo container.</summary>
public static class StartupTaskExtensions
{
    /// <summary>
    /// Migrates the database (unless <c>Database:RunMigrationsOnStartup</c> is off), seeds it and
    /// prepares blob storage, all in one scope.
    /// </summary>
    public static async Task RunStartupTasksAsync(this WebApplication app)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Booting the kettle...", async ctx =>
            {
                using var scope = app.Services.CreateScope();

                if (ShouldRunMigrationsOnStartup(app.Configuration))
                {
                    ctx.Status("Summoning database goblins...");
                    await DatabaseMigrationRules.ApplyAsync(scope.ServiceProvider);
                }
                else
                {
                    ctx.Status("Skipping migrations — apply them via the deploy step...");
                    app.Logger.LogInformation(
                        "Startup migrations skipped for environment {Environment}; the schema must be applied out-of-band (migration bundle) so instances do not race.",
                        app.Environment.EnvironmentName);
                }

                ctx.Status("Planting heroic seed data...");
                await DataSeeder.SeedAsync(scope.ServiceProvider);

                ctx.Status("Locking the photo container...");
                try
                {
                    await scope.ServiceProvider.GetRequiredService<IPhotoService>().PrepareStorageAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Storage being down must not keep the API from starting; the first upload retries.
                    app.Logger.LogWarning(ex, "Could not prepare blob storage at startup.");
                }

                ctx.Status("Startup complete.");
            });

        AnsiConsole.MarkupLine("[green]✔ Startup successful![/]");

        var pause = StartupConsoleRules.BannerPauseMs(app.Configuration, app.Environment);
        if (pause > 0)
        {
            await Task.Delay(pause);
        }
    }

    // Defaults to true, so every environment auto-migrates on startup exactly as before.
    // Set "Database:RunMigrationsOnStartup": false in an environment's config once you want
    // that environment to apply the schema out-of-band instead (via the migration bundle,
    // scripts/build-migration-bundle.*), so several instances can boot without racing.
    private static bool ShouldRunMigrationsOnStartup(IConfiguration configuration)
        => configuration.GetValue("Database:RunMigrationsOnStartup", true);
}
