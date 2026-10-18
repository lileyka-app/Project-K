using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using AutoMapper.EquivalencyExpression;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ProjectK.API.Authorization;
using ProjectK.API.Helpers;
using ProjectK.API.Hosting;
using ProjectK.API.Middleware;
using ProjectK.API.Serialization;
using ProjectK.API.Swagger;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.BusinessLogic.MappingProfiles;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Kurin.Get;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Security;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models;
using ProjectK.Common.Models.Authorization;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Reports;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Logging.TelegramDevAlerts;
using ProjectK.Infrastructure.Reports;
using ProjectK.Infrastructure.Repositories;
using ProjectK.Infrastructure.Seeding;
using ProjectK.Infrastructure.Services.BlobStorageService;
using ProjectK.Infrastructure.Services.BlobStorageService.OrphanCleanup;
using ProjectK.Infrastructure.Services.GeoIP;
using ProjectK.Optimization.Extensions;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Enrichers.Sensitive;
using Serilog.Filters.Expressions;
using Spectre.Console;

namespace ProjectK.API;

public static class Program
{
    private const string UnknownValue = "unknown";
    private const int DefaultDevBannerPauseMs = 2000;

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureQuestPdfLicense(builder.Configuration);
        // Лічильники продуктивності — дві третини всієї телеметрії і жодного нового знання: вони
        // йдуть за таймером незалежно від навантаження, а CPU, памʼять і HTTP-метрики App Service
        // і так віддає безкоштовно. Решта модулів (запити, залежності, винятки) лишається.
        builder.Services.AddApplicationInsightsTelemetry(
            options => options.EnablePerformanceCounterCollectionModule = false);

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithSensitiveDataMasking(_ => { });

            var devAlerts = context.Configuration
                .GetSection("Telegram:DevAlerts")
                .Get<TelegramDevAlertOptions>() ?? new TelegramDevAlertOptions();

