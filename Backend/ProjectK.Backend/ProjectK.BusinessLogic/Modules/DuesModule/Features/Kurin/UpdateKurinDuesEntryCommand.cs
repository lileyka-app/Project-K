using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.DuesModule.Requests;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Kurin;

public sealed record UpdateKurinDuesEntryCommand(Guid KurinKey, Guid EntryKey, UpsertDuesEntryRequest Request) : IRequest<ServiceResult<Unit>>;

public sealed class UpdateKurinDuesEntryCommandValidator : AbstractValidator<UpdateKurinDuesEntryCommand>
{
    public UpdateKurinDuesEntryCommandValidator(TimeProvider time)
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.EntryKey).NotEmpty();
        RuleFor(c => c.Request).NotNull().ChildRules(request => DuesEntryRules.Apply(request, time, DuesEntryRules.KurinKinds));
    }
}

public sealed class UpdateKurinDuesEntryCommandHandler : IRequestHandler<UpdateKurinDuesEntryCommand, ServiceResult<Unit>>
{
    private readonly DuesAccess _access;
    private readonly DuesEntryWriter _writer;
    private readonly IDuesUnitOfWork _dues;
    private readonly ICurrentUserContext _currentUser;
    private readonly TimeProvider _time;

    public UpdateKurinDuesEntryCommandHandler(DuesAccess access, DuesEntryWriter writer, IDuesUnitOfWork dues, ICurrentUserContext currentUser, TimeProvider time)
    {
        _access = access;
        _writer = writer;
        _dues = dues;
        _currentUser = currentUser;
        _time = time;
    }

    public async Task<ServiceResult<Unit>> Handle(UpdateKurinDuesEntryCommand request, CancellationToken cancellationToken)
    {
        if (_access.Refuse<Unit>(request.KurinKey) is { } refused)
        {
            return refused;
        }

        var entry = await _access.OwnEntryAsync(request.KurinKey, request.EntryKey, cancellationToken);
        if (entry is null)
        {
            return DuesErrors.NoEntry<Unit>();
        }

        if (entry.IsVerified)
        {
            return DuesErrors.Locked<Unit>("Звʼязковий");
        }

        if (await _writer.CheckForKurinAsync<Unit>(request.KurinKey, request.Request, cancellationToken) is { } problem)
        {
            return problem;
        }

        DuesEntryWriter.Apply(entry, request.Request);
        DuesEntryTrail.Record(entry, DuesEntryTrail.Updated, _currentUser.UserId, _time.GetUtcNow().UtcDateTime);
        await _dues.SaveChangesAsync(cancellationToken);

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
