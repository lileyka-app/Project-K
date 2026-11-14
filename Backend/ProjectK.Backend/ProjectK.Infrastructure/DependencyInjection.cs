using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Logging;
using ProjectK.Infrastructure.Persistence.Interceptors;
using ProjectK.Infrastructure.Repositories.AuditModule;
using ProjectK.Infrastructure.Repositories.AuthModule;
using ProjectK.Infrastructure.Repositories.InfrastructureModule;
using ProjectK.Infrastructure.Repositories.KurinModule;
using ProjectK.Infrastructure.Seeding;
using ProjectK.Infrastructure.Services.Blobs;
using ProjectK.Infrastructure.Services.Cards;
using ProjectK.Infrastructure.Services.Catalog;
using ProjectK.Infrastructure.Services.Email;
using ProjectK.Infrastructure.Services.Feedback;
using ProjectK.Infrastructure.Services.GeoIP;
using ProjectK.Infrastructure.Services.Jwt;
using ProjectK.Infrastructure.Services.Qr;
using ProjectK.Infrastructure.Services.Reports;
using ProjectK.Infrastructure.Services.Spreadsheets;
using ProjectK.Infrastructure.Services.Telemetry;
using ProjectK.Infrastructure.Services.WebPush;
using Resend;

namespace ProjectK.Infrastructure;

