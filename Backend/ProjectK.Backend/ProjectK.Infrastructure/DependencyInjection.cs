using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.IdentityModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.NotificationsModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Options.NotificationsModule;
using ProjectK.Common.Options.ProbesAndBadgesModule;
using ProjectK.Infrastructure.BackgroundServices;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Logging;
using ProjectK.Infrastructure.Persistence.Interceptors;
using ProjectK.Infrastructure.Repositories.AuditModule;
using ProjectK.Infrastructure.Repositories.IdentityModule;
using ProjectK.Infrastructure.Repositories.KurinModule;
using ProjectK.Infrastructure.Repositories.NotificationsModule;
using ProjectK.Infrastructure.Seeding;
using ProjectK.Infrastructure.Seeding.Demo;
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
using ProjectK.Infrastructure.Stores;
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

        services.Configure<SecurityMonitoringOptions>(configuration.GetSection(SecurityMonitoringOptions.SectionName));
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));

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
        // The journal writes on the request context and is saved by whoever saves the request.
        services.AddScoped<IAuditUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork.UnitOfWork>());

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<ICalendarFeedStore, CalendarFeedStore>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<ICardTokenSigner, CardTokenSigner>();

        services.AddHostedService<AuditCleanupBackgroundService>();
        services.AddHostedService<MemberWarningExpiryBackgroundService>();
        services.AddHostedService<AgendaArchiveBackgroundService>();
        services.AddHostedService<DemoResetBackgroundService>();

        services.AddScoped<IKurinReportMedia, KurinReportMediaService>();
        services.AddSingleton<IKurinReportPdfRenderer, KurinReportPdfRenderer>();
        services.AddSingleton<ILifeStoryPdfRenderer, LifeStoryPdfRenderer>();
        services.AddSingleton<ISpreadsheetWriter, ClosedXmlSpreadsheetWriter>();
        services.AddSingleton<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();

        services.AddScoped<IGeoIPService, GeoIPService>();
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
            ContainerName = configuration[$"{BlobStorageOptions.SectionName}:ContainerName"] ?? "photos",
            PublicBaseUrl = configuration[$"{BlobStorageOptions.SectionName}:PublicBaseUrl"]
        };
        services.AddScoped<MemberPhotoReferenceReader>();
        services.AddScoped<GroupSilhouetteReferenceReader>();
        services.AddScoped<KurinEmblemReferenceReader>();
        services.AddScoped<ProbeProjectImageReferenceReader>();
        services.AddScoped<IPhotoReferenceReader>(sp =>
        {
            var providers = new IPhotoReferenceReader[]
            {
                sp.GetRequiredService<MemberPhotoReferenceReader>(),
                sp.GetRequiredService<GroupSilhouetteReferenceReader>(),
                sp.GetRequiredService<KurinEmblemReferenceReader>(),
                sp.GetRequiredService<ProbeProjectImageReferenceReader>()
            };

            return new CompositePhotoReferenceReader(providers);
        });

        services.AddSingleton(blobOptions);
        services.AddSingleton<IBlobReadLinks, SignedBlobReadLinks>();

        services.Configure<OrphanCleanupOptions>(configuration.GetSection(OrphanCleanupOptions.SectionName));

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
        services.Configure<FeedbackOptions>(configuration.GetSection(FeedbackOptions.SectionName));

        if (string.IsNullOrWhiteSpace(configuration[$"{FeedbackOptions.SectionName}:GitHub:Token"]))
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
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var provider = configuration[$"{EmailOptions.SectionName}:Provider"] ?? "Mock";
        if (!provider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, MockEmailService>();
            return;
        }

        services.AddOptions();
        services.AddHttpClient<IResend, ResendClient>();
        services.Configure<ResendClientOptions>(options => options.ApiToken = configuration[$"{EmailOptions.SectionName}:ApiKey"]!);
        services.AddTransient<IResend, ResendClient>();
        services.AddScoped<IEmailService, ResendEmailService>();
    }
}
