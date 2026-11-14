using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;

public sealed record SetScoreRuleCommand(Guid KurinKey, SetScoreRuleRequest Request) : IRequest<ServiceResult<Unit>>;

public sealed class SetScoreRuleCommandValidator : AbstractValidator<SetScoreRuleCommand>
{
    private static readonly ScoreSource[] Automatic =
        [ScoreSource.Skill, ScoreSource.ProbePoint, ScoreSource.Probe, ScoreSource.Dues, ScoreSource.Warning];

    public SetScoreRuleCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request.Source).Must(s => Automatic.Contains(s))
            .WithMessage("Only an automatic source has a rule; attendance is priced by event.");
        RuleFor(c => c.Request.Variant).InclusiveBetween(1, 3).When(c => c.Request.Source == ScoreSource.Warning)
            .WithMessage("A пересторога has a level of 1 to 3.");
        RuleFor(c => c.Request.Variant).Equal(0).When(c => c.Request.Source != ScoreSource.Warning);
        RuleFor(c => c.Request.Points).InclusiveBetween(-1000, 1000);
        RuleFor(c => c.Request.Points).LessThanOrEqualTo(0).When(c => c.Request.Source == ScoreSource.Warning)
            .WithMessage("A пересторога takes points, never gives them.");
        RuleFor(c => c.Request.Points).GreaterThanOrEqualTo(0).When(c => c.Request.Source != ScoreSource.Warning);
    }
}

public sealed class SetScoreRuleCommandHandler : IRequestHandler<SetScoreRuleCommand, ServiceResult<Unit>>
{
    private readonly ScoreSettingsAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public SetScoreRuleCommandHandler(ScoreSettingsAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(SetScoreRuleCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<Unit>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var r = request.Request;
        var history = (await _score.ScoreRules.GetForKurinAsync(request.KurinKey, cancellationToken))
            .Where(x => x.Source == r.Source && x.Variant == r.Variant)
            .OrderBy(x => x.FromDate)
            .ToList();
        var latest = history.LastOrDefault();
        var sameDay = history.FirstOrDefault(x => x.FromDate == r.FromDate);

        ScoreRule rule;
        if (sameDay is not null)
        {
            // A rule already starts that day: this is its new rate.
            rule = (await _score.ScoreRules.GetByKeyAsync(sameDay.ScoreRuleKey, cancellationToken))!;
        }
        else if (latest is null || (r.Points != latest.Points && r.FromDate > latest.FromDate))
        {
            // A different rate from a later day: the rule in force stays for what was earned before it.
            rule = new ScoreRule { KurinKey = request.KurinKey, Source = r.Source, Variant = r.Variant, FromDate = r.FromDate, CreatedDate = now };
            _score.ScoreRules.Create(rule, cancellationToken);
        }
        else
        {
            // The same rate from another day, or any rate from an earlier one, corrects the rule in
            // force rather than adding to it. Adding was what happened before, and the same rate
            // from a later day then changed nothing: the old row kept paying for the days in
            // between — a провід who set a rule on the day they entered a year of history could not
            // push its start past that day, and the history scored itself.
            var previous = history.Count > 1 ? history[^2] : null;
            if (previous is not null && r.FromDate <= previous.FromDate)
            {
                return ServiceResult<Unit>.Failure(
                    ResultType.BadRequest,
                    ScoreErrors.ScoreRuleOverlap,
                    "The rule cannot start on or before the day the previous one did.");
            }

            rule = (await _score.ScoreRules.GetByKeyAsync(latest.ScoreRuleKey, cancellationToken))!;
            rule.FromDate = r.FromDate;
        }

        rule.Points = r.Points;
        rule.SetByUserKey = _currentUser.UserId;
        rule.UpdatedDate = now;
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Rule, rule.ScoreRuleKey, ScoreTrail.Updated,
            new { rule.Source, rule.Variant, rule.FromDate, rule.Points }, _currentUser.UserId, now);

        await DropRulesThatChangeNothingAsync(request.KurinKey, history, rule, now, cancellationToken);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    /// <summary>
    /// The history must read as a list of changes. A rule that repeats the rate of the one before
    /// it changes nothing, and it hides the day the rate really started: retyping an older rule to
    /// the rate of a later duplicate left the duplicate in force and the page kept showing its
    /// date — «the date does not change». Such duplicates were what the old handler added on every
    /// save of the same rate, so every kurin that set its rules before 1.1.2 may carry them.
    /// </summary>
    private async Task DropRulesThatChangeNothingAsync(
        Guid kurinKey, IReadOnlyList<ScoreRule> history, ScoreRule changed, DateTime now, CancellationToken cancellationToken)
    {
        var rows = history
            .Where(x => x.ScoreRuleKey != changed.ScoreRuleKey)
            .Select(x => (x.ScoreRuleKey, x.FromDate, x.Points))
            .Append((changed.ScoreRuleKey, changed.FromDate, changed.Points))
            .OrderBy(x => x.FromDate)
            .ToList();

        int? rateBefore = null;
        foreach (var (key, fromDate, points) in rows)
        {
            if (rateBefore == points)
            {
                var redundant = key == changed.ScoreRuleKey ? changed : (await _score.ScoreRules.GetByKeyAsync(key, cancellationToken))!;
                _score.ScoreRules.Delete(redundant, cancellationToken);
                ScoreTrail.Record(_score, kurinKey, ScoreTrail.Rule, key, ScoreTrail.Deleted,
                    new { redundant.Source, redundant.Variant, FromDate = fromDate, Points = points, Reason = "SameRateAsBefore" }, _currentUser.UserId, now);
                continue;
            }

            rateBefore = points;
        }
    }
}
