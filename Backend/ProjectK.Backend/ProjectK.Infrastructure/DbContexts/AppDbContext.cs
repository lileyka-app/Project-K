using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using ProjectK.Common.Models.Records;

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

    /// <summary>
    /// Stamps a new member's public code before it is written. It sits here rather than in a use
    /// case because a member is also opened by the seeders and by account activation, and a person
    /// without a code cannot be found by the one thing another kurin can ask for.
    /// <para>
    /// The гурток stints of memberships are kept here for the same reason: a person changes гурток
    /// on joining, moving, leaving, a transfer, an import and a dissolved гурток, and a path that
    /// forgot to write the stint would leave a hole in the archive nobody notices.
    /// </para>
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampNewMembers();
        var moves = GroupMoves();
        var keys = moves.Where(move => move.WasIn is not null).Select(move => move.Membership.MembershipKey).ToList();
        ApplyGroupMoves(moves, keys.Count == 0 ? [] : MembershipGroupStints.Where(stint => keys.Contains(stint.MembershipKey) && stint.ToUtc == null).ToList());
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampNewMembers();
        var moves = GroupMoves();
        var keys = moves.Where(move => move.WasIn is not null).Select(move => move.Membership.MembershipKey).ToList();
        ApplyGroupMoves(moves, keys.Count == 0 ? [] : await MembershipGroupStints.Where(stint => keys.Contains(stint.MembershipKey) && stint.ToUtc == null).ToListAsync(cancellationToken));
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampNewMembers()
    {
        foreach (var entry in ChangeTracker.Entries<Member>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.PublicId))
            {
                entry.Entity.PublicId = MemberPublicId.New();
            }
        }
    }

    /// <summary>A membership whose гурток changed: where it stood before this save and where it stands after.</summary>
    private sealed record GroupMove(Membership Membership, bool Added, Guid? WasIn, Guid? IsIn);

    private List<GroupMove> GroupMoves()
    {
        var moves = new List<GroupMove>();
        foreach (var entry in ChangeTracker.Entries<Membership>())
        {
            var membership = entry.Entity;
            if (entry.State == EntityState.Added && membership.GroupKey is not null)
            {
                moves.Add(new GroupMove(membership, true, null, membership.GroupKey));
            }
            else if (entry.State == EntityState.Modified)
            {
                var wasIn = entry.Property(e => e.LeftAtUtc).OriginalValue is null ? entry.Property(e => e.GroupKey).OriginalValue : null;
                var isIn = membership.LeftAtUtc is null ? membership.GroupKey : null;
                if (wasIn != isIn)
                {
                    moves.Add(new GroupMove(membership, false, wasIn, isIn));
                }
            }
        }

        return moves;
    }

    private void ApplyGroupMoves(List<GroupMove> moves, List<MembershipGroupStint> openInStore)
    {
        var now = DateTime.UtcNow;
        foreach (var move in moves)
        {
            var membershipKey = move.Membership.MembershipKey;
            if (move.Added)
            {
                // Joined straight into a гурток — or seeded as a membership that has already ended.
                MembershipGroupStints.Add(new MembershipGroupStint
                {
                    MembershipKey = membershipKey,
                    GroupKey = move.IsIn!.Value,
                    FromUtc = move.Membership.JoinedAtUtc == default ? now : move.Membership.JoinedAtUtc,
                    ToUtc = move.Membership.LeftAtUtc
                });
                continue;
            }

            if (move.WasIn is { } leftGroup)
            {
                var until = move.Membership.LeftAtUtc ?? now;
                var open = MembershipGroupStints.Local.Concat(openInStore)
                    .FirstOrDefault(stint => stint.MembershipKey == membershipKey && stint.GroupKey == leftGroup && stint.ToUtc == null);
                if (open is null)
                {
                    MembershipGroupStints.Add(new MembershipGroupStint { MembershipKey = membershipKey, GroupKey = leftGroup, ToUtc = until });
                }
                else
                {
                    open.ToUtc = until;
                }
            }

            if (move.IsIn is { } enteredGroup)
            {
                MembershipGroupStints.Add(new MembershipGroupStint { MembershipKey = membershipKey, GroupKey = enteredGroup, FromUtc = now });
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // After the base call: AppUserConfiguration adds to the user table Identity has just mapped.
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
