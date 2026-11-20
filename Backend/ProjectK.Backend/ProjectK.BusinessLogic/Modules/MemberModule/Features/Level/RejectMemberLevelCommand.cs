using MediatR;
using ProjectK.BusinessLogic.Modules.MemberModule.Services;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Level;

/// <summary>
/// The Звʼязковий rejects a senior ступінь the person entered for themselves, and it leaves the card.
/// Who may is <see cref="PlastLevelConfirmationService"/>'s to say.
/// </summary>
public sealed record RejectMemberLevelCommand(Guid MemberKey, Guid PlastLevelHistoryKey) : IRequest<ServiceResult<Unit>>;

public sealed class RejectMemberLevelCommandHandler : IRequestHandler<RejectMemberLevelCommand, ServiceResult<Unit>>
{
    private readonly PlastLevelConfirmationService _confirmation;

    public RejectMemberLevelCommandHandler(PlastLevelConfirmationService confirmation)
    {
        _confirmation = confirmation;
    }

    public Task<ServiceResult<Unit>> Handle(RejectMemberLevelCommand request, CancellationToken cancellationToken)
        => _confirmation.RejectAsync(request.MemberKey, request.PlastLevelHistoryKey, cancellationToken);
}
