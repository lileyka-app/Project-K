using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Settings;

/// <summary>A new position on the list, or a change to one. A change does not touch what was already given by it.</summary>
public sealed record UpsertScoreItemCommand(Guid KurinKey, Guid? ScoreItemKey, UpsertScoreItemRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class UpsertScoreItemCommandValidator : AbstractValidator<UpsertScoreItemCommand>
{
    public UpsertScoreItemCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().ChildRules(request =>
        {
            request.RuleFor(r => r.Name).NotEmpty().MaximumLength(100);
            request.RuleFor(r => r.Points).NotEqual(0).InclusiveBetween(-1000, 1000);
        });
    }
}

public sealed class UpsertScoreItemCommandHandler : IRequestHandler<UpsertScoreItemCommand, ServiceResult<Guid>>
{
    private readonly ScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public UpsertScoreItemCommandHandler(ScoreAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Guid>> Handle(UpsertScoreItemCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseSettingsAsync<Guid>(request.KurinKey, cancellationToken) is { } refused)
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
                return ScoreErrors.NotFound<Guid>("There is no such position on the list.");
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
