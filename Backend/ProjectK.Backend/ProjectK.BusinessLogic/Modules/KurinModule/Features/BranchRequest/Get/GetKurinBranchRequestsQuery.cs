using MediatR;
using Microsoft.AspNetCore.Identity;
using ProjectK.BusinessLogic.Modules.KurinModule.Models;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.BranchRequest.Get;

/// <summary>The request one kurin is waiting on, if any; read by its settings.</summary>
public sealed record GetKurinBranchRequestQuery(Guid KurinKey) : IRequest<ServiceResult<KurinBranchRequestResponse?>>;

/// <summary>Every request still waiting for an administrator, beside the applications.</summary>
public sealed record GetPendingKurinBranchRequestsQuery : IRequest<ServiceResult<IReadOnlyList<KurinBranchRequestResponse>>>;

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

public sealed class GetPendingKurinBranchRequestsQueryHandler
    : IRequestHandler<GetPendingKurinBranchRequestsQuery, ServiceResult<IReadOnlyList<KurinBranchRequestResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;

    public GetPendingKurinBranchRequestsQueryHandler(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    public async Task<ServiceResult<IReadOnlyList<KurinBranchRequestResponse>>> Handle(GetPendingKurinBranchRequestsQuery request, CancellationToken cancellationToken)
    {
        var pending = await _unitOfWork.KurinBranchRequests.GetPendingAsync(cancellationToken);
        var responses = new List<KurinBranchRequestResponse>(pending.Count);
        foreach (var open in pending)
        {
            // A handful at most, so one read each is cheaper than a contract for it.
            if (await _unitOfWork.Kurins.GetByKeyAsync(open.KurinKey, cancellationToken) is { } kurin)
            {
                responses.Add(KurinBranchRequestResponse.From(open, kurin, await KurinBranchRequesters.NameOfAsync(_userManager, open)));
            }
        }

        return new ServiceResult<IReadOnlyList<KurinBranchRequestResponse>>(ResultType.Success, responses);
    }
}

internal static class KurinBranchRequesters
{
    public static async Task<string?> NameOfAsync(UserManager<AppUser> users, KurinBranchRequest request)
        => await users.FindByIdAsync(request.RequestedByUserKey.ToString()) is { } user
            ? $"{user.FirstName} {user.LastName}".Trim()
            : null;
}
