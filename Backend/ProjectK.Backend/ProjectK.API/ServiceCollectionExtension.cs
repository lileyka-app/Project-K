using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectK.API.Helpers;
using ProjectK.BusinessLogic;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Settings;
using ProjectK.Infrastructure;

namespace ProjectK.API;

/// <summary>
/// Host-level wiring only: request context, configuration binding, and the two calls that let each
/// layer register what it owns.
/// </summary>
public static class ServiceCollectionExtension
{
    public static IServiceCollection AddProjectDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.Configure<SecurityMonitoringOptions>(configuration.GetSection("SecurityMonitoring"));
        services.Configure<LoginThrottleOptions>(configuration.GetSection("LoginThrottle"));
        services.Configure<TelegramOptions>(configuration.GetSection("Telegram"));
        // Injected wherever the clock decides something — token and invitation expiry, warning
        // windows, the agenda's default range — so those rules can be tested at a fixed instant.
        // Plain timestamps still use DateTime.UtcNow.
        services.AddSingleton(TimeProvider.System);

        // Reading the caller's identity is a request concern, so it stays with the host.
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

        services.AddInfrastructure(configuration);
        services.AddBusinessLogic(configuration);

        return services;
    }
}
