using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.ScoreModule.Models;
using ProjectK.BusinessLogic.Modules.ScoreModule.Services;
using ProjectK.Common.Entities.ScoreModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.ScoreModule;
using ProjectK.Common.Models.Dtos.ScoreModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ScoreModule.Features.Private;

public sealed record DeletePrivateScoreEntryCommand(Guid KurinKey, Guid EntryKey) : IRequest<ServiceResult<Unit>>;

public sealed class DeletePrivateScoreEntryCommandHandler : IRequestHandler<DeletePrivateScoreEntryCommand, ServiceResult<Unit>>
{
    private readonly PrivateScoreAccess _access;
    private readonly IScoreUnitOfWork _score;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public DeletePrivateScoreEntryCommandHandler(PrivateScoreAccess access, IScoreUnitOfWork score, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _score = score;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(DeletePrivateScoreEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _access.RefuseAsync<Unit>(request.KurinKey, ResourceAction.Delete, cancellationToken) is { } refused)
        {
            return refused;
        }

        var entry = await _score.PrivateScoreEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.KurinKey != request.KurinKey || entry.IsDeleted)
        {
            return ScoreErrors.NotFound<Unit>("There is no such entry here.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        entry.DeletedAtUtc = now;
        entry.DeletedByUserKey = _currentUser.UserId;
        entry.UpdatedDate = now;
        _score.PrivateScoreEntries.Update(entry, cancellationToken);
        ScoreTrail.Record(_score, request.KurinKey, ScoreTrail.PrivateEntry, entry.PrivateScoreEntryKey, ScoreTrail.Deleted, PrivateScoreEntryWriter.Snapshot(entry), _currentUser.UserId, now);

        await _score.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
