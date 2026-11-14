using AutoMapper;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dtos.KurinModule;
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
    private readonly IMapper _mapper;

    public GetMemberWarningsQueryHandler(IMemberUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ServiceResult<IEnumerable<MemberWarningDto>>> Handle(GetMemberWarningsQuery request, CancellationToken cancellationToken)
    {
        var member = await _unitOfWork.Members.GetByKeyAsync(request.MemberKey, cancellationToken);
        if (member is null)
        {
            return new ServiceResult<IEnumerable<MemberWarningDto>>(ResultType.NotFound);
        }

        var warnings = await _unitOfWork.MemberWarnings.GetByMemberKeyAsync(request.MemberKey, cancellationToken);

        var response = _mapper.Map<IEnumerable<MemberWarningDto>>(
            warnings.OrderByDescending(w => w.IssuedAtUtc)
        );

        return new ServiceResult<IEnumerable<MemberWarningDto>>(ResultType.Success, response);
    }
}

