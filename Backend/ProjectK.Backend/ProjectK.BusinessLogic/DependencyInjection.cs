using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Notifications;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Member.ProfileVerification;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;
using ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;

namespace ProjectK.BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccessContextResolver, AccessContextResolver>();
        services.AddScoped<ILoginResponseFactory, LoginResponseFactory>();
        services.AddSingleton<ILoginThrottle, LoginThrottle>();
        services.AddScoped<ISavedSessionList, SavedSessionList>();
        services.AddScoped<IAccountProvisioningService, AccountProvisioningService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IKraiProfileProvider, KraiProfileProvider>();
        services.AddScoped<IKurinAccessPolicyProvider, KurinAccessPolicyProvider>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<ICurrentPermissions, CurrentPermissions>();
        services.AddScoped<IMemberFieldAccess, MemberFieldAccess>();
        services.AddScoped<KraiProfileEditor>();
        services.AddScoped<IMfaEnforcementPolicy, MfaEnforcementPolicy>();
        services.AddScoped<ResourceAccessService>();
        services.AddScoped<IResourceAccessService>(sp =>
            new ResourceAccessServiceInstrumentationDecorator(
                sp.GetRequiredService<ResourceAccessService>(),
                sp.GetRequiredService<ILogger<ResourceAccessServiceInstrumentationDecorator>>()));

        services.AddSingleton<IBackendCache, MemoryBackendCache>();

        // Domain events. The in-process delivery is the only thing a broker would replace.
        services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();

        services.AddMemberModule();
        services.AddDuesModule();
        services.AddScoreModule();
        services.AddScoped<MeAgendaScopes>();
        services.AddScoped<RefreshTokenInvalidation>();
        services.AddScoped<MePerson>();

        services.AddScoped<IAgendaAccess, AgendaAccess>();
        services.AddScoped<KurinReportDataService>();

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReviewNotificationRecipientResolver, ReviewNotificationRecipientResolver>();

        services.AddScoped<IBadgesCatalogService, BadgesCatalogService>();
        services.AddScoped<IProbesCatalogService, ProbesCatalogService>();
        services.AddScoped<IProgressCatalogReader, ProgressCatalogReader>();
        services.AddScoped<IKurinCatalog, KurinCatalog>();

        services.AddScoped<IMembershipDirectory, MembershipDirectory>();
        services.AddScoped<IOfficeDirectory, OfficeDirectory>();
        services.AddScoped<IMemberProgressDirectory, MemberProgressDirectory>();

        return services;
    }

    private static void AddDuesModule(this IServiceCollection services)
    {
        services.AddScoped<IDuesAccrual, DuesAccrual>();
        services.AddScoped<DuesLedgerReader>();
        services.AddScoped<IDuesDirectory, DuesDirectory>();
        services.AddScoped<GroupDuesAccess>();
        services.AddScoped<KurinDuesAccess>();
        services.AddScoped<DuesEntryWriter>();
    }

    private static void AddScoreModule(this IServiceCollection services)
    {
        services.AddScoped<ScoreAccess>();
        services.AddScoped<ScoreSettingsAccess>();
        services.AddScoped<PrivateScoreAccess>();
        services.AddScoped<ScoreBookReader>();
        services.AddScoped<IScoreFactSource, KurinScoreFacts>();
    }

    /// <summary>
    /// The member module registers itself: deleting this call takes the module out and breaks nothing
    /// else at compile time except the contract other modules hold.
    /// </summary>
    private static void AddMemberModule(this IServiceCollection services)
    {
        services.AddScoped<IMemberDirectory, MemberDirectory>();
        services.AddScoped<IMemberProfileVerificationService, MemberProfileVerificationService>();
    }
}
