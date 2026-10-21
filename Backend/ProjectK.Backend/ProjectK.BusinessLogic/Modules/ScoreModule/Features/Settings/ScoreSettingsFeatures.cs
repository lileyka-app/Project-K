using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Models;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;

/// <summary>What every settings write shares: it is the caller's kurin, and they manage its score.</summary>
public sealed class ScoreSettingsAccess
{
    private readonly ScoreAccess _access;

    public ScoreSettingsAccess(ScoreAccess access)
    {
        _access = access;
    }

    public async Task<ServiceResult<T>?> RefuseAsync<T>(Guid kurinKey, CancellationToken cancellationToken)
    {
        if (_access.Refuse<T>(kurinKey) is { } refused)
        {
            return refused;
        }

        return await _access.MayManageAsync(kurinKey, cancellationToken) ? null : ScoreAccess.Forbidden<T>();
    }
}

public sealed record GetKurinScoreSettingsQuery(Guid KurinKey) : IRequest<ServiceResult<KurinScoreSettingsResponse>>;

public sealed class GetKurinScoreSettingsQueryHandler : IRequestHandler<GetKurinScoreSettingsQuery, ServiceResult<KurinScoreSettingsResponse>>
{
    private readonly ScoreSettingsAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IUnitOfWork _unitOfWork;

    public GetKurinScoreSettingsQueryHandler(ScoreSettingsAccess access, ScoreBookReader books, IUnitOfWork unitOfWork)
    {
        _access = access;
        _books = books;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<KurinScoreSettingsResponse>> Handle(GetKurinScoreSettingsQuery request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<KurinScoreSettingsResponse>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        var categories = await _unitOfWork.AgendaCategories.GetForKurinAsync(request.KurinKey, includeArchived: false, cancellationToken);

        return new ServiceResult<KurinScoreSettingsResponse>(ResultType.Success, new KurinScoreSettingsResponse
        {
            KurinKey = request.KurinKey,
            Algorithm = book.Algorithm,
            AttendanceRates = categories
                .OrderBy(c => c.Name)
                .Select(c => new ScoreAttendanceRateDto
                {
                    AgendaCategoryKey = c.AgendaCategoryKey,
                    CategoryName = c.Name,
                    CategoryColorHex = c.ColorHex,
                    CategoryIcon = c.Icon,
                    Points = book.AttendanceRates.FirstOrDefault(r => r.AgendaCategoryKey == c.AgendaCategoryKey)?.Points ?? 0
                })
                .ToList(),
            Rules = book.Rules
                .Select(r => new ScoreRuleDto { Source = r.Source, Variant = r.Variant, FromDate = r.FromDate, Points = r.Points })
                .ToList(),
            Items = book.Items.Select(ScoreBook.ToDto).ToList(),
            Stages = book.Stages
                .OrderByDescending(s => s.FromDate)
                .Select(s => new ScoreStageDto { ScoreStageKey = s.ScoreStageKey, Name = s.Name, FromDate = s.FromDate, ToDate = s.ToDate })
                .ToList()
        });
    }
}

public sealed record SetScoreAlgorithmCommand(Guid KurinKey, SetScoreAlgorithmRequest Request) : IRequest<ServiceResult<object>>;

public sealed class SetScoreAlgorithmCommandValidator : AbstractValidator<SetScoreAlgorithmCommand>
{
    public SetScoreAlgorithmCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request.Algorithm).IsInEnum();
    }
}

