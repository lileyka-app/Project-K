using ProjectK.API.Hosting;
using ProjectK.BusinessLogic;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Infrastructure;

namespace ProjectK.API;

/// <summary>
/// Host-level wiring only: request context, the clock, and the two calls that let each layer
/// register what it owns.
/// </summary>
public static class ServiceCollectionExtension
{
    /// <summary>
    /// Registers the host's own services, then <c>AddInfrastructure</c> and <c>AddBusinessLogic</c>.
    /// </summary>
    /// <remarks>
    /// Call it before <c>AddAuthentication</c>: Identity, registered by the infrastructure, sets the
    /// default schemes to its cookies, and the bearer scheme has to be set after it to win.
    /// </remarks>
    public static IServiceCollection AddProjectDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

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
