using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

/// <summary>The request one kurin is waiting on, if any; read by its settings.</summary>
public sealed record GetKurinBranchRequestQuery(Guid KurinKey) : IRequest<ServiceResult<KurinBranchRequestResponse?>>;

public sealed class GetKurinBranchRequestQueryHandler
    : IRequestHandler<GetKurinBranchRequestQuery, ServiceResult<KurinBranchRequestResponse?>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public GetKurinBranchRequestQueryHandler(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<ServiceResult<KurinBranchRequestResponse?>> Handle(GetKurinBranchRequestQuery request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<KurinBranchRequestResponse?>.Failure(ResultType.NotFound, "KurinNotFound", "Kurin not found.");
        }

        var open = await _unitOfWork.KurinBranchRequests.GetPendingForKurinAsync(request.KurinKey, cancellationToken);
        return new ServiceResult<KurinBranchRequestResponse?>(
            ResultType.Success,
            open is null ? null : KurinBranchRequestResponse.From(open, kurin, await KurinBranchRequesters.NameOfAsync(_userManager, open)));
    }
}
