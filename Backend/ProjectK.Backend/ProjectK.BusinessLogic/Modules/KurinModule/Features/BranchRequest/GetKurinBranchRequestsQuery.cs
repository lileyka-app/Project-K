using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.IdentityModule;
using KurinEntity = ProjectK.Common.Entities.KurinModule.Kurin;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest;

/// <summary>The administrators' inbox beside the applications: every request still waiting and the latest settled ones, newest first.</summary>
public sealed record GetKurinBranchRequestsQuery : IRequest<ServiceResult<IReadOnlyList<KurinBranchRequestDto>>>;

public sealed class GetKurinBranchRequestsQueryHandler
    : IRequestHandler<GetKurinBranchRequestsQuery, ServiceResult<IReadOnlyList<KurinBranchRequestDto>>>
{
    /// <summary>How many settled requests the inbox keeps showing under the waiting ones.</summary>
    public const int SettledShown = 50;

    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public GetKurinBranchRequestsQueryHandler(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<ServiceResult<IReadOnlyList<KurinBranchRequestDto>>> Handle(GetKurinBranchRequestsQuery request, CancellationToken cancellationToken)
    {
        var requests = await _unitOfWork.KurinBranchRequests.GetInboxAsync(SettledShown, cancellationToken);
        var responses = new List<KurinBranchRequestDto>(requests.Count);

        // A kurin asks again and again, by the same person: each is read once, not once a row.
        var kurins = new Dictionary<Guid, KurinEntity?>();
        var names = new Dictionary<Guid, string?>();
        foreach (var asked in requests)
        {
            if (!kurins.TryGetValue(asked.KurinKey, out var kurin))
            {
                kurin = kurins[asked.KurinKey] = await _unitOfWork.Kurins.GetByKeyAsync(asked.KurinKey, cancellationToken);
            }

            if (kurin is null)
            {
                continue;
            }

            if (!names.TryGetValue(asked.RequestedByUserKey, out var name))
            {
                name = names[asked.RequestedByUserKey] = await KurinBranchRequesterNames.NameOfAsync(_userManager, asked);
            }

            responses.Add(asked.ToDto(kurin, name));
        }

        return new ServiceResult<IReadOnlyList<KurinBranchRequestDto>>(ResultType.Success, responses);
    }
}
