using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.IdentityModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

/// <summary>Every kurin's request, waiting and settled, newest first, for the administrators' inbox beside the applications.</summary>
public sealed record GetKurinBranchRequestsQuery : IRequest<ServiceResult<IReadOnlyList<KurinBranchRequestDto>>>;

public sealed class GetKurinBranchRequestsQueryHandler
    : IRequestHandler<GetKurinBranchRequestsQuery, ServiceResult<IReadOnlyList<KurinBranchRequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public GetKurinBranchRequestsQueryHandler(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<ServiceResult<IReadOnlyList<KurinBranchRequestDto>>> Handle(GetKurinBranchRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = await _unitOfWork.KurinBranchRequests.GetAllAsync(cancellationToken);
        var responses = new List<KurinBranchRequestDto>(requests.Count);
        foreach (var open in requests)
        {
            // A handful at most, so one read each is cheaper than a contract for it.
            if (await _unitOfWork.Kurins.GetByKeyAsync(open.KurinKey, cancellationToken) is { } kurin)
            {
                responses.Add(open.ToDto(kurin, await KurinBranchRequesterNames.NameOfAsync(_userManager, open)));
            }
        }

        return new ServiceResult<IReadOnlyList<KurinBranchRequestDto>>(ResultType.Success, responses);
    }
}
