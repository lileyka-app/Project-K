using ProjectK.Common.Models;
using Spectre.Console;

namespace ProjectK.API.Hosting;

/// <summary>The boot banner a developer sees in the local console.</summary>
internal static class StartupConsole
{
    private const int DefaultDevBannerPauseMs = 2000;

    public static void TryClear()
    {
        try
        {
            AnsiConsole.Clear();
        }
        catch (IOException)
        {
            // Some CI/service hosts expose stdout without an interactive console buffer.
        }
    }

    public static void PrintTitle(IConfiguration config, IHostEnvironment env)
    {
        var version = config[ReleaseStamp.VersionKey] ?? "v0.0.0";

        AnsiConsole.Write(new FigletText("Project K").Color(Color.Green));

        AnsiConsole.Write(new Rule($"[yellow]{ReleaseDisplay.Label(version, ReleaseStamp.CodeName(config))}[/]")
        {
            Justification = Justify.Left
        });

        AnsiConsole.WriteLine();

        var pause = BannerPauseMs(config, env);
        if (pause > 0)
        {
            Thread.Sleep(pause);
        }
    }

    // The startup banner dwell is a local-console nicety: it keeps the boot
    // banner readable on a warm DB, where migrations/seeding finish almost
    // instantly and nothing else holds the screen. Deployed environments pay
    // it straight into cold start with nobody watching, so it defaults off
    // outside Development. Startup:ConsoleBannerPauseMs overrides either way.
    public static int BannerPauseMs(IConfiguration config, IHostEnvironment env)
    {
        if (int.TryParse(config["Startup:ConsoleBannerPauseMs"], out var configured) && configured >= 0)
        {
            return configured;
        }

        return env.IsDevelopment() ? DefaultDevBannerPauseMs : 0;
    }
}
