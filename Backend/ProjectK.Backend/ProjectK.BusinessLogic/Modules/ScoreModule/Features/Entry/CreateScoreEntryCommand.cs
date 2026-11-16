using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Entry;

public sealed record CreateScoreEntryCommand(Guid KurinKey, UpsertScoreEntryRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class CreateScoreEntryCommandValidator : AbstractValidator<CreateScoreEntryCommand>
{
    public CreateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().ChildRules(request => ScoreEntryRules.AddShapeRules(request, time));
    }
}

public sealed class CreateScoreEntryCommandHandler : IRequestHandler<CreateScoreEntryCommand, ServiceResult<Guid>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileDirectory _profiles;

    public CreateScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, IUnitOfWork unitOfWork, ICurrentUserContext currentUser, TimeProvider time, IKraiProfileDirectory profiles)
    {
        _access = access;
        _books = books;
        _score = score;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
    }

    public async Task<ServiceResult<Guid>> Handle(CreateScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Guid>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (await ScoreEntryRules.CheckAsync<Guid>(_access, _unitOfWork, _profiles, book, request.Request, null, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new ScoreEntry
        {
            KurinKey = request.KurinKey,
            MembershipKey = request.Request.MembershipKey,
            GroupKey = request.Request.GroupKey,
            CreatedByUserKey = _currentUser.UserId,
            CreatedDate = now,
            UpdatedDate = now
        };
        ScoreEntryRules.Apply(entry, request.Request, book);
        _score.ScoreEntries.Create(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Entry, entry.ScoreEntryKey, ScoreTrail.Created, ScoreEntryRules.Snapshot(entry), _currentUser.UserId, now);

        try
        {
            await _score.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            return ScoreEntryRules.GivenMeanwhile<Guid>();
        }

        return new ServiceResult<Guid>(ResultType.Created, entry.ScoreEntryKey);
    }
}
