using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Life;

public sealed record DeleteMyLifeEntryCommand(Guid Key) : IRequest<ServiceResult<Unit>>;

public sealed class DeleteMyLifeEntryCommandHandler : IRequestHandler<DeleteMyLifeEntryCommand, ServiceResult<Unit>>
{
    private readonly LifeStoryOwner _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public DeleteMyLifeEntryCommandHandler(LifeStoryOwner owner, IMemberUnitOfWork unitOfWork)
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
