using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.DuesModule.Services;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Dues;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Features.Concession;

/// <summary>Removes a пільга given by mistake, with what it took off the quarters it covered.</summary>
public sealed record DeleteDuesConcessionGrantCommand(Guid BoxKey, Guid GrantKey) : IRequest<ServiceResult<object>>;

public sealed class DeleteDuesConcessionGrantCommandHandler : IRequestHandler<DeleteDuesConcessionGrantCommand, ServiceResult<object>>
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

    public async Task<ServiceResult<object>> Handle(DeleteDuesConcessionGrantCommand request, CancellationToken cancellationToken)
    {
        var (grant, failure) = await DuesConcessionGrants.OpenAsync(_access, _dues, _memberships, request.BoxKey, request.GrantKey, cancellationToken);
        if (grant is null)
        {
            return failure!;
        }

        _dues.DuesConcessionGrants.Delete(grant, cancellationToken);
        await _dues.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
