using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

/// <summary>The request one kurin is waiting on, if any; read by its settings.</summary>
public sealed record GetKurinBranchRequestQuery(Guid KurinKey) : IRequest<ServiceResult<KurinBranchRequestDto?>>;

public sealed class GetKurinBranchRequestQueryHandler
    : IRequestHandler<GetKurinBranchRequestQuery, ServiceResult<KurinBranchRequestDto?>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public GetKurinBranchRequestQueryHandler(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<ServiceResult<KurinBranchRequestDto?>> Handle(GetKurinBranchRequestQuery request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<KurinBranchRequestDto?>.Failure(ResultType.NotFound, MemberErrors.KurinNotFound, "Kurin not found.");
        }

        var open = await _unitOfWork.KurinBranchRequests.GetPendingForKurinAsync(request.KurinKey, cancellationToken);
        return new ServiceResult<KurinBranchRequestDto?>(
            ResultType.Success,
            open is null ? null : open.ToDto(kurin, await KurinBranchRequesters.NameOfAsync(_userManager, open)));
    }
}
