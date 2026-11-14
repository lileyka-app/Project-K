using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Contracts.DuesModule;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Concession;

/// <summary>Removes a пільга given by mistake, with what it took off the quarters it covered.</summary>
public sealed record DeleteDuesConcessionGrantCommand(Guid BoxKey, Guid GrantKey) : IRequest<ServiceResult<Unit>>;

public sealed class DeleteDuesConcessionGrantCommandHandler : IRequestHandler<DeleteDuesConcessionGrantCommand, ServiceResult<Unit>>
{
    private readonly GroupDuesAccess _access;
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;

    public DeleteDuesConcessionGrantCommandHandler(GroupDuesAccess access, IDuesUnitOfWork dues, IMembershipDirectory memberships)
    {
        _access = access;
        _dues = dues;
        _memberships = memberships;
    }

    public async Task<ServiceResult<Unit>> Handle(DeleteDuesConcessionGrantCommand request, CancellationToken cancellationToken)
    {
        var (grant, failure) = await DuesConcessionGrants.OpenAsync(_access, _dues, _memberships, request.BoxKey, request.GrantKey, cancellationToken);
        if (grant is null)
        {
            return failure!;
        }

        _dues.DuesConcessionGrants.Delete(grant, cancellationToken);
        await _dues.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