public sealed class SetScoreAlgorithmCommandHandler : IRequestHandler<SetScoreAlgorithmCommand, ServiceResult<object>>
{
    private readonly ScoreSettingsAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public SetScoreAlgorithmCommandHandler(ScoreSettingsAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(SetScoreAlgorithmCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<object>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var settings = await _score.KurinScoreSettings.GetForKurinAsync(request.KurinKey, cancellationToken);
        if (settings is null)
        {
            settings = new KurinScoreSettings { KurinKey = request.KurinKey, CreatedDate = now };
            _score.KurinScoreSettings.Create(settings, cancellationToken);
        }

        settings.Algorithm = request.Request.Algorithm;
        settings.SetByUserKey = _currentUser.UserId;
        settings.UpdatedDate = now;
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Settings, settings.KurinScoreSettingsKey, ScoreTrail.Updated, new { settings.Algorithm }, _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}

public sealed record SetScoreRuleCommand(Guid KurinKey, SetScoreRuleRequest Request) : IRequest<ServiceResult<object>>;

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

public sealed class SetScoreRuleCommandHandler : IRequestHandler<SetScoreRuleCommand, ServiceResult<object>>
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

    public async Task<ServiceResult<object>> Handle(SetScoreRuleCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<object>(request.KurinKey, cancellationToken) is { } refused)
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
                return ServiceResult<object>.Failure(
                    ResultType.BadRequest,
                    "ScoreRuleOverlap",
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
        return new ServiceResult<object>(ResultType.Success);
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

public sealed record SetScoreAttendanceRateCommand(Guid KurinKey, SetScoreAttendanceRateRequest Request) : IRequest<ServiceResult<object>>;

public sealed class SetScoreAttendanceRateCommandValidator : AbstractValidator<SetScoreAttendanceRateCommand>
{
    public SetScoreAttendanceRateCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).Must(r => r.AgendaCategoryKey.HasValue != r.AgendaItemKey.HasValue)
            .WithMessage("A rate is for a group of events or for one event.");
        RuleFor(c => c.Request.Points).InclusiveBetween(0, 1000).When(c => c.Request.Points.HasValue);
        RuleFor(c => c.Request.Points).NotNull().When(c => c.Request.AgendaCategoryKey.HasValue)
            .WithMessage("A group of events is always worth something, if only zero.");
    }
}

public sealed class SetScoreAttendanceRateCommandHandler : IRequestHandler<SetScoreAttendanceRateCommand, ServiceResult<object>>
{
    private readonly ScoreSettingsAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public SetScoreAttendanceRateCommandHandler(ScoreSettingsAccess access, IScoreUnitOfWork score, IUnitOfWork unitOfWork, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(SetScoreAttendanceRateCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<object>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var r = request.Request;
        if (await MissingTargetAsync(request, cancellationToken) is { } missing)
        {
            return missing;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var found = (await _score.ScoreAttendanceRates.GetForKurinAsync(request.KurinKey, cancellationToken))
            .FirstOrDefault(x => r.AgendaCategoryKey.HasValue ? x.AgendaCategoryKey == r.AgendaCategoryKey : x.AgendaItemKey == r.AgendaItemKey);
        var rate = found is null ? null : await _score.ScoreAttendanceRates.GetByKeyAsync(found.ScoreAttendanceRateKey, cancellationToken);

        if (r.Points is null)
        {
            if (rate is not null)
            {
                _score.ScoreAttendanceRates.Delete(rate, cancellationToken);
                ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.AttendanceRate, rate.ScoreAttendanceRateKey, ScoreTrail.Deleted,
                    new { rate.AgendaCategoryKey, rate.AgendaItemKey, rate.Points }, _currentUser.UserId, now);
                await _score.SaveChangesAsync(cancellationToken);
            }

            return new ServiceResult<object>(ResultType.Success);
        }

        if (rate is null)
        {
            rate = new ScoreAttendanceRate { KurinKey = request.KurinKey, AgendaCategoryKey = r.AgendaCategoryKey, AgendaItemKey = r.AgendaItemKey, CreatedDate = now };
            _score.ScoreAttendanceRates.Create(rate, cancellationToken);
        }

        rate.Points = r.Points.Value;
        rate.SetByUserKey = _currentUser.UserId;
        rate.UpdatedDate = now;
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.AttendanceRate, rate.ScoreAttendanceRateKey, ScoreTrail.Updated,
            new { rate.AgendaCategoryKey, rate.AgendaItemKey, rate.Points }, _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }

    private async Task<ServiceResult<object>?> MissingTargetAsync(SetScoreAttendanceRateCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        if (r.AgendaCategoryKey is { } categoryKey)
        {
            var category = await _unitOfWork.AgendaCategories.GetByKeyAsync(categoryKey, cancellationToken);
            return category is null || category.KurinKey != request.KurinKey
                ? ScoreAccess.NotFound<object>("There is no such group of events here.")
                : null;
        }

        var item = await _unitOfWork.AgendaItems.GetByKeyAsync(r.AgendaItemKey!.Value, cancellationToken);
        return item is null || item.KurinKey != request.KurinKey || item.Kind != AgendaItemKind.Event
            ? ScoreAccess.NotFound<object>("There is no such event here.")
            : null;
    }
}

public sealed class UpsertScoreItemRequestValidator : AbstractValidator<UpsertScoreItemRequest>
{
    public UpsertScoreItemRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
        RuleFor(r => r.Points).NotEqual(0).InclusiveBetween(-1000, 1000);
    }
}

/// <summary>A new position on the list, or a change to one. A change does not touch what was already given by it.</summary>
public sealed record UpsertScoreItemCommand(Guid KurinKey, Guid? ScoreItemKey, UpsertScoreItemRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class UpsertScoreItemCommandValidator : AbstractValidator<UpsertScoreItemCommand>
{
    public UpsertScoreItemCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertScoreItemRequestValidator());
    }
}

