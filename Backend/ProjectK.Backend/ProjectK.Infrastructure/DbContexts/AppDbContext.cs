using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.AuditModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Entities.InfrastructureModule;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.Infrastructure.DbContexts;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public DbSet<Krai> Krais { get; set; }
    public DbSet<KraiProfileRevision> KraiProfileRevisions { get; set; }
    public DbSet<Kurin> Kurins { get; set; }
    public DbSet<KurinBranchRequest> KurinBranchRequests { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<Member> Members { get; set; }
    public DbSet<Membership> Memberships { get; set; }
    public DbSet<MembershipGroupStint> MembershipGroupStints { get; set; }
    public DbSet<MembershipRequest> MembershipRequests { get; set; }
    public DbSet<MemberCardShare> MemberCardShares { get; set; }
    public DbSet<MemberLifeEntry> MemberLifeEntries { get; set; }
    public DbSet<KurinCardSharing> KurinCardSharing { get; set; }
    public DbSet<PlastLevelHistory> PlastLevelHistories { get; set; }
    public DbSet<Leadership> Leaderships { get; set; }
    public DbSet<LeadershipHistory> LeadershipHistories { get; set; }
    public DbSet<PlanningSession> PlanningSessions { get; set; }
    public DbSet<PlanningParticipant> PlanningParticipants { get; set; }
    public DbSet<ParticipantBusyRange> ParticipantBusyRanges { get; set; }
    public DbSet<AgendaItem> AgendaItems { get; set; }
    public DbSet<AgendaAssignment> AgendaAssignments { get; set; }
    public DbSet<AgendaAssignmentProgress> AgendaAssignmentProgress { get; set; }
    public DbSet<AgendaCategory> AgendaCategories { get; set; }
    public DbSet<AgendaResponse> AgendaResponses { get; set; }
    public DbSet<KurinTag> KurinTags { get; set; }
    public DbSet<MemberTag> MemberTags { get; set; }
    public DbSet<KurinTagSettings> KurinTagSettings { get; set; }
    public DbSet<BadgeProgress> BadgeProgresses { get; set; }
    public DbSet<ProbeProgress> ProbeProgresses { get; set; }
    public DbSet<ProbePointProgress> ProbePointProgresses { get; set; }
    public DbSet<ProbeProject> ProbeProjects { get; set; }
    public DbSet<ProbeProjectImage> ProbeProjectImages { get; set; }
    public DbSet<ThirdProbeFile> ThirdProbeFiles { get; set; }
    public DbSet<MentorAssignment> MentorAssignments { get; set; }
    public DbSet<MemberWarning> MemberWarnings { get; set; }
    public DbSet<MemberAward> MemberAwards { get; set; }

    public DbSet<KurinDuesRate> KurinDuesRates { get; set; }
    public DbSet<GroupDuesRate> GroupDuesRates { get; set; }
    public DbSet<DuesConcession> DuesConcessions { get; set; }
    public DbSet<DuesConcessionGrant> DuesConcessionGrants { get; set; }
    public DbSet<DuesCharge> DuesCharges { get; set; }
    public DbSet<DuesEntry> DuesEntries { get; set; }
    public DbSet<DuesEntryEvent> DuesEntryEvents { get; set; }
    public DbSet<KurinDuesSettings> KurinDuesSettings { get; set; }

    public DbSet<KurinScoreSettings> KurinScoreSettings { get; set; }
    public DbSet<ScoreRule> ScoreRules { get; set; }
    public DbSet<ScoreAttendanceRate> ScoreAttendanceRates { get; set; }
    public DbSet<ScoreItem> ScoreItems { get; set; }
    public DbSet<ScoreStage> ScoreStages { get; set; }
    public DbSet<ScoreAttendance> ScoreAttendances { get; set; }
    public DbSet<ScoreNotHeldOccurrence> ScoreNotHeldOccurrences { get; set; }
    public DbSet<ScoreEntry> ScoreEntries { get; set; }
    public DbSet<ScoreGroupMove> ScoreGroupMoves { get; set; }
    public DbSet<ScoreTrailEvent> ScoreTrailEvents { get; set; }
    public DbSet<PrivateScoreCriterion> PrivateScoreCriteria { get; set; }
    public DbSet<PrivateScoreEntry> PrivateScoreEntries { get; set; }

    public DbSet<KurinAuditEvent> KurinAuditEvents { get; set; }

    public DbSet<WaitlistEntry> WaitlistEntries { get; set; }
    public DbSet<UserRefreshToken> UserRefreshTokens { get; set; }
    public DbSet<CalendarFeed> CalendarFeeds { get; set; }
    public DbSet<Invitation> Invitations { get; set; }
    public DbSet<AppNotification> AppNotifications { get; set; }
    public DbSet<WebPushSubscription> WebPushSubscriptions { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<UserTileLayout> UserTileLayouts { get; set; }
    public DbSet<DismissedDuty> DismissedDuties { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // After the base call: AppUserConfiguration adds to the user table Identity has just mapped.
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
