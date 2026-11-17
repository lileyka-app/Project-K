using MediatR;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Life;

/// <summary>The вишколи, табори and заходи the person wrote into their life story.</summary>
public sealed record GetMyLifeEntriesQuery : IRequest<ServiceResult<IReadOnlyList<MemberLifeEntryDto>>>;

public sealed class GetMyLifeEntriesQueryHandler : IRequestHandler<GetMyLifeEntriesQuery, ServiceResult<IReadOnlyList<MemberLifeEntryDto>>>
{
    private readonly ICurrentMember _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public GetMyLifeEntriesQueryHandler(ICurrentMember owner, IMemberUnitOfWork unitOfWork)
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
        return new ServiceResult<IReadOnlyList<MemberLifeEntryDto>>(ResultType.Success, entries.Select(LifeMapping.ToDto).ToList());
    }
}