            if (devAlerts.Enabled)
            {
                configuration.WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly("EventType = 'Security.Suspicious' or @Level >= 'Error'")
                    .WriteTo.TelegramDevAlerts(
                        devAlerts,
                        context.HostingEnvironment.EnvironmentName,
                        context.Configuration["ReleaseInfo:Version"] ?? UnknownValue,
                        ResolveCodeName(context.Configuration)));
            }
        });

        TryClearConsole();
        PrintTitle(builder.Configuration, builder.Environment);

        // Refused before anything is wired: a template or short signing key is the one
        // misconfiguration that makes every session forgeable, and a self-host that boots
        // with the example key would run that way for months without a symptom.
        if (JwtKeyRules.Refusal(builder.Configuration["Jwt:Key"], builder.Environment.EnvironmentName) is { } keyRefusal)
        {
            throw new InvalidOperationException(keyRefusal);
        }

        builder.Services.AddIdentity<AppUser, AppRole>(options =>
        {
            // Strict unless a local config opts out explicitly. A missing key used to read as
            // "no digit, no upper case, no symbol" — which is what a fresh self-host got, since
            // its appsettings never mentioned the key at all.
            var securePasswordOption =
                !bool.TryParse(builder.Configuration["DebugMode:SecurePasswordOptions"], out var configured)
                || configured;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = securePasswordOption;
            options.Password.RequireNonAlphanumeric = securePasswordOption;
            options.Password.RequireUppercase = securePasswordOption;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),

                RoleClaimType = ClaimTypes.Role,
                NameClaimType = JwtRegisteredClaimNames.Sub
            };
        });

        builder.Services.AddHttpContextAccessor();

        // The body UseExceptionHandler writes for an unhandled exception.
        builder.Services.AddProblemDetails();

        builder.Services.AddAuthorization(options => options.AddProjectPolicies());

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("EnvCorsPolicy", policy =>
            {
                // Comma-separated: a domain move keeps the old frontend origin working until it redirects.
                var origins = (builder.Configuration["EnvCorsOrigin"] ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            });
        });

        builder.Services.AddDbContext<AppDbContext>(opt =>
        {
            opt.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly("ProjectK.Infrastructure")
            );
        });

        // --- Blob storage DI ---
        var blobOptions = new BlobStorageOptions
        {
            ConnectionString = builder.Configuration.GetConnectionString("BlobStorage") ?? "UseDevelopmentStorage=true",
            ContainerName = builder.Configuration["BlobStorage:ContainerName"] ?? "photos",
            PublicBaseUrl = builder.Configuration["BlobStorage:PublicBaseUrl"]
        };
        builder.Services.AddScoped<MemberPhotoReferenceProvider>();
        builder.Services.AddScoped<GroupSilhouetteReferenceProvider>();
        builder.Services.AddScoped<IPhotoReferenceProvider>(sp =>
        {
            var providers = new IPhotoReferenceProvider[]
            {
                sp.GetRequiredService<MemberPhotoReferenceProvider>(),
                sp.GetRequiredService<GroupSilhouetteReferenceProvider>()
            };

            return new CompositePhotoReferenceProvider(providers);
        });

        builder.Services.AddSingleton(blobOptions);
        builder.Services.AddSingleton<IBlobReadLinks, SignedBlobReadLinks>();

        builder.Services.Configure<OrphanCleanupOptions>(builder.Configuration.GetSection("OrphanCleanup"));
        builder.Services.Configure<KurinNumberOptions>(builder.Configuration.GetSection(KurinNumberOptions.SectionName));
        builder.Services.Configure<KraiOptions>(builder.Configuration.GetSection(KraiOptions.SectionName));

        builder.Services.AddScoped<IPhotoService, AzureBlobPhotoService>(sp =>
        {
            var opts = sp.GetRequiredService<BlobStorageOptions>();
            var refProvider = sp.GetService<IPhotoReferenceProvider>();
            return new AzureBlobPhotoService(
                opts,
                refProvider,
                sp.GetRequiredService<IBlobReadLinks>(),
                sp.GetRequiredService<ILogger<AzureBlobPhotoService>>());
        });

        builder.Services.AddHostedService<OrphanPhotoCleanupService>();
        // --- end Blob storage DI ---

        if (StartupWarmUpService.IsEnabled(builder.Configuration, builder.Environment))
        {
            builder.Services.AddHostedService<StartupWarmUpService>();
        }

        ConfigureRateLimiting(builder.Services, builder.Configuration);

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<RequestTimings>();
        builder.Services.AddHttpClient();
        builder.Services.AddAutoMapper(cfg => { cfg.AddCollectionMappers(); }, typeof(KurinModuleProfile));
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(GetKurinByKeyQuery).Assembly);
            // Outer -> inner. Timing wraps everything; Validation fails fast; Caching
            // returns hits before a transaction is opened; Transaction sits around the handler.
            cfg.AddOpenBehavior(typeof(RequestTimingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });
        builder.Services.AddValidatorsFromAssembly(typeof(GetKurinByKeyQuery).Assembly);
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                // The e2e fixture controller is compiled into the same image as production. Outside
                // the E2E environment it is taken out of the application model, so its routes do
                // not exist there rather than merely refuse.
                if (!builder.Environment.IsEnvironment("E2E"))
                {
                    manager.FeatureProviders.Add(new E2EOnlyControllerFeatureProvider());
                }

                // The dev role switcher: same idea, three local tiers.
                if (!DevOnlyControllerFeatureProvider.Allows(builder.Environment.EnvironmentName))
                {
                    manager.FeatureProviders.Add(new DevOnlyControllerFeatureProvider());
                }

                // The public demo's password-less entry: only where the data is a fixture.
                if (!DemoOnlyControllerFeatureProvider.Allows(builder.Environment.EnvironmentName))
                {
                    manager.FeatureProviders.Add(new DemoOnlyControllerFeatureProvider());
                }
            })
            .AddJsonOptions(opt =>
            {
                opt.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                opt.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                opt.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ProjectK API",
                Version = builder.Configuration["ReleaseInfo:Version"] ?? "v1",
                Description = "Management API for a Plast kurin: membership, leadership offices, "
                    + "agenda and planning, probes and badges, announcements and onboarding. "
                    + "Every failure answers with { error, message }; access is decided by the "
                    + "office a member holds, not by an account-level role."
            });

            var xmlDocumentation = Path.Combine(AppContext.BaseDirectory, "ProjectK.API.xml");
            if (File.Exists(xmlDocumentation))
            {
                options.IncludeXmlComments(xmlDocumentation, includeControllerXmlComments: true);
            }

            options.OperationFilter<UnifiedErrorResponsesFilter>();

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        builder.Services.AddWolfPackOptimization();

        builder.Services.Configure<SecurityPatchOptions>(
            builder.Configuration.GetSection("SecurityPatch"));

        builder.Services.AddProjectDependencies(builder.Configuration);

        // Which address a request is charged to. X-Forwarded-For is believed from the proxies
        // named in Security:ClientIp:TrustedProxies — from anyone when the list is empty, which
        // is what App Service needs and what makes the header forgeable by whoever reaches the
        // API directly. That is why Security:ClientIp:Header exists: a header only the one proxy
        // in front can write (Cloudflare's CF-Connecting-IP, nginx's X-Real-IP), applied by
        // ClientIpMiddleware and read by everything after it. The Azure side of this — admitting
        // Cloudflare alone to the App Service — is SEC-4.1 and lives outside the code.
        var clientIp = builder.Configuration.GetSection(ClientIpOptions.SectionName).Get<ClientIpOptions>() ?? new ClientIpOptions();
        builder.Services.Configure<ClientIpOptions>(builder.Configuration.GetSection(ClientIpOptions.SectionName));
        builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var rejected in clientIp.ApplyTo(options))
            {
                Log.Warning("Security:ClientIp:TrustedProxies entry {Entry} is neither an address nor a network and was ignored.", rejected);
            }
        });

        // The clock a kurin's week repeats by (DST-safe recurrence); Kyiv unless configured.
        AgendaRecurrence.DefaultZone = AgendaRecurrence.ResolveZone(builder.Configuration["Agenda:TimeZone"] ?? "Europe/Kyiv");

        var app = builder.Build();

        // First, so the time covers every middleware after it, not just the controller.
        app.UseMiddleware<RequestTimingMiddleware>();
        app.UseForwardedHeaders();
        app.UseMiddleware<ProjectK.API.Middleware.ClientIpMiddleware>();
        // Outside the exception handler, so a request that ends in its 500 is counted as one.
        app.UseMiddleware<ProjectK.API.Middleware.TelemetryCountingMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString());
            };
        });

        // Without a handler an unhandled exception reaches Kestrel, which answers 500 with the CORS
        // headers stripped: the browser reports a CORS error and the app cannot read the failure.
        // The handler keeps them, because CORS writes its headers when the response starts.
        // Development is left to the developer exception page the host adds there.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
        }

        ValidateTelegramConfiguration(app);

        await RunStartupTasksAsync(app);

        // Staging too: the spec is the only description of the contract that stays in step with the
        // code, and staging is where the frontend is pointed while a release is being checked.
        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProjectK API"));
        }

        app.UseRouting();

        app.UseCors("EnvCorsPolicy");

        app.UseAuthentication();

        // Geo-blocking runs before the rate limiter so blocked regions are rejected
        // without consuming limiter accounting.
        app.UseMiddleware<ProjectK.API.Middleware.SecurityHardeningMiddleware>();

        app.UseRateLimiter();

        app.UseMiddleware<ProjectK.API.Middleware.SecurityActivityMiddleware>();
        app.UseMiddleware<ProjectK.API.Middleware.PrivilegedMfaEnforcementMiddleware>();

        app.UseAuthorization();

        app.MapControllers();

        app.MapGet("/health", (IConfiguration config, RequestTimings timings) => Results.Ok(new
        {
            status = "ready",
            version = config["ReleaseInfo:Version"] ?? UnknownValue,
            // null, not "unknown", when the release carries no code name: the key stays in the
            // payload so nothing reading it has to change, and absence reads as absence.
            codeName = ResolveCodeName(config),
            utc = DateTimeOffset.UtcNow,
            // What the status page draws: the API's own time on real requests, not the network's.
            api = timings.Snapshot()
        }))
        .WithSummary("Reports that the API is up, and which release is running.")
        .WithDescription("What the container health check and the deployment pipeline read. Answers without touching the database, so it says the process is serving — not that everything behind it is well. `api` is the median and 95th percentile of the server's own time on real requests over the last 15 minutes, null without any.");

        app.MapGet("/", () => "Backend Started")
            .WithSummary("A plain sign of life at the root, for anyone who opens the API in a browser.");

        await app.RunAsync();
    }

    /// <summary>The code name this build was stamped with, or <c>null</c> when it carries none.</summary>
    private static string? ResolveCodeName(IConfiguration config) =>
        ReleaseDisplay.CodeName(config["ReleaseInfo:Codename"] ?? config["ReleaseInfo:CodeName"]);

    private static void PrintTitle(IConfiguration config, IHostEnvironment env)
    {
        var version = config["ReleaseInfo:Version"] ?? "v0.0.0";

        AnsiConsole.Write(new FigletText("Project K").Color(Spectre.Console.Color.Green));

        AnsiConsole.Write(new Rule($"[yellow]{ReleaseDisplay.Label(version, ResolveCodeName(config))}[/]")
        {
            Justification = Justify.Left
        });

        AnsiConsole.WriteLine();

        var pause = GetBannerPauseMs(config, env);
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
    private static int GetBannerPauseMs(IConfiguration config, IHostEnvironment env)
    {
        if (int.TryParse(config["Startup:ConsoleBannerPauseMs"], out var configured) && configured >= 0)
        {
            return configured;
        }

        return env.IsDevelopment() ? DefaultDevBannerPauseMs : 0;
    }

    private static void ConfigureQuestPdfLicense(IConfiguration configuration)
    {
        QuestPDF.Settings.License = Enum.TryParse<LicenseType>(
            configuration["QuestPdf:License"],
            ignoreCase: true,
            out var license)
                ? license
                : LicenseType.Community;
    }

    private static void ValidateTelegramConfiguration(WebApplication app)
    {
        var devAlerts = app.Configuration
            .GetSection("Telegram:DevAlerts")
            .Get<TelegramDevAlertOptions>() ?? new TelegramDevAlertOptions();

        if (devAlerts.Enabled && (string.IsNullOrWhiteSpace(devAlerts.BotToken) || string.IsNullOrWhiteSpace(devAlerts.ChatId)))
        {
            app.Logger.LogWarning(
                "Telegram dev alerts are enabled but BotToken or ChatId is missing. Alerts will not be delivered.");
        }
    }

    private static void TryClearConsole()
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

    private static async Task RunStartupTasksAsync(WebApplication app)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync("Booting the kettle...", async ctx =>
            {
                using var scope = app.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (ShouldRunMigrationsOnStartup(app.Configuration))
                {
                    ctx.Status("Summoning database goblins...");
                    await dbContext.Database.MigrateAsync();
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

        var pause = GetBannerPauseMs(app.Configuration, app.Environment);
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

    private static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        // The strict limits are per address, and on a container stand every browser arrives from
        // the same docker gateway address: five failed sign-ins by anyone on the tailnet locked
        // everyone out for five minutes. Production sees real addresses (Security:ClientIp), so the
        // defaults stay tight there; the local tiers raise them in their appsettings.
        var strictPermit = configuration.GetValue("RateLimiting:StrictAuth:PermitLimit", 5);
        var strictWindow = TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:StrictAuth:WindowMinutes", 5));
        var accountPermit = configuration.GetValue("RateLimiting:AccountSecurity:PermitLimit", 10);
        var accountWindow = TimeSpan.FromMinutes(configuration.GetValue("RateLimiting:AccountSecurity:WindowMinutes", 5));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var bypassPartition = TryGetBypassPartition(httpContext, configuration);
                if (bypassPartition is not null)
                {
                    return bypassPartition.Value;
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.GetUserKeyValue()
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? httpContext.Request.Headers.Host.ToString(),
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 300,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

            options.AddPolicy<string>("StrictAuthLimit", httpContext =>
            {
                var bypassPartition = TryGetBypassPartition(httpContext, configuration);
                if (bypassPartition is not null)
                {
                    return bypassPartition.Value;
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = strictPermit,
                        QueueLimit = 0,
                        Window = strictWindow
                    });
            });

            options.AddPolicy<string>("AccountSecurityLimit", httpContext =>
            {
                var bypassPartition = TryGetBypassPartition(httpContext, configuration);
                if (bypassPartition is not null)
                {
                    return bypassPartition.Value;
                }

                var partitionKey = httpContext.User.GetUserKeyValue()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: partition => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = accountPermit,
                        QueueLimit = 0,
                        Window = accountWindow
                    });
            });

            options.OnRejected = async (context, token) =>
            {
                var endpoint = context.HttpContext.GetEndpoint();
                // No endpoint policy means the global limiter turned it away — said so, rather than left empty.
                var policyName = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "global";
                var activityLogger = context.HttpContext.RequestServices.GetService<IActivityLogger>();
                activityLogger?.ReportRateLimitRejection(policyName);
                await context.HttpContext.Response.WriteAsync("Too many requests. Please try again later.", token);
            };
        });
    }

    private static RateLimitPartition<string>? TryGetBypassPartition(HttpContext httpContext, IConfiguration configuration)
    {
        var bypassKey = configuration["RateLimitBypassKey"];
        if (!string.IsNullOrEmpty(bypassKey) &&
            httpContext.Request.Headers.TryGetValue("X-RateLimit-Bypass", out var providedKey) &&
            SecretComparer.Matches(providedKey.ToString(), bypassKey))
        {
            return RateLimitPartition.GetNoLimiter("Bypass");
        }

        return null;
    }
}
