using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps a new member's public code before it is written. It sits on the save rather than in a use
/// case because a member is also opened by the seeders and by account activation, and a person
/// without a code cannot be found by the one thing another kurin can ask for.
/// </summary>
public sealed class MemberPublicIdInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            StampNewMembers(context);
        }

        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            StampNewMembers(context);
        }

        return ValueTask.FromResult(result);
    }

    private static void StampNewMembers(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<Member>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.PublicId))
            {
                entry.Entity.PublicId = MemberPublicId.New();
            }
        }
    }
}
