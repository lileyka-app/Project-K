using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;

public sealed record CreatePrivateScoreEntryCommand(Guid KurinKey, UpsertPrivateScoreEntryRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class CreatePrivateScoreEntryCommandValidator : AbstractValidator<CreatePrivateScoreEntryCommand>
{
    public CreatePrivateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().ChildRules(request => PrivateScoreEntryRules.AddShapeRules(request, time));
    }
}

public sealed class CreatePrivateScoreEntryCommandHandler : IRequestHandler<CreatePrivateScoreEntryCommand, ServiceResult<Guid>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public CreatePrivateScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _books = books;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Guid>> Handle(CreatePrivateScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefusePrivateAsync<Guid>(request.KurinKey, ResourceAction.Create, cancellationToken) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (await PrivateScoreEntryRules.CheckAsync<Guid>(_score, book, request.Request, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new PrivateScoreEntry { KurinKey = request.KurinKey, MembershipKey = request.Request.MembershipKey, CreatedByUserKey = _currentUser.UserId, CreatedDate = now, UpdatedDate = now };
        PrivateScoreEntryRules.Apply(entry, request.Request);
        _score.PrivateScoreEntries.Create(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.PrivateEntry, entry.PrivateScoreEntryKey, ScoreTrail.Created, PrivateScoreEntryRules.Snapshot(entry), _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Guid>(ResultType.Created, entry.PrivateScoreEntryKey);
    }
}
