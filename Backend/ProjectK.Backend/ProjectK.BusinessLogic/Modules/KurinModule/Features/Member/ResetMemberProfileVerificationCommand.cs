using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;

public sealed record ResetMemberProfileVerificationCommand(Guid MemberKey) : IRequest<ServiceResult<MemberDto>>;

public sealed class ResetMemberProfileVerificationCommandHandler : IRequestHandler<ResetMemberProfileVerificationCommand, ServiceResult<MemberDto>>
{
    private readonly IMemberProfileVerificationService _service;

    public ResetMemberProfileVerificationCommandHandler(IMemberProfileVerificationService service)
    {
        _service = service;
    }

    public Task<ServiceResult<MemberDto>> Handle(ResetMemberProfileVerificationCommand request, CancellationToken cancellationToken)
        => _service.ResetAsync(request.MemberKey, cancellationToken);
}
