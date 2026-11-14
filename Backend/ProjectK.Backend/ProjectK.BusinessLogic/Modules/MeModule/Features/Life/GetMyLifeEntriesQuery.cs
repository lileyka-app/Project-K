using ProjectK.BusinessLogic.Modules.KurinModule;
using FluentValidation;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Life;

/// <summary>The вишколи, табори and заходи the person wrote into their life story.</summary>
public sealed record GetMyLifeEntriesQuery : IRequest<ServiceResult<IReadOnlyList<MemberLifeEntryDto>>>;

public sealed class GetMyLifeEntriesQueryHandler : IRequestHandler<GetMyLifeEntriesQuery, ServiceResult<IReadOnlyList<MemberLifeEntryDto>>>
{
    private readonly LifeStoryOwner _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public GetMyLifeEntriesQueryHandler(LifeStoryOwner owner, IMemberUnitOfWork unitOfWork)
    {
        _owner = owner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<IReadOnlyList<MemberLifeEntryDto>>> Handle(GetMyLifeEntriesQuery request, CancellationToken cancellationToken)
    {
        if (await _owner.MemberKeyAsync(cancellationToken) is not { } memberKey)
        {
            return ServiceResult<IReadOnlyList<MemberLifeEntryDto>>.Failure(ResultType.Forbidden, MemberErrors.NoOwnCard, "No person stands behind this account.");
        }

        var entries = await _unitOfWork.MemberLifeEntries.GetForMemberAsync(memberKey, cancellationToken);
        return new ServiceResult<IReadOnlyList<MemberLifeEntryDto>>(ResultType.Success, entries.Select(LifeEntries.ToDto).ToList());
    }
}
