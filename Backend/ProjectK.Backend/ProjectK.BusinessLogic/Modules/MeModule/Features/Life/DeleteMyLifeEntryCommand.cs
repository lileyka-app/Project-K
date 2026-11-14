using FluentValidation;
using MediatR;
using ProjectK.Common.Entities.KurinModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.InfrastructureModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Dtos.KurinModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.MeModule.Features.Life;

public sealed record DeleteMyLifeEntryCommand(Guid Key) : IRequest<ServiceResult<object>>;

public sealed class DeleteMyLifeEntryCommandHandler : IRequestHandler<DeleteMyLifeEntryCommand, ServiceResult<object>>
{
    private readonly LifeStoryOwner _owner;
    private readonly IMemberUnitOfWork _unitOfWork;

    public DeleteMyLifeEntryCommandHandler(LifeStoryOwner owner, IMemberUnitOfWork unitOfWork)
    {
        _owner = owner;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<object>> Handle(DeleteMyLifeEntryCommand request, CancellationToken cancellationToken)
    {
        if (await _owner.MemberKeyAsync(cancellationToken) is not { } memberKey)
        {
            return new ServiceResult<object>(ResultType.Forbidden);
        }

        if (await _unitOfWork.MemberLifeEntries.GetOwnAsync(memberKey, request.Key, cancellationToken) is not { } entry)
        {
            return new ServiceResult<object>(ResultType.NotFound);
        }

        _unitOfWork.MemberLifeEntries.Delete(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ServiceResult<object>(ResultType.Success);
    }
}
