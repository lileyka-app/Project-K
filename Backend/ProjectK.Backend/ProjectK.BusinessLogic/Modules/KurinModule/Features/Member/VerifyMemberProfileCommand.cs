using MediatR;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Extensions;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.AuthModule;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Events;
using ProjectK.Common.Models.Records;
using MemberEntity = ProjectK.Common.Entities.KurinModule.Member;
using MembershipEntity = ProjectK.Common.Entities.KurinModule.Membership;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Member;

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
