using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;

/// <summary>A stage is only a slice: dropping it loses no points, so it goes for good.</summary>
public sealed record DeleteScoreStageCommand(Guid KurinKey, Guid ScoreStageKey) : IRequest<ServiceResult<Unit>>;

public sealed class DeleteScoreStageCommandHandler : IRequestHandler<DeleteScoreStageCommand, ServiceResult<Unit>>
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

    public async Task<ServiceResult<Unit>> Handle(DeleteScoreStageCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<Unit>(request.KurinKey, cancellationToken) is { } refused)
        {
            return refused;
        }

        var stage = await _score.ScoreStages.GetByKeyAsync(request.ScoreStageKey, cancellationToken);
        if (stage is null || stage.KurinKey != request.KurinKey)
        {
            return ScoreErrors.NotFound<Unit>("There is no such stage.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        _score.ScoreStages.Delete(stage, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Stage, stage.ScoreStageKey, ScoreTrail.Deleted,
            new { stage.Name, stage.FromDate, stage.ToDate }, _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
