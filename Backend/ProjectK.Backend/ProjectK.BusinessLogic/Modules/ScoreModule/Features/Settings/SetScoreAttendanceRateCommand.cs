using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;

public sealed record SetScoreAttendanceRateCommand(Guid KurinKey, SetScoreAttendanceRateRequest Request) : IRequest<ServiceResult<Unit>>;

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

public sealed class SetScoreAttendanceRateCommandHandler : IRequestHandler<SetScoreAttendanceRateCommand, ServiceResult<Unit>>
{
    private readonly ScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly IAgendaDirectory _agenda;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public SetScoreAttendanceRateCommandHandler(ScoreAccess access, IScoreUnitOfWork score, IAgendaDirectory agenda, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _agenda = agenda;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(SetScoreAttendanceRateCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseSettingsAsync<Unit>(request.KurinKey, cancellationToken) is { } refused)
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

            return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
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
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }

    private async Task<ServiceResult<Unit>?> MissingTargetAsync(SetScoreAttendanceRateCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;
        if (r.AgendaCategoryKey is { } categoryKey)
        {
            var category = await _agenda.FindCategoryAsync(categoryKey, cancellationToken);
            return category is null || category.KurinKey != request.KurinKey
                ? ScoreErrors.NotFound<Unit>("There is no such group of events here.")
                : null;
        }

        var item = await _agenda.FindEventAsync(r.AgendaItemKey!.Value, cancellationToken);
        return item is null || item.KurinKey != request.KurinKey
            ? ScoreErrors.NotFound<Unit>("There is no such event here.")
            : null;
    }
}
