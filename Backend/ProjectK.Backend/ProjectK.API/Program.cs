using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ProjectK.API.Authorization;
using ProjectK.API.Hosting;
using ProjectK.API.Middleware;
using ProjectK.API.Serialization;
using ProjectK.API.Swagger;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure.Logging;
using ProjectK.Infrastructure.Logging.TelegramDevAlerts;
using QuestPDF.Infrastructure;
using Serilog;
using Serilog.Enrichers.Sensitive;

namespace ProjectK.API;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureQuestPdfLicense(builder.Configuration);
        // Лічильники продуктивності — дві третини всієї телеметрії і жодного нового знання: вони
        // йдуть за таймером незалежно від навантаження, а CPU, памʼять і HTTP-метрики App Service
        // і так віддає безкоштовно. Решта модулів (запити, залежності, винятки) лишається.
        builder.Services.AddApplicationInsightsTelemetry(
            options => options.EnablePerformanceCounterCollectionModule = false);
        builder.Services.AddSingleton<ITelemetryInitializer, SecretPathTelemetryInitializer>();

        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithSensitiveDataMasking(_ => { })
                .Enrich.With<SecretPathMaskingEnricher>();

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
                        context.Configuration[ReleaseStamp.VersionKey] ?? ReleaseStamp.Unknown,
                        ReleaseStamp.CodeName(context.Configuration)));
            }
        });

        StartupConsole.TryClear();
        StartupConsole.PrintTitle(builder.Configuration, builder.Environment);

        // Refused before anything is wired: a template or short signing key is the one
        // misconfiguration that makes every session forgeable, and a self-host that boots
        // with the example key would run that way for months without a symptom.
        if (JwtKeyRules.Refusal(builder.Configuration["Jwt:Key"], builder.Environment.EnvironmentName) is { } keyRefusal)
        {
            throw new InvalidOperationException(keyRefusal);
        }

        // Before AddAuthentication: Identity, registered in here, sets the default schemes to its
        // cookies, and only the AddAuthentication below puts the bearer scheme back on top.
        builder.Services.AddProjectDependencies(builder.Configuration);

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
                        .AllowCredentials()
                        .WithExposedHeaders(AccessStaleHeaderMiddleware.HeaderName);
                }
            });
        });

        if (StartupWarmUpService.IsEnabled(builder.Configuration, builder.Environment))
        {
            builder.Services.AddHostedService<StartupWarmUpService>();
        }

        builder.Services.AddLileykaRateLimiting(builder.Configuration);

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<RequestTimings>();
        builder.Services.AddHttpClient();
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
                opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                opt.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                opt.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeConverter());
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ProjectK API",
                Version = builder.Configuration[ReleaseStamp.VersionKey] ?? "v1",
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

        builder.Services.Configure<SecurityPatchOptions>(
            builder.Configuration.GetSection("SecurityPatch"));

        // Which address a request is charged to. X-Forwarded-For is believed from the proxies
        // named in Security:ClientIp:TrustedProxies — from anyone when the list is empty, which
        // is what App Service needs and what makes the header forgeable by whoever reaches the
        // API directly. That is why Security:ClientIp:Header exists: a header only the one proxy
        // in front can write (Cloudflare's CF-Connecting-IP, nginx's X-Real-IP), applied by
        // ClientIpMiddleware and read by everything after it. The Azure side of this — admitting
        // Cloudflare alone to the App Service — is SEC-4.1 and lives outside the code.
        var clientIp = builder.Configuration.GetSection(ClientIpOptions.SectionName).Get<ClientIpOptions>() ?? new ClientIpOptions();
        builder.Services.Configure<ClientIpOptions>(builder.Configuration.GetSection(ClientIpOptions.SectionName));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var rejected in clientIp.ApplyTo(options))
            {
                Log.Warning("Security:ClientIp:TrustedProxies entry {Entry} is neither an address nor a network and was ignored.", rejected);
            }
        });

        var app = builder.Build();

        // First, so the time covers every middleware after it, not just the controller.
        app.UseMiddleware<RequestTimingMiddleware>();
        app.UseForwardedHeaders();
        app.UseMiddleware<ClientIpMiddleware>();
        // Outside the exception handler, so a request that ends in its 500 is counted as one.
        app.UseMiddleware<TelemetryCountingMiddleware>();
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

        await app.RunStartupTasksAsync();

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

        app.UseMiddleware<AccessStaleHeaderMiddleware>();

        // Geo-blocking runs before the rate limiter so blocked regions are rejected
        // without consuming limiter accounting.
        app.UseMiddleware<SecurityHardeningMiddleware>();

        app.UseLileykaRateLimiting();

        app.UseMiddleware<SecurityActivityMiddleware>();
        app.UseMiddleware<PrivilegedMfaEnforcementMiddleware>();

        app.UseAuthorization();

        app.MapControllers();

        app.MapGet("/health", (IConfiguration config, RequestTimings timings) => Results.Ok(new
        {
            status = "ready",
            version = config[ReleaseStamp.VersionKey] ?? ReleaseStamp.Unknown,
            // null, not "unknown", when the release carries no code name: the key stays in the
            // payload so nothing reading it has to change, and absence reads as absence.
            codeName = ReleaseStamp.CodeName(config),
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
}
