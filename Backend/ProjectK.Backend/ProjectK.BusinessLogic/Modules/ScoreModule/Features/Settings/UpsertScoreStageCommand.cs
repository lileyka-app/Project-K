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

public sealed record UpsertScoreStageCommand(Guid KurinKey, Guid? ScoreStageKey, UpsertScoreStageRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class UpsertScoreStageCommandValidator : AbstractValidator<UpsertScoreStageCommand>
{
    public UpsertScoreStageCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertScoreStageRequestValidator());
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
