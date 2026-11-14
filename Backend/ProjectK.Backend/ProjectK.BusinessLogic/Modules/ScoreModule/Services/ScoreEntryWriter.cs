using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>The checks a new and an edited entry share, with the book open.</summary>
public static class ScoreEntryWriter
{
    /// <summary>Null when the entry may be written as asked; otherwise why not.</summary>
    public static async Task<ServiceResult<T>?> CheckAsync<T>(
        ScoreAccess access,
        IUnitOfWork unitOfWork,
        IKraiProfileProvider profiles,
        ScoreBook book,
        UpsertScoreEntryRequest request,
        Guid? editingKey,
        CancellationToken cancellationToken)
    {
        if (ResolveGroup<T>(book, request, out var groupKey) is { } unknownTarget)
        {
            return unknownTarget;
        }

        if (!await access.MayScoreAsync(book.KurinKey, groupKey, ResourceAction.Create, cancellationToken))
        {
            return ScoreErrors.Refused<T>();
        }

        if (request.ScoreItemKey is { } itemKey && book.Items.All(i => i.ScoreItemKey != itemKey || i.IsArchived))
        {
            return ScoreErrors.NotFound<T>("There is no such position on the list.");
        }

        if (request.AgendaItemKey is { } eventKey)
        {
            var occurrence = await EventOccurrence.LoadAsync(unitOfWork, profiles, book.KurinKey, eventKey, request.OccurrenceStartUtc!.Value, cancellationToken);
            if (occurrence is null)
            {
                return ScoreErrors.NotFound<T>("There is no such event, or it does not meet that day.");
            }

            if (request.ScoreItemKey is { } positionKey)
            {
                return AlreadyGiven<T>(book, request, editingKey, positionKey, eventKey, occurrence.StartUtc);
            }
        }

        return null;
    }

    private static ServiceResult<T>? ResolveGroup<T>(ScoreBook book, UpsertScoreEntryRequest request, out Guid? groupKey)
    {
        groupKey = null;
        if (request.MembershipKey is { } membershipKey)
        {
            if (!book.Memberships.TryGetValue(membershipKey, out var membership) || membership.Kind != MembershipKind.Youth)
            {
                return ScoreErrors.NotFound<T>("That person is not a youth of this kurin.");
            }

            groupKey = membership.LeftAtUtc is null ? membership.GroupKey : null;
        }
        else
        {
            groupKey = request.GroupKey;
            if (!book.GroupNames.ContainsKey(groupKey!.Value))
            {
                return ScoreErrors.NotFound<T>("There is no such гурток here.");
            }
        }

        return null;
    }

    private static ServiceResult<T>? AlreadyGiven<T>(ScoreBook book, UpsertScoreEntryRequest request, Guid? editingKey, Guid positionKey, Guid eventKey, DateTime occurrenceStartUtc)
    {
        var already = book.Entries.FirstOrDefault(e =>
            e.ScoreEntryKey != editingKey
            && e.ScoreItemKey == positionKey
            && e.AgendaItemKey == eventKey
            && e.OccurrenceStartUtc == occurrenceStartUtc
            && e.MembershipKey == request.MembershipKey
            && e.GroupKey == request.GroupKey);
        if (already is not null)
        {
            return ServiceResult<T>.Failure(ResultType.Conflict, ScoreErrors.ItemAlreadyGiven,
                $"Уже дав {book.NameOfAccount(already.CreatedByUserKey) ?? "хтось"}.");
        }

        return null;
    }

    /// <summary>
    /// The same position was given for the same event between our check and our save — the unique
    /// index caught what the check could not. Said the way the check would have said it.
    /// </summary>
    public static ServiceResult<T> GivenMeanwhile<T>() =>
        ServiceResult<T>.Failure(ResultType.Conflict, ScoreErrors.ItemAlreadyGiven, "Уже дав хтось інший щойно.");

    public static void Apply(ScoreEntry entry, UpsertScoreEntryRequest request, ScoreBook book)
    {
        entry.ScoreItemKey = request.ScoreItemKey;
        entry.Points = request.ScoreItemKey is { } itemKey
            ? book.Items.First(i => i.ScoreItemKey == itemKey).Points
            : request.Points!.Value;
        entry.Reason = request.ScoreItemKey.HasValue ? null : request.Reason!.Trim();
        entry.AgendaItemKey = request.AgendaItemKey;
        entry.OccurrenceStartUtc = request.AgendaItemKey.HasValue ? DateTime.SpecifyKind(request.OccurrenceStartUtc!.Value, DateTimeKind.Utc) : null;
        entry.OccurredOn = request.OccurredOn;
    }

    public static object Snapshot(ScoreEntry e) => new
    {
        e.MembershipKey,
        e.GroupKey,
        e.ScoreItemKey,
        e.Points,
        e.Reason,
        e.AgendaItemKey,
        e.OccurrenceStartUtc,
        e.OccurredOn,
        e.DeletedAtUtc,
        e.DeletedByUserKey
    };
}