/// <summary>
/// Registers this project's own services, so the API does not have to know about them.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the database, Identity, blob storage and every other outside system this project talks
    /// to, with the options they read.
    /// </summary>
    /// <remarks>
    /// Call it before the host's <c>AddAuthentication</c>: <c>AddIdentity</c> sets the default schemes to
    /// its cookies, and only a later <c>AddAuthentication</c> puts the bearer scheme back on top.
    /// </remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddBlobStorage(services, configuration);

        services.Configure<SecurityMonitoringOptions>(configuration.GetSection("SecurityMonitoring"));
        services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));

        // Data access. Repositories are reached only through IUnitOfWork, which owns their lifetime
        // and shares the request DbContext, so they are deliberately not registered individually.
        services.AddScoped<UnitOfWork.UnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork.UnitOfWork>());
        // The member module's facade is the same instance, so its writes still commit with the rest
        // of the request. What it buys is that no one else can reach the member table at all.
        services.AddScoped<IMemberUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork.UnitOfWork>());
        // The same for money: only the dues module can reach its tables.
        services.AddScoped<IDuesUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork.UnitOfWork>());
        services.AddScoped<IScoreUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork.UnitOfWork>());
        services.AddScoped<IResourceScopeReader, ResourceScopeReader>();
        // The journal writes on the request context and is saved by whoever saves the request.
        services.AddScoped<IKurinAuditWriter, KurinAuditWriter>();
        services.AddScoped<IKurinJournalReader, KurinJournalReader>();

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<ICalendarFeedStore, CalendarFeedStore>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<ICardTokenSigner, CardTokenSigner>();

        services.AddHostedService<AuditCleanupBackgroundService>();
        services.AddHostedService<MemberWarningExpiryBackgroundService>();
        services.AddHostedService<AgendaArchiveBackgroundService>();
        services.AddHostedService<DemoResetBackgroundService>();

        services.AddScoped<IKurinReportSource, KurinReportSource>();
        services.AddScoped<IKurinReportMedia, KurinReportMediaService>();
        services.AddSingleton<IKurinReportPdfRenderer, KurinReportPdfRenderer>();
        services.AddSingleton<ILifeStoryPdfRenderer, LifeStoryPdfRenderer>();
        services.AddSingleton<ISpreadsheetWriter, ClosedXmlSpreadsheetWriter>();
        services.AddSingleton<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();

        services.AddScoped<GeoIPService>();
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
        services.AddSingleton<IActivityLogger, ActivityLogger>();

        AddCatalog(services, configuration);
        AddEmail(services, configuration);
        AddFeedback(services, configuration);
        AddWebPush(services, configuration);
        AddTelemetry(services, configuration);

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentity<AppUser, AppRole>(options =>
        {
            // Strict unless a local config opts out explicitly. A missing key used to read as
            // "no digit, no upper case, no symbol" — which is what a fresh self-host got, since
            // its appsettings never mentioned the key at all.
            var securePasswordOption =
                !bool.TryParse(configuration["DebugMode:SecurePasswordOptions"], out var configured)
                || configured;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = securePasswordOption;
            options.Password.RequireNonAlphanumeric = securePasswordOption;
            options.Password.RequireUppercase = securePasswordOption;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.AddDbContext<AppDbContext>(opt =>
        {
            opt.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("ProjectK.Infrastructure"));
            opt.AddAppDbContextInterceptors();
        });
    }

    /// <summary>
    /// Photos, emblems, silhouettes and project images live in one blob container. What the database
    /// still points at is gathered from every reference provider, so the orphan cleanup knows what to keep.
    /// </summary>
    private static void AddBlobStorage(IServiceCollection services, IConfiguration configuration)
    {
        var blobOptions = new BlobStorageOptions
        {
            ConnectionString = configuration.GetConnectionString("BlobStorage") ?? "UseDevelopmentStorage=true",
            ContainerName = configuration["BlobStorage:ContainerName"] ?? "photos",
            PublicBaseUrl = configuration["BlobStorage:PublicBaseUrl"]
        };
        services.AddScoped<MemberPhotoReferenceProvider>();
        services.AddScoped<GroupSilhouetteReferenceProvider>();
        services.AddScoped<KurinEmblemReferenceProvider>();
        services.AddScoped<ProbeProjectImageReferenceProvider>();
        services.AddScoped<IPhotoReferenceProvider>(sp =>
        {
            var providers = new IPhotoReferenceProvider[]
            {
                sp.GetRequiredService<MemberPhotoReferenceProvider>(),
                sp.GetRequiredService<GroupSilhouetteReferenceProvider>(),
                sp.GetRequiredService<KurinEmblemReferenceProvider>(),
                sp.GetRequiredService<ProbeProjectImageReferenceProvider>()
            };

            return new CompositePhotoReferenceProvider(providers);
        });

        services.AddSingleton(blobOptions);
        services.AddSingleton<IBlobReadLinks, SignedBlobReadLinks>();

        services.Configure<OrphanCleanupOptions>(configuration.GetSection("OrphanCleanup"));

        services.AddScoped<IPhotoService, AzureBlobPhotoService>(sp =>
        {
            var opts = sp.GetRequiredService<BlobStorageOptions>();
            return new AzureBlobPhotoService(
                opts,
                sp.GetRequiredService<IBlobReadLinks>(),
                sp.GetRequiredService<ILogger<AzureBlobPhotoService>>());
        });

        services.AddHostedService<OrphanPhotoCleanupBackgroundService>();
    }

    /// <summary>
    /// The anonymous daily ping to the status service. The request counters are always there for
    /// the API's middleware to write to; the ping itself only runs when it is enabled.
    /// </summary>
    private static void AddTelemetry(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TelemetryOptions>(configuration.GetSection(TelemetryOptions.SectionName));
        services.AddSingleton<TelemetryCounters>();

        var options = configuration.GetSection(TelemetryOptions.SectionName).Get<TelemetryOptions>() ?? new TelemetryOptions();
        if (!options.IsEnabled)
        {
            return;
        }

        services.AddScoped<TelemetryCollector>();
        services.AddHttpClient(TelemetryBackgroundService.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));
        services.AddHostedService<TelemetryBackgroundService>();
    }

    /// <summary>
    /// Problem reports go to GitHub Issues when a token is configured, to the log otherwise. The
    /// choice is made once at startup: a self-host without a token must not fail at report time.
    /// </summary>
    private static void AddFeedback(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FeedbackOptions>(configuration.GetSection("Feedback"));

        if (string.IsNullOrWhiteSpace(configuration["Feedback:GitHub:Token"]))
        {
            services.AddScoped<IProblemReporter, LogProblemReporter>();
            return;
        }

        services.AddHttpClient<IProblemReporter, GitHubProblemReporter>();
    }

    /// <summary>
    /// Web Push to the PWA when VAPID keys are configured (<c>WebPush:PublicKey</c>/<c>PrivateKey</c>);
    /// without them the queue swallows everything and the PWA hides its switch. Decided once at startup.
    /// </summary>
    private static void AddWebPush(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebPushOptions>(configuration.GetSection(WebPushOptions.SectionName));

        var configured = new WebPushOptions
        {
            PublicKey = configuration[$"{WebPushOptions.SectionName}:PublicKey"],
            PrivateKey = configuration[$"{WebPushOptions.SectionName}:PrivateKey"]
        };
        if (!configured.IsConfigured)
        {
            services.AddSingleton<IPushQueue, DisabledPushQueue>();
            return;
        }

        services.AddSingleton<ChannelPushQueue>();
        services.AddSingleton<IPushQueue>(sp => sp.GetRequiredService<ChannelPushQueue>());
        services.AddHttpClient<WebPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<WebPushDispatcher>();
        services.AddHostedService<WebPushBackgroundService>();
    }

    /// <summary>
    /// Вмілості, проби and відзначення come from the catalogue service over HTTP and live in memory,
    /// with the last good answer kept on disk for a restart without the network.
    /// </summary>
    private static void AddCatalog(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CatalogOptions>(configuration.GetSection(CatalogOptions.SectionName));
        var timeout = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue<int?>($"{CatalogOptions.SectionName}:TimeoutSeconds") ?? 30));

        services.AddSingleton<CatalogStore>();
        services.AddSingleton<IBadgesCatalog>(sp => sp.GetRequiredService<CatalogStore>());
        services.AddSingleton<IProbesCatalog>(sp => sp.GetRequiredService<CatalogStore>());
        services.AddSingleton<ICatalogKraiNames>(sp => sp.GetRequiredService<CatalogStore>());
        services.AddSingleton<IHonoursCatalog>(sp => sp.GetRequiredService<CatalogStore>());
        services.AddHttpClient(CatalogClient.HttpClientName, client => client.Timeout = timeout);
        services.AddSingleton<CatalogClient>();
        services.AddSingleton<IAwardImagesCatalog, CatalogAwardImages>();
        services.AddHostedService<CatalogSyncBackgroundService>();
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        // The business logic reads it too: BaseUrl is the frontend address its links are built on.
        services.Configure<EmailSettings>(configuration.GetSection("Email"));

        var provider = configuration["Email:Provider"] ?? "Mock";
        if (!provider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, MockEmailService>();
            return;
        }

        services.AddOptions();
        services.AddHttpClient<IResend, ResendClient>();
        services.Configure<ResendClientOptions>(options => options.ApiToken = configuration["Email:ApiKey"]!);
        services.AddTransient<IResend, ResendClient>();
        services.AddScoped<IEmailService, ResendEmailService>();
    }
}