public sealed class UpsertScoreItemCommandHandler : IRequestHandler<UpsertScoreItemCommand, ServiceResult<Guid>>
{
    private readonly ScoreSettingsAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public UpsertScoreItemCommandHandler(ScoreSettingsAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Guid>> Handle(UpsertScoreItemCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<Guid>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        ScoreItem item;
        if (request.ScoreItemKey is { } key)
        {
            var found = await _score.ScoreItems.GetByKeyAsync(key, cancellationToken);
            if (found is null || found.KurinKey != request.KurinKey)
            {
                return ScoreAccess.NotFound<Guid>("There is no such position on the list.");
            }

            item = found;
        }
        else
        {
            item = new ScoreItem { KurinKey = request.KurinKey, CreatedDate = now };
            _score.ScoreItems.Create(item, cancellationToken);
        }

        item.Name = request.Request.Name.Trim();
        item.Points = request.Request.Points;
        item.IsArchived = request.Request.IsArchived;
        item.UpdatedDate = now;
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Item, item.ScoreItemKey, request.ScoreItemKey is null ? ScoreTrail.Created : ScoreTrail.Updated,
            new { item.Name, item.Points, item.IsArchived }, _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Guid>(request.ScoreItemKey is null ? ResultType.Created : ResultType.Success, item.ScoreItemKey);
    }
}

public sealed class UpsertScoreStageRequestValidator : AbstractValidator<UpsertScoreStageRequest>
{
    public UpsertScoreStageRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
        RuleFor(r => r.ToDate).GreaterThanOrEqualTo(r => r.FromDate).WithMessage("A stage ends no earlier than it starts.");
    }
}

public sealed record UpsertScoreStageCommand(Guid KurinKey, Guid? ScoreStageKey, UpsertScoreStageRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class UpsertScoreStageCommandValidator : AbstractValidator<UpsertScoreStageCommand>
{
    public UpsertScoreStageCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertScoreStageRequestValidator());
    }
}

public sealed class UpsertScoreStageCommandHandler : IRequestHandler<UpsertScoreStageCommand, ServiceResult<Guid>>
{
    private readonly ScoreSettingsAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public UpsertScoreStageCommandHandler(ScoreSettingsAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Guid>> Handle(UpsertScoreStageCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<Guid>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        ScoreStage stage;
        if (request.ScoreStageKey is { } key)
        {
            var found = await _score.ScoreStages.GetByKeyAsync(key, cancellationToken);
            if (found is null || found.KurinKey != request.KurinKey)
            {
                return ScoreAccess.NotFound<Guid>("There is no such stage.");
            }

            stage = found;
        }
        else
        {
            stage = new ScoreStage { KurinKey = request.KurinKey, CreatedDate = now };
            _score.ScoreStages.Create(stage, cancellationToken);
        }

        stage.Name = request.Request.Name.Trim();
        stage.FromDate = request.Request.FromDate;
        stage.ToDate = request.Request.ToDate;
        stage.UpdatedDate = now;
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Stage, stage.ScoreStageKey, request.ScoreStageKey is null ? ScoreTrail.Created : ScoreTrail.Updated,
            new { stage.Name, stage.FromDate, stage.ToDate }, _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Guid>(request.ScoreStageKey is null ? ResultType.Created : ResultType.Success, stage.ScoreStageKey);
    }
}

/// <summary>A stage is only a slice: dropping it loses no points, so it goes for good.</summary>
public sealed record DeleteScoreStageCommand(Guid KurinKey, Guid ScoreStageKey) : IRequest<ServiceResult<object>>;

public sealed class DeleteScoreStageCommandHandler : IRequestHandler<DeleteScoreStageCommand, ServiceResult<object>>
{
    private readonly ScoreSettingsAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public DeleteScoreStageCommandHandler(ScoreSettingsAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(DeleteScoreStageCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<object>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var stage = await _score.ScoreStages.GetByKeyAsync(request.ScoreStageKey, cancellationToken);
        if (stage is null || stage.KurinKey != request.KurinKey)
        {
            return ScoreAccess.NotFound<object>("There is no such stage.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        _score.ScoreStages.Delete(stage, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Stage, stage.ScoreStageKey, ScoreTrail.Deleted,
            new { stage.Name, stage.FromDate, stage.ToDate }, _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
