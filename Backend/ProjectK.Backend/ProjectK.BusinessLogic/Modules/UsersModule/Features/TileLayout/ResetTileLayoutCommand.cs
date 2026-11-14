using FluentValidation;
using MediatR;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout;

public record ResetTileLayoutCommand(Guid UserKey, string BoardKey) : IRequest<ServiceResult<Unit>>;

public sealed class ResetTileLayoutCommandValidator : AbstractValidator<ResetTileLayoutCommand>
{
    public ResetTileLayoutCommandValidator()
    {
        RuleFor(command => command.BoardKey)
            .Must(TileBoardKeys.All.Contains)
            .WithMessage("Unknown board key.");
    }
}

public class ResetTileLayoutCommandHandler : IRequestHandler<ResetTileLayoutCommand, ServiceResult<Unit>>
{
    private readonly IUnitOfWork _unitOfWork;

    public ResetTileLayoutCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<Unit>> Handle(ResetTileLayoutCommand request, CancellationToken cancellationToken)
    {
        // Board key validation lives in ResetTileLayoutCommandValidator (runs in the pipeline).
        var existing = await _unitOfWork.UserTileLayouts.GetByBoardAsync(request.UserKey, request.BoardKey, cancellationToken);
        if (existing != null)
        {
            _unitOfWork.UserTileLayouts.Delete(existing, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ServiceResult<Unit>(ResultType.Success, Unit.Value);
    }
}
