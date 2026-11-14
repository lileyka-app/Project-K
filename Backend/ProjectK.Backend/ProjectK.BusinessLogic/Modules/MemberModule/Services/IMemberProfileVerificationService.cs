using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Services;

public interface IMemberProfileVerificationService
{
    Task<ServiceResult<MemberDto>> VerifyAsync(Guid memberKey, string? note, CancellationToken cancellationToken);

    Task<ServiceResult<MemberDto>> ResetAsync(Guid memberKey, CancellationToken cancellationToken);
}
