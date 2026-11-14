using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Enums.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;

public sealed record UpdatePrivateScoreEntryCommand(Guid KurinKey, Guid EntryKey, UpsertPrivateScoreEntryRequest Request) : IRequest<ServiceResult<Unit>>;

public sealed class UpdatePrivateScoreEntryCommandValidator : AbstractValidator<UpdatePrivateScoreEntryCommand>
{
    public UpdatePrivateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.EntryKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertPrivateScoreEntryRequestValidator(time));
    }
}

public sealed class UpdatePrivateScoreEntryCommandHandler : IRequestHandler<UpdatePrivateScoreEntryCommand, ServiceResult<Unit>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public UpdatePrivateScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _books = books;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(UpdatePrivateScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefusePrivateAsync<Unit>(request.KurinKey, ResourceAction.Update, cancellationToken) is { } refused)
        {
            return refused;
        }

        var entry = await _score.PrivateScoreEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.KurinKey != request.KurinKey || entry.IsDeleted)
        {
            return ScoreErrors.NotFound<Unit>("There is no such entry here.");
        }

        if (entry.MembershipKey != request.Request.MembershipKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, ScoreErrors.EntryTargetFixed, "An entry stays with whom it was written about; write a new one instead.");
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (await PrivateScoreEntryRules.CheckAsync<Unit>(_score, book, request.Request, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        PrivateScoreEntryRules.Apply(entry, request.Request);
        entry.UpdatedDate = now;
        _score.PrivateScoreEntries.Update(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.PrivateEntry, entry.PrivateScoreEntryKey, ScoreTrail.Updated, PrivateScoreEntryRules.Snapshot(entry), _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
