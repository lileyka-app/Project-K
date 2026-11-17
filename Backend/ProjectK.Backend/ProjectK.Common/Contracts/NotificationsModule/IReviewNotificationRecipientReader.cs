using ProjectK.Common.Enums.NotificationsModule;

namespace ProjectK.Common.Contracts.NotificationsModule;

/// <summary>Who is told that something in a kurin waits on a reviewer.</summary>
public interface IReviewNotificationRecipientReader
{
    /// <summary>
    /// The accounts that may act on <paramref name="subject"/> for a member of
    /// <paramref name="groupKey"/> in <paramref name="kurinKey"/>, without
    /// <paramref name="excludedUserKey"/> (the one who caused it).
    /// </summary>
    Task<IReadOnlyCollection<Guid>> ResolveAsync(
        ReviewSubject subject,
        Guid kurinKey,
        Guid? groupKey,
        Guid? excludedUserKey,
        CancellationToken cancellationToken = default);
}
