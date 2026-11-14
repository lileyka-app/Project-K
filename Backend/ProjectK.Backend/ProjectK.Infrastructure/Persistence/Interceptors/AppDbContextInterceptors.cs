using Microsoft.EntityFrameworkCore;

namespace ProjectK.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The save-time rules every <see cref="DbContexts.AppDbContext"/> carries, in the order they run.
/// </summary>
/// <remarks>
/// Every place that builds the context's options calls this: the host, the design-time factory and
/// the tests. A context built without it saves members with no public code and memberships with no
/// гурток stints, and nothing fails to say so.
/// </remarks>
public static class AppDbContextInterceptors
{
    private static readonly MemberPublicIdInterceptor PublicIds = new();
    private static readonly MembershipGroupStintInterceptor GroupStints = new();

    // Not named AddInterceptors: EF's own AddInterceptors(params IInterceptor[]) would win the call.
    public static TBuilder AddAppDbContextInterceptors<TBuilder>(this TBuilder builder)
        where TBuilder : DbContextOptionsBuilder
    {
        builder.AddInterceptors(PublicIds, GroupStints);
        return builder;
    }
}
