using ProjectK.Common.Contracts.AuthModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.ProbesAndBadgesModule;

namespace ProjectK.Common.Contracts.Shared;

public interface IUnitOfWork
{
    IKraiRepository Krais { get; }
    IKurinRepository Kurins { get; }
    IKurinBranchRequestRepository KurinBranchRequests { get; }
    IGroupRepository Groups { get; }
    IMembershipRepository Memberships { get; }
    IMembershipRequestRepository MembershipRequests { get; }
    IMemberCardShareRepository MemberCardShares { get; }
    IKurinCardSharingRepository KurinCardSharing { get; }
    // Members, MemberAwards and MemberWarnings are deliberately absent: it lives on IMemberUnitOfWork so that reaching a person
    // from another module is a contract call (IMemberDirectory) rather than a property access.
    ILeadershipRepository Leaderships { get; }
    IKurinArchiveRepository Archive { get; }
    IPlanningSessionRepository PlanningSessions { get; }
    IAgendaItemRepository AgendaItems { get; }
    IAgendaCategoryRepository AgendaCategories { get; }
    IKurinTagRepository KurinTags { get; }
    IAgendaResponseRepository AgendaResponses { get; }
    IBadgeProgressRepository BadgeProgresses { get; }
    IProbeProgressRepository ProbeProgresses { get; }
    IProbePointProgressRepository ProbePointProgresses { get; }
    IProbeProjectRepository ProbeProjects { get; }
    IMentorAssignmentRepository MentorAssignments { get; }
    IWaitlistRepository WaitlistEntries { get; }
    IInvitationRepository Invitations { get; }
    IAppNotificationRepository AppNotifications { get; }
    IWebPushSubscriptionRepository WebPushSubscriptions { get; }
    ISystemSettingRepository SystemSettings { get; }
    IUserTileLayoutRepository UserTileLayouts { get; }
    IDismissedDutyRepository DismissedDuties { get; }
    IAppUserRepository Users { get; }

    /// <summary>Where a resource stands (kurin, гурток, owner) and which гуртки an account leads: what authorization reads.</summary>
    IResourceScopeRepository ResourceScopes { get; }

    /// <summary>Everything the kurin's PDF report reads, in the few queries it needs.</summary>
    IKurinReportRepository KurinReports { get; }

    Task<int> SaveChangesAsync(CancellationToken token = default);
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken token = default);
}
