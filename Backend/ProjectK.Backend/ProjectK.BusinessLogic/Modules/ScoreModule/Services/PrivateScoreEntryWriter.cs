using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Models;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>What a new and an edited entry share: the youth is here, the criterion is on the list.</summary>
public static class PrivateScoreEntryWriter
{
    public static async Task<ServiceResult<T>?> CheckAsync<T>(IScoreUnitOfWork score, ScoreBook book, UpsertPrivateScoreEntryRequest request, CancellationToken cancellationToken)
    {
        if (!book.Memberships.TryGetValue(request.MembershipKey, out var membership) || membership.Kind != MembershipKind.Youth)
        {
            return ScoreAccess.NotFound<T>("That person is not a youth of this kurin.");
        }

        if (request.PrivateScoreCriterionKey is { } key)
        {
            var criteria = await score.PrivateScoreCriteria.GetForKurinAsync(book.KurinKey, cancellationToken);
            if (criteria.All(c => c.PrivateScoreCriterionKey != key || c.IsArchived))
            {
                return ScoreAccess.NotFound<T>("There is no such criterion on the list.");
            }
        }

        return null;
    }

    public static void Apply(PrivateScoreEntry entry, UpsertPrivateScoreEntryRequest request)
    {
        entry.PrivateScoreCriterionKey = request.PrivateScoreCriterionKey;
        entry.Points = request.Points;
        entry.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        entry.OccurredOn = request.OccurredOn;
    }

    public static object Snapshot(PrivateScoreEntry e) => new
    {
        e.MembershipKey,
        e.PrivateScoreCriterionKey,
        e.Points,
        e.Note,
        e.OccurredOn,
        e.DeletedAtUtc,
        e.DeletedByUserKey
    };
}
