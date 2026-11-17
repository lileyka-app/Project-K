using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Dtos.MemberModule;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Level;

/// <summary>
/// The Звʼязковий confirms a senior ступінь the person entered for themselves; from then on it counts.
/// Who may is <see cref="PlastLevelConfirmationService"/>'s to say.
/// </summary>
public sealed record ConfirmMemberLevelCommand(Guid MemberKey, Guid PlastLevelHistoryKey) : IRequest<ServiceResult<PlastLevelHistoryDto>>;

public sealed class ConfirmMemberLevelCommandHandler : IRequestHandler<ConfirmMemberLevelCommand, ServiceResult<PlastLevelHistoryDto>>
{
    private readonly PlastLevelConfirmationService _confirmation;

    public ConfirmMemberLevelCommandHandler(PlastLevelConfirmationService confirmation)
    {
        _confirmation = confirmation;
    }

    public Task<ServiceResult<PlastLevelHistoryDto>> Handle(ConfirmMemberLevelCommand request, CancellationToken cancellationToken)
        => _confirmation.ConfirmAsync(request.MemberKey, request.PlastLevelHistoryKey, cancellationToken);
}
