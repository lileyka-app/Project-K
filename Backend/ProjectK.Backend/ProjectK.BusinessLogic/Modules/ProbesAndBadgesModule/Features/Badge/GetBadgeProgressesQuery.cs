using MediatR;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.ProbesAndBadgesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge;

public sealed class GetBadgeProgressesQuery : IRequest<ServiceResult<IEnumerable<BadgeProgressDto>>>
{
    public GetBadgeProgressesQuery(Guid memberKey)
    {
        MemberKey = memberKey;
    }

    public Guid MemberKey { get; }
}

public sealed class GetBadgeProgressesQueryHandler : IRequestHandler<GetBadgeProgressesQuery, ServiceResult<IEnumerable<BadgeProgressDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IKurinJournalRepository _journal;

    public GetBadgeProgressesQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IAuditUnitOfWork audit)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _journal = audit.KurinJournal;
    }

    public async Task<ServiceResult<IEnumerable<BadgeProgressDto>>> Handle(GetBadgeProgressesQuery request, CancellationToken cancellationToken)
    {
        if (!await _members.ExistsAsync(request.MemberKey, cancellationToken))
        {
            return ServiceResult<IEnumerable<BadgeProgressDto>>.Failure(ResultType.NotFound, ProbesAndBadgesErrors.MemberNotFound, "Member not found.");
        }

        var progresses = (await _unitOfWork.BadgeProgresses.GetByMemberKeyAsync(request.MemberKey, cancellationToken)).ToList();
        var journal = (await _journal.GetForSubjectsAsync(progresses.Select(x => x.BadgeProgressKey).ToList(), cancellationToken))
            .ToLookup(entry => entry.SubjectKey);
        var response = progresses
            .OrderByDescending(x => x.UpdatedDate)
            .Select(x => BadgeMapping.ToDto(x, journal[x.BadgeProgressKey]))
            .ToList();

        return new ServiceResult<IEnumerable<BadgeProgressDto>>(ResultType.Success, response);
    }
}
