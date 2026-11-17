namespace ProjectK.Common.Rules.Shared;

/// <summary>
/// How a paged read takes the window a client asks for: a negative skip starts at the top, and the
/// page holds at least one row and never more than the read's own ceiling.
/// </summary>
public static class PagingRules
{
    /// <summary>Rows to pass over; zero for a negative request.</summary>
    public static int Skip(int skip) => Math.Max(0, skip);

    /// <summary>Rows to return: the request held between one and <paramref name="maxTake"/>.</summary>
    public static int Take(int take, int maxTake) => Math.Clamp(take, 1, maxTake);
}
