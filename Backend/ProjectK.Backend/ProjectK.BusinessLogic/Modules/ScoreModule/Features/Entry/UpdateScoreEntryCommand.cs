using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Exceptions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Entry;

/// <summary>Changes what was given, not to whom: the target of an entry is what it is.</summary>
public sealed record UpdateScoreEntryCommand(Guid KurinKey, Guid EntryKey, UpsertScoreEntryRequest Request) : IRequest<ServiceResult<Unit>>;

public sealed class UpdateScoreEntryCommandValidator : AbstractValidator<UpdateScoreEntryCommand>
{
    public UpdateScoreEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.EntryKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertScoreEntryRequestValidator(time));
    }
}

public sealed class UpdateScoreEntryCommandHandler : IRequestHandler<UpdateScoreEntryCommand, ServiceResult<Unit>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;
    private readonly IKraiProfileProvider _profiles;

    public UpdateScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, IUnitOfWork unitOfWork, ICurrentUserContext currentUser, TimeProvider time, IKraiProfileProvider profiles)
    {
        _access = access;
        _books = books;
        _score = score;
        _unitOfWork = unitOfWork;
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
        if (await ScoreEntryWriter.CheckAsync<Unit>(_access, _unitOfWork, _profiles, book, request.Request, entry.ScoreEntryKey, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        ScoreEntryWriter.Apply(entry, request.Request, book);
        entry.UpdatedDate = now;
        _score.ScoreEntries.Update(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Entry, entry.ScoreEntryKey, ScoreTrail.Updated, ScoreEntryWriter.Snapshot(entry), _currentUser.UserId, now);

        try
        {
            await _score.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            return ScoreEntryWriter.GivenMeanwhile<Unit>();
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
