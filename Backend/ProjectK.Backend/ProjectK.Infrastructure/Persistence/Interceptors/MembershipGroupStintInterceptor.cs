using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProjectK.Common.Entities.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Keeps the гурток stints of memberships on every save. A person changes гурток on joining, moving,
/// leaving, a transfer, an import and a dissolved гурток, and a path that forgot to write the stint
/// would leave a hole in the archive nobody notices.
/// </summary>
public sealed class MembershipGroupStintInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            var moves = GroupMoves(context);
            var keys = LeftMembershipKeys(moves);
            ApplyGroupMoves(context, moves, keys.Count == 0 ? [] : OpenStints(context, keys).ToList());
        }

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var moves = GroupMoves(context);
            var keys = LeftMembershipKeys(moves);
            ApplyGroupMoves(context, moves, keys.Count == 0 ? [] : await OpenStints(context, keys).ToListAsync(cancellationToken));
        }

        return result;
    }

    /// <summary>A membership whose гурток changed: where it stood before this save and where it stands after.</summary>
    private sealed record GroupMove(Membership Membership, bool Added, Guid? WasIn, Guid? IsIn);

    private static List<Guid> LeftMembershipKeys(List<GroupMove> moves) =>
        moves.Where(move => move.WasIn is not null).Select(move => move.Membership.MembershipKey).ToList();

    private static IQueryable<MembershipGroupStint> OpenStints(DbContext context, List<Guid> keys) =>
        context.Set<MembershipGroupStint>().Where(stint => keys.Contains(stint.MembershipKey) && stint.ToUtc == null);

    private static List<GroupMove> GroupMoves(DbContext context)
    {
        var moves = new List<GroupMove>();
        foreach (var entry in context.ChangeTracker.Entries<Membership>())
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

    private static void ApplyGroupMoves(DbContext context, List<GroupMove> moves, List<MembershipGroupStint> openInStore)
    {
        var stints = context.Set<MembershipGroupStint>();
        var now = DateTime.UtcNow;
        foreach (var move in moves)
        {
            var membershipKey = move.Membership.MembershipKey;
            if (move.Added)
            {
                // Joined straight into a гурток — or seeded as a membership that has already ended.
                stints.Add(new MembershipGroupStint
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
                var open = stints.Local.Concat(openInStore)
                    .FirstOrDefault(stint => stint.MembershipKey == membershipKey && stint.GroupKey == leftGroup && stint.ToUtc == null);
                if (open is null)
                {
                    stints.Add(new MembershipGroupStint { MembershipKey = membershipKey, GroupKey = leftGroup, ToUtc = until });
                }
                else
                {
                    open.ToUtc = until;
                }
            }

            if (move.IsIn is { } enteredGroup)
            {
                stints.Add(new MembershipGroupStint { MembershipKey = membershipKey, GroupKey = enteredGroup, FromUtc = now });
            }
        }
    }
}
