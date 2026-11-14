using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

/// <summary>Every request still waiting for an administrator, beside the applications.</summary>
public sealed record GetPendingKurinBranchRequestsQuery : IRequest<ServiceResult<IReadOnlyList<KurinBranchRequestDto>>>;

public sealed class GetPendingKurinBranchRequestsQueryHandler
    : IRequestHandler<GetPendingKurinBranchRequestsQuery, ServiceResult<IReadOnlyList<KurinBranchRequestDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public GetPendingKurinBranchRequestsQueryHandler(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<ServiceResult<IReadOnlyList<KurinBranchRequestDto>>> Handle(GetPendingKurinBranchRequestsQuery request, CancellationToken cancellationToken)
    {
        var pending = await _unitOfWork.KurinBranchRequests.GetPendingAsync(cancellationToken);
        var responses = new List<KurinBranchRequestDto>(pending.Count);
        foreach (var open in pending)
        {
            // A handful at most, so one read each is cheaper than a contract for it.
            if (await _unitOfWork.Kurins.GetByKeyAsync(open.KurinKey, cancellationToken) is { } kurin)
            {
                responses.Add(open.ToDto(kurin, await KurinBranchRequesters.NameOfAsync(_userManager, open)));
            }
        }

        return new ServiceResult<IReadOnlyList<KurinBranchRequestDto>>(ResultType.Success, responses);
    }
}
