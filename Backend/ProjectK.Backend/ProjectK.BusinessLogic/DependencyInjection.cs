using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectK.BusinessLogic.Behaviors;
using ProjectK.BusinessLogic.Modules.ArchiveModule.Services;
using ProjectK.BusinessLogic.Modules.AgendaModule.Services;
using ProjectK.BusinessLogic.Modules.AuditModule.Services;
using ProjectK.BusinessLogic.Modules.AuthModule.Services;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.BusinessLogic.Modules.InfrastructureModule.Services;
using ProjectK.BusinessLogic.Modules.KraiModule.Services;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MeModule.Services;
using ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.ThirdProbe;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Services;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.BusinessLogic.Services.Events;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Options.AuthModule;
using ProjectK.Common.Options.KraiModule;
using ProjectK.Common.Options.KurinModule;
using ProjectK.Common.Rules.AgendaModule;
using ProjectK.Optimization.Extensions;

namespace ProjectK.BusinessLogic;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the request pipeline (MediatR with its behaviors, validators, mapping), the domain
    /// services and the options this project reads.
    /// </summary>
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Outer -> inner. Timing wraps everything; Validation fails fast; Transaction sits
            // around the handler.
            cfg.AddOpenBehavior(typeof(RequestTimingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        services.AddWolfPackOptimization();

        services.Configure<KurinNumberOptions>(configuration.GetSection(KurinNumberOptions.SectionName));
        services.Configure<KraiOptions>(configuration.GetSection(KraiOptions.SectionName));
        services.Configure<LoginThrottleOptions>(configuration.GetSection("LoginThrottle"));

        // The clock a kurin's week repeats by (DST-safe recurrence); Kyiv unless configured.
        AgendaRecurrenceRules.DefaultZone = AgendaRecurrenceRules.ResolveZone(configuration["Agenda:TimeZone"] ?? "Europe/Kyiv");

        services.AddScoped<IAccessContextResolver, AccessContextResolver>();
        services.AddScoped<ILoginResponseService, LoginResponseService>();
        services.AddSingleton<ILoginThrottle, LoginThrottle>();
        services.AddScoped<IReauthentication, Reauthentication>();
        services.AddScoped<ISavedSessionList, SavedSessionList>();
        services.AddScoped<IAccountProvisioningService, AccountProvisioningService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IKraiProfileDirectory, KraiProfileDirectory>();
        services.AddScoped<IKurinAccessPolicyReader, KurinAccessPolicyReader>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IAccessSnapshotFactory, AccessSnapshotFactory>();
        services.AddSingleton<IAccessChangeStore, AccessChangeStore>();
        services.AddScoped<ICurrentPermissions, CurrentPermissions>();
        services.AddScoped<IMemberFieldAccess, MemberFieldAccess>();
        services.AddScoped<IKurinTagVisibility, KurinTagVisibility>();
        services.AddScoped<IKurinTagDirectory, KurinTagDirectory>();
        services.AddScoped<IKurinDirectory, KurinDirectory>();
        services.AddScoped<IKurinGroupWriter, KurinGroupWriter>();
        services.AddScoped<KraiProfileWriter>();
        services.AddScoped<IMfaEnforcementReader, MfaEnforcementReader>();
        services.AddScoped<ResourceAccessService>();
        services.AddScoped<IResourceAccessService>(sp =>
            new ResourceAccessServiceInstrumentationDecorator(
                sp.GetRequiredService<ResourceAccessService>(),
                sp.GetRequiredService<ILogger<ResourceAccessServiceInstrumentationDecorator>>()));

        services.AddSingleton<IBackendCache, MemoryBackendCache>();

        // Domain events. The in-process delivery is the only thing a broker would replace.
        services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();

        services.AddScoped<IKurinAudit, KurinAudit>();

        services.AddMemberModule();
        services.AddAgendaModule();
        services.AddDuesModule();
        services.AddScoreModule();
        services.AddArchiveModule();
        services.AddScoped<MeAgendaScopes>();
        services.AddScoped<RefreshTokenInvalidation>();
        services.AddScoped<MemberCards>();

        services.AddScoped<KurinReportHeaderReader>();
        services.AddScoped<KurinReportTagReader>();
        services.AddScoped<KurinReportDataService>();

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReviewNotificationRecipientReader, ReviewNotificationRecipientReader>();

        services.AddScoped<IBadgesCatalogReader, BadgesCatalogReader>();
        services.AddScoped<IProbesCatalogReader, ProbesCatalogReader>();
        services.AddScoped<IProgressCatalogReader, ProgressCatalogReader>();
        services.AddScoped<IKurinCatalog, KurinCatalog>();

        services.AddScoped<IMemberProgressDirectory, MemberProgressDirectory>();
        services.AddScoped<MemberProbeLine>();
        services.AddScoped<IMemberProbeLine>(sp => sp.GetRequiredService<MemberProbeLine>());
        services.AddScoped<IProbesAndBadgesAccess, ProbesAndBadgesAccess>();
        services.AddScoped<ThirdProbeFileWriter>();
        services.AddScoped<IThirdProbeReader, ThirdProbeReader>();
        services.AddScoped<IProbeEditionHolds>(sp => sp.GetRequiredService<MemberProbeLine>());
        services.AddScoped<IGroupProbeEditions, GroupProbeEditions>();

        return services;
    }

    private static void AddAgendaModule(this IServiceCollection services)
    {
        services.AddScoped<IAgendaAccess, AgendaAccess>();
        services.AddScoped<AgendaStatusWriter>();
        services.AddScoped<IAgendaRsvpWriter, AgendaRsvpWriter>();
        services.AddScoped<IAgendaDirectory, AgendaDirectory>();
    }

    private static void AddDuesModule(this IServiceCollection services)
    {
        services.AddScoped<IDuesAccrual, DuesAccrual>();
        services.AddScoped<IDuesKurinShape, DuesKurinShape>();
        services.AddScoped<DuesAccountRows>();
        services.AddScoped<DuesLedgerReader>();
        services.AddScoped<IDuesDirectory, DuesDirectory>();
        services.AddScoped<DuesAccess>();
        services.AddScoped<DuesEntryWriter>();
    }

    private static void AddScoreModule(this IServiceCollection services)
    {
        services.AddScoped<ScoreAccess>();
        services.AddScoped<ScoreBookReader>();
        services.AddScoped<IScoreFactReader, ScoreFactReader>();
    }

    private static void AddArchiveModule(this IServiceCollection services)
    {
        services.AddScoped<ArchiveReach>();
        services.AddSingleton<ArchiveDangerZoneStore>();
        services.AddScoped<ArchiveDangerZoneService>();
        services.AddScoped<ArchiveDeletionService>();
    }

    /// <summary>
    /// The member module registers itself: deleting this call takes the module out and breaks nothing
    /// else at compile time except the contract other modules hold.
    /// </summary>
    private static void AddMemberModule(this IServiceCollection services)
    {
        services.AddScoped<IMemberDirectory, MemberDirectory>();
        services.AddScoped<IMembershipDirectory, MembershipDirectory>();
        services.AddScoped<IOfficeDirectory, OfficeDirectory>();
        services.AddScoped<IMemberImportWriter, MemberImportWriter>();
        services.AddScoped<ICurrentMember, CurrentMember>();
        services.AddScoped<MembershipMoves>();
        services.AddScoped<MembershipAskRouting>();
        services.AddScoped<IMemberProfileVerificationService, MemberProfileVerificationService>();
    }
}
