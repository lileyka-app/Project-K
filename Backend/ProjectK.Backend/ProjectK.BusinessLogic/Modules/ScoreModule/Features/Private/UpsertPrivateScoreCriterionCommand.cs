using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;

public sealed record UpsertPrivateScoreCriterionCommand(Guid KurinKey, Guid? CriterionKey, UpsertPrivateScoreCriterionRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class UpsertPrivateScoreCriterionCommandValidator : AbstractValidator<UpsertPrivateScoreCriterionCommand>
{
    public UpsertPrivateScoreCriterionCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().ChildRules(request =>
        {
            request.RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
        });
    }
}

public sealed class UpsertPrivateScoreCriterionCommandHandler : IRequestHandler<UpsertPrivateScoreCriterionCommand, ServiceResult<Guid>>
{
    private readonly ScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public UpsertPrivateScoreCriterionCommandHandler(ScoreAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Guid>> Handle(UpsertPrivateScoreCriterionCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefusePrivateAsync<Guid>(request.KurinKey, ResourceAction.Update, cancellationToken) is { } refused)
        {
            return refused;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        PrivateScoreCriterion criterion;
        if (request.CriterionKey is { } key)
        {
            var found = await _score.PrivateScoreCriteria.GetByKeyAsync(key, cancellationToken);
            if (found is null || found.KurinKey != request.KurinKey)
            {
                return ScoreErrors.NotFound<Guid>("There is no such criterion.");
            }

            criterion = found;
        }
        else
        {
            criterion = new PrivateScoreCriterion { KurinKey = request.KurinKey, CreatedDate = now };
            _score.PrivateScoreCriteria.Create(criterion, cancellationToken);
        }

        criterion.Name = request.Request.Name.Trim();
        criterion.IsArchived = request.Request.IsArchived;
        criterion.UpdatedDate = now;
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.PrivateCriterion, criterion.PrivateScoreCriterionKey,
            request.CriterionKey is null ? ScoreTrail.Created : ScoreTrail.Updated, new { criterion.Name, criterion.IsArchived }, _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Guid>(request.CriterionKey is null ? ResultType.Created : ResultType.Success, criterion.PrivateScoreCriterionKey);
    }
}
