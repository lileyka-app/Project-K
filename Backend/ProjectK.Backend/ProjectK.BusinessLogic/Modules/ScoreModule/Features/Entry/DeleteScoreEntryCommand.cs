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

public sealed record DeleteScoreEntryCommand(Guid KurinKey, Guid EntryKey) : IRequest<ServiceResult<object>>;

public sealed class DeleteScoreEntryCommandHandler : IRequestHandler<DeleteScoreEntryCommand, ServiceResult<object>>
{
    private readonly ScoreAccess _access;
    private readonly ScoreBookReader _books;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public DeleteScoreEntryCommandHandler(ScoreAccess access, ScoreBookReader books, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _books = books;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(DeleteScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<object>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var entry = await _score.ScoreEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.KurinKey != request.KurinKey || entry.IsDeleted)
        {
            return ScoreAccess.NotFound<object>("There is no such entry here.");
        }

        var book = await _books.OpenAsync(request.KurinKey, cancellationToken);
        var groupKey = entry.GroupKey ?? (book.Memberships.TryGetValue(entry.MembershipKey!.Value, out var m) && m.LeftAtUtc is null ? m.GroupKey : null);
        if (!await _access.MayScoreAsync(request.KurinKey, groupKey, ResourceAction.Delete, cancellationToken))
        {
            return ScoreAccess.Forbidden<object>();
        }

        var now = _time.GetUtcNow().UtcDateTime;
        entry.DeletedAtUtc = now;
        entry.DeletedByUserKey = _currentUser.UserId;
        entry.UpdatedDate = now;
        _score.ScoreEntries.Update(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.Entry, entry.ScoreEntryKey, ScoreTrail.Deleted, ScoreEntryWriter.Snapshot(entry), _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
