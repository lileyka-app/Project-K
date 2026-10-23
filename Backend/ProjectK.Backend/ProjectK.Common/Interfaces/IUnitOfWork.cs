using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.ProbesAndBadgesModule;

namespace ProjectK.Common.Interfaces;

public interface IUnitOfWork
{
    IKraiRepository Krais { get; }
    IKurinRepository Kurins { get; }
    IGroupRepository Groups { get; }
    IMembershipRepository Memberships { get; }
    // Members, MemberAwards and MemberWarnings are deliberately absent: it lives on IMemberUnitOfWork so that reaching a person
    // from another module is a contract call (IMemberDirectory) rather than a property access.
    ILeadershipRepository Leaderships { get; }
    IPlanningSessionRepository PlanningSessions { get; }
    IAgendaItemRepository AgendaItems { get; }
    IAgendaCategoryRepository AgendaCategories { get; }
    IAgendaResponseRepository AgendaResponses { get; }
    IBadgeProgressRepository BadgeProgresses { get; }
    IProbeProgressRepository ProbeProgresses { get; }
    IProbePointProgressRepository ProbePointProgresses { get; }
    IMentorAssignmentRepository MentorAssignments { get; }
    IWaitlistRepository WaitlistEntries { get; }
    IInvitationRepository Invitations { get; }
    IAppNotificationRepository AppNotifications { get; }
    IWebPushSubscriptionRepository WebPushSubscriptions { get; }
    ISystemSettingRepository SystemSettings { get; }
    IUserTileLayoutRepository UserTileLayouts { get; }
    IDismissedDutyRepository DismissedDuties { get; }
    IAppUserRepository Users { get; }
    Task<int> SaveChangesAsync(CancellationToken token = default);
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken token = default);
}
