using MediatR;
using ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Models;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuditModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.ProbesAndBadgesModule.Features.Badge.Get;

public sealed class GetBadgeProgressesQuery : IRequest<ServiceResult<IEnumerable<BadgeProgressResponse>>>
{
    public GetBadgeProgressesQuery(Guid memberKey)
    {
        MemberKey = memberKey;
    }

    public Guid MemberKey { get; }
}

public sealed class GetBadgeProgressesQueryHandler : IRequestHandler<GetBadgeProgressesQuery, ServiceResult<IEnumerable<BadgeProgressResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberDirectory _members;
    private readonly IKurinJournalReader _journal;

    public GetBadgeProgressesQueryHandler(IUnitOfWork unitOfWork, IMemberDirectory members, IKurinJournalReader journal)
    {
        _unitOfWork = unitOfWork;
        _members = members;
        _journal = journal;
    }

    public async Task<ServiceResult<IEnumerable<BadgeProgressResponse>>> Handle(GetBadgeProgressesQuery request, CancellationToken cancellationToken)
    {
        if (!await _members.ExistsAsync(request.MemberKey, cancellationToken))
        {
            return new ServiceResult<IEnumerable<BadgeProgressResponse>>(ResultType.NotFound);
        }

        var progresses = (await _unitOfWork.BadgeProgresses.GetByMemberKeyAsync(request.MemberKey, cancellationToken)).ToList();
        var journal = (await _journal.GetForSubjectsAsync(progresses.Select(x => x.BadgeProgressKey).ToList(), cancellationToken))
            .ToLookup(entry => entry.SubjectKey);
        var response = progresses
            .OrderByDescending(x => x.UpdatedDate)
            .Select(x => BadgeProgressResponse.FromEntity(x, journal[x.BadgeProgressKey]))
            .ToList();

        return new ServiceResult<IEnumerable<BadgeProgressResponse>>(ResultType.Success, response);
    }
}
