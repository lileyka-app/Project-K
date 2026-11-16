using FluentValidation;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.MemberModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Services;

/// <summary>What a new and an edited entry share: the youth is here, the criterion is on the list.</summary>
public static class PrivateScoreEntryRules
{
    /// <summary>A youth, a non-zero amount within bounds, what it was for, and a day not ahead of today.</summary>
    public static void AddShapeRules(AbstractValidator<UpsertPrivateScoreEntryRequest> rules, TimeProvider time)
    {
        rules.RuleFor(r => r.MembershipKey).NotEmpty();
        rules.RuleFor(r => r.Points).NotEqual(0).InclusiveBetween(-1000, 1000);
        rules.RuleFor(r => r.Note).MaximumLength(500);
        rules.RuleFor(r => r).Must(r => r.PrivateScoreCriterionKey.HasValue || !string.IsNullOrWhiteSpace(r.Note))
            .WithMessage("Say what it was for: a criterion, or a note.");
        rules.RuleFor(r => r.OccurredOn).LessThanOrEqualTo(_ => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(1));
    }

    public static async Task<ServiceResult<T>?> CheckAsync<T>(IScoreUnitOfWork score, ScoreBook book, UpsertPrivateScoreEntryRequest request, CancellationToken cancellationToken)
    {
        if (!book.Memberships.TryGetValue(request.MembershipKey, out var membership) || membership.Kind != MembershipKind.Youth)
        {
            return ScoreErrors.NotFound<T>("That person is not a youth of this kurin.");
        }

        if (request.PrivateScoreCriterionKey is { } key)
        {
            var criteria = await score.PrivateScoreCriteria.GetForKurinAsync(book.KurinKey, cancellationToken);
            if (criteria.All(c => c.PrivateScoreCriterionKey != key || c.IsArchived))
            {
                return ScoreErrors.NotFound<T>("There is no such criterion on the list.");
            }
        }

        return null;
    }

    public static void Apply(PrivateScoreEntry entry, UpsertPrivateScoreEntryRequest request)
    {
        entry.PrivateScoreCriterionKey = request.PrivateScoreCriterionKey;
        entry.Points = request.Points;
        entry.Note = TextRules.TrimOrNull(request.Note);
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
