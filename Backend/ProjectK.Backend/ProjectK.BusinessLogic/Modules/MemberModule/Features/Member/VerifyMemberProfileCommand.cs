using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Member;

public sealed record VerifyMemberProfileCommand(Guid MemberKey, string? Note = null) : IRequest<ServiceResult<MemberDto>>;

public sealed class VerifyMemberProfileCommandHandler : IRequestHandler<VerifyMemberProfileCommand, ServiceResult<MemberDto>>
{
    private readonly IMemberProfileVerificationService _service;

    public VerifyMemberProfileCommandHandler(IMemberProfileVerificationService service)
    {
        _service = service;
    }

    public Task<ServiceResult<MemberDto>> Handle(VerifyMemberProfileCommand request, CancellationToken cancellationToken)
        => _service.VerifyAsync(request.MemberKey, request.Note, cancellationToken);
}
