using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry.Update;

/// <summary>Rewrites an operation that nobody has verified yet. A verified one is locked (409).</summary>
public sealed record UpdateDuesEntryCommand(Guid GroupKey, Guid EntryKey, UpsertDuesEntryRequest Request)
    : IRequest<ServiceResult<object>>;

public sealed class UpdateDuesEntryCommandValidator : AbstractValidator<UpdateDuesEntryCommand>
{
    public UpdateDuesEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.GroupKey).NotEmpty();
        RuleFor(c => c.EntryKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertDuesEntryRequestValidator(time));
    }
}

public sealed class UpdateDuesEntryCommandHandler : IRequestHandler<UpdateDuesEntryCommand, ServiceResult<object>>
{
    private readonly GroupDuesAccess _access;
    private readonly DuesEntryWriter _writer;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public UpdateDuesEntryCommandHandler(
        GroupDuesAccess access,
        DuesEntryWriter writer,
        IDuesUnitOfWork dues,
        ICurrentUserContext currentUser,
        TimeProvider time)
    {
        _access = access;
        _writer = writer;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<object>> Handle(UpdateDuesEntryCommand request, CancellationToken cancellationToken)
    {
        var (group, failure) = await _access.OpenAsync<object>(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        var entry = await _dues.DuesEntries.GetByKeyAsync(request.EntryKey, cancellationToken);
        if (entry is null || entry.GroupKey != group.GroupKey || entry.IsDeleted)
        {
            return ServiceResult<object>.Failure(ResultType.NotFound, "EntryNotFound", "There is no such operation here.");
        }

        if (DuesEntryLock.Check(entry) is { } locked)
        {
            return locked;
        }

        if (await _writer.CheckForGroupAsync<object>(group, request.Request, cancellationToken) is { } problem)
        {
            return problem;
        }

        DuesEntryWriter.Apply(entry, request.Request);
        DuesEntryTrail.Record(entry, DuesEntryTrail.Updated, _currentUser.UserId, _time.GetUtcNow().UtcDateTime);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<object>(ResultType.Success);
    }
}
