using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Membership;

/// <summary>
/// Who used to belong to this kurin.
/// <para>
/// Without this the провід could remove someone and then not reach them at all: every membership
/// read filters the closed rows out, so a person who left vanished from every screen, and taking
/// them back needed the public code they themselves hold. The данні were never lost — the row
/// survives with its <c>LeftAtUtc</c>, and so do the проби and вмілості — but nobody in the
/// kurin could see them.
/// </para>
/// </summary>
public sealed record GetFormerMembersQuery(Guid KurinKey)
    : IRequest<ServiceResult<IReadOnlyCollection<FormerMember>>>;

public sealed class GetFormerMembersQueryHandler
    : IRequestHandler<GetFormerMembersQuery, ServiceResult<IReadOnlyCollection<FormerMember>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFormerMembersQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<IReadOnlyCollection<FormerMember>>> Handle(
        GetFormerMembersQuery request,
        CancellationToken cancellationToken)
    {
        if (request.KurinKey == Guid.Empty)
        {
            return ServiceResult<IReadOnlyCollection<FormerMember>>.Failure(
                ResultType.BadRequest, MembershipErrors.KurinKeyRequired, "A kurin is required.");
        }

        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        if (kurin is null)
        {
            return ServiceResult<IReadOnlyCollection<FormerMember>>.Failure(
                ResultType.NotFound, MembershipErrors.KurinNotFound, "No such kurin.");
        }

        var former = await _unitOfWork.Memberships.GetFormerInKurinAsync(request.KurinKey, cancellationToken);

        return new ServiceResult<IReadOnlyCollection<FormerMember>>(ResultType.Success, former);
    }
}
