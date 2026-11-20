using MediatR;
using ProjectK.Common.Contracts.MemberModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.MemberModule.Features.Life;

public sealed record DeleteMyLifeEntryCommand(Guid Key) : IRequest<ServiceResult<Unit>>;

public sealed class DeleteMyLifeEntryCommandHandler : IRequestHandler<DeleteMyLifeEntryCommand, ServiceResult<Unit>>
{
    private readonly ICurrentMember _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public DeleteMyLifeEntryCommandHandler(ICurrentMember owner, IMemberUnitOfWork unitOfWork)
    {
        _owner = owner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<Unit>> Handle(DeleteMyLifeEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _owner.MemberKeyAsync(cancellationToken) is not { } memberKey)
        {
            return ServiceResult<Unit>.Failure(ResultType.Forbidden, MemberErrors.NoOwnCard, "No person stands behind this account.");
        }

        if (await _unitOfWork.MemberLifeEntries.GetOwnAsync(memberKey, request.Key, cancellationToken) is not { } entry)
        {
            return ServiceResult<Unit>.Failure(ResultType.NotFound, MemberErrors.LifeEntryNotFound, "No such entry in your life story.");
        }

        _unitOfWork.MemberLifeEntries.Delete(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
