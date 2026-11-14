using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.InfrastructureModule;
using ProjectK.Common.Dtos.DuesModule.Requests;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin;

public sealed record CreateKurinDuesEntryCommand(Guid KurinKey, UpsertDuesEntryRequest Request) : IRequest<ServiceResult<Guid>>;

public sealed class CreateKurinDuesEntryCommandValidator : AbstractValidator<CreateKurinDuesEntryCommand>
{
    public CreateKurinDuesEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().SetValidator(new UpsertKurinDuesEntryRequestValidator(time));
    }
}

public sealed class CreateKurinDuesEntryCommandHandler : IRequestHandler<CreateKurinDuesEntryCommand, ServiceResult<Guid>>
{
    private readonly DuesAccess _access;
    private readonly DuesEntryWriter _writer;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public CreateKurinDuesEntryCommandHandler(DuesAccess access, DuesEntryWriter writer, IDuesUnitOfWork dues, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _writer = writer;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Guid>> Handle(CreateKurinDuesEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Guid>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        if (await _writer.CheckForKurinAsync<Guid>(request.KurinKey, request.Request, cancellationToken) is { } problem)
        {
            return problem;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var entry = new DuesEntry { KurinKey = request.KurinKey, GroupKey = null, CreatedByUserKey = _currentUser.UserId, CreatedDate = now };
        DuesEntryWriter.Apply(entry, request.Request);
        DuesEntryTrail.Record(entry, DuesEntryTrail.Created, _currentUser.UserId, now);

        _dues.DuesEntries.Create(entry, cancellationToken);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Guid>(ResultType.Created, entry.DuesEntryKey);
    }
}
