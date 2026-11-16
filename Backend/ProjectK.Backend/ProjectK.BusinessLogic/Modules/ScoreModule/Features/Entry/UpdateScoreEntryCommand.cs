using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.ScoreModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ScoreModule.Requests;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Entry;

/// <summary>Changes what was given, not to whom: the target of an entry is what it is.</summary>
public sealed record UpdateScoreEntryCommand(Guid KurinKey, Guid EntryKey, UpsertScoreEntryRequest Request) : IRequest<ServiceResult<Unit>>;

public sealed class UpdateScoreEntryCommandValidator : AbstractValidator<UpdateScoreEntryCommand>
{
    public UpdateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.EntryKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().ChildRules(request => ScoreEntryRules.AddShapeRules(request, time));
    }
}

public sealed class UpdateScoreEntryCommandHandler : IRequestHandler<UpdateScoreEntryCommand, ServiceResult<Unit>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly IAgendaDirectory _agenda;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileDirectory _profiles;

    public UpdateScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, IAgendaDirectory agenda, ICurrentUserContext currentUser, TimeProvider time, IKraiProfileDirectory profiles)
    {
        _access = access;
        _books = books;
        _score = score;
        _agenda = agenda;
        _currentUser = currentUser;
        _time = time;
        _profiles = profiles;
    }

    public async Task<ServiceResult<Unit>> Handle(UpdateScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Unit>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var entry = await _score.ScoreEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.KurinKey != request.KurinKey || entry.IsDeleted)
        {
            return ScoreErrors.NotFound<Unit>("There is no such entry here.");
        }

        if (entry.MembershipKey != request.Request.MembershipKey || entry.GroupKey != request.Request.GroupKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.BadRequest, ScoreErrors.EntryTargetFixed, "An entry stays with whom it was given to; give a new one instead.");
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        if (await ScoreEntryRules.CheckAsync<Unit>(_access, _agenda, _profiles, book, request.Request, entry.ScoreEntryKey, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        ScoreEntryRules.Apply(entry, request.Request, book);
        entry.UpdatedDate = now;
        _score.ScoreEntries.Update(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Entry, entry.ScoreEntryKey, ScoreTrail.Updated, ScoreEntryRules.Snapshot(entry), _currentUser.UserId, now);

        try
        {
            await _score.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            return ScoreEntryRules.GivenMeanwhile<Unit>();
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
