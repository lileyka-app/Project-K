using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;

public sealed record SetScoreAlgorithmCommand(Guid KurinKey, SetScoreAlgorithmRequest Request) : IRequest<ServiceResult<Unit>>;

public sealed class SetScoreAlgorithmCommandValidator : AbstractValidator<SetScoreAlgorithmCommand>
{
    public SetScoreAlgorithmCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request.Algorithm).IsInEnum();
    }
}

public sealed class SetScoreAlgorithmCommandHandler : IRequestHandler<SetScoreAlgorithmCommand, ServiceResult<Unit>>
{
    private readonly ScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public SetScoreAlgorithmCommandHandler(ScoreAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(SetScoreAlgorithmCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseSettingsAsync<Unit>(request.KurinKey, cancellationToken) is { } refused)
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
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
