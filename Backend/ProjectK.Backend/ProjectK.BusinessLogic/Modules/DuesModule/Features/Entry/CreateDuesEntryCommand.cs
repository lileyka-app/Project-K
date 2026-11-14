using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.DuesModule.Requests;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Entry;

/// <summary>Writes a new operation into a гурток's box. Returns its key.</summary>
public sealed record CreateDuesEntryCommand(Guid GroupKey, UpsertDuesEntryRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class CreateDuesEntryCommandValidator : AbstractValidator<CreateDuesEntryCommand>
{
    public CreateDuesEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.GroupKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertDuesEntryRequestValidator(time));
    }
}

public sealed class CreateDuesEntryCommandHandler : IRequestHandler<CreateDuesEntryCommand, ServiceResult<Guid>>
{
    private readonly DuesAccess _access;
    private readonly DuesEntryWriter _writer;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public CreateDuesEntryCommandHandler(
        DuesAccess access,
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

    public async Task<ServiceResult<Guid>> Handle(CreateDuesEntryCommand request, CancellationToken cancellationToken)
    {
        var (group, failure) = await _access.OpenGroupAsync<Guid>(request.GroupKey, cancellationToken);
        if (group is null)
        {
            return failure!;
        }

        if (await _writer.CheckForGroupAsync<Guid>(group, request.Request, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new DuesEntry
        {
            KurinKey = group.KurinKey,
            GroupKey = group.GroupKey,
            CreatedByUserKey = _currentUser.UserId,
            CreatedDate = now
        };
        DuesEntryWriter.Apply(entry, request.Request);
        DuesEntryTrail.Record(entry, DuesEntryTrail.Created, _currentUser.UserId, now);

        _dues.DuesEntries.Create(entry, cancellationToken);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Guid>(ResultType.Created, entry.DuesEntryKey);
    }
}
