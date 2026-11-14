using MediatR;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.MemberWarning;

public sealed class GetMemberWarningsQuery : IRequest<ServiceResult<IEnumerable<MemberWarningDto>>>
{
    public GetMemberWarningsQuery(Guid memberKey)
    {
        MemberKey = memberKey;
    }

    public Guid MemberKey { get; }
}

public sealed class GetMemberWarningsQueryHandler : IRequestHandler<GetMemberWarningsQuery, ServiceResult<IEnumerable<MemberWarningDto>>>
{
    private readonly IMemberUnitOfWork _unitOfWork;

    public GetMemberWarningsQueryHandler(IMemberUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<IEnumerable<MemberWarningDto>>> Handle(GetMemberWarningsQuery request, CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return ServiceResult<IEnumerable<MemberWarningDto>>.Failure(ResultType.NotFound, MemberErrors.MemberNotFound, "No such member.");
        }

        var warnings = await _unitOfWork.MemberWarnings.GetByMemberKeyAsync(request.MemberKey, cancellationToken);

        var response = warnings.OrderByDescending(w => w.IssuedAtUtc).Select(MemberWarningMapping.ToDto).ToList();

        return new ServiceResult<IEnumerable<MemberWarningDto>>(ResultType.Success, response);
    }
}

