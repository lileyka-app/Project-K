using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.UsersModule;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.UsersModule;

namespace ProjectK.BusinessLogic.Modules.UsersModule.Features.TileLayout;

public record SaveTileLayoutCommand(
    Guid UserKey,
    string BoardKey,
    IReadOnlyList<string> TileKeys,
    IReadOnlyList<string> HiddenTileKeys,
    int SchemaVersion) : IRequest<ServiceResult<TileLayoutDto>>;

public sealed partial class SaveTileLayoutCommandValidator : AbstractValidator<SaveTileLayoutCommand>
{
    private const int MaxTileCount = 40;
    private const int MaxTileKeyLength = 64;

    public SaveTileLayoutCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.BoardKey)
            .Must(TileBoardKeys.All.Contains)
            .WithMessage("Unknown board key.");

        RuleFor(command => command.TileKeys)
            .Must(keys => (keys?.Count ?? 0) <= MaxTileCount)
                .WithMessage($"A layout cannot contain more than {MaxTileCount} tiles.")
            .Must(AllKeysWellFormed)
                .WithMessage("Tile keys must be non-empty, at most 64 lowercase alphanumeric/hyphen characters.")
            .Must(AllKeysUnique)
                .WithMessage("Tile keys must be unique.");

        RuleFor(command => command.HiddenTileKeys)
            .Must(keys => (keys?.Count ?? 0) <= MaxTileCount)
                .WithMessage($"A layout cannot hide more than {MaxTileCount} tiles.")
            .Must(AllKeysWellFormed)
                .WithMessage("Hidden tile keys must be non-empty, at most 64 lowercase alphanumeric/hyphen characters.")
            .Must(AllKeysUnique)
                .WithMessage("Hidden tile keys must be unique.");
    }

    private static bool AllKeysWellFormed(IReadOnlyList<string>? keys)
        => keys is null || keys.All(key =>
            !string.IsNullOrWhiteSpace(key) && key.Length <= MaxTileKeyLength && TileKeyPattern().IsMatch(key));

    private static bool AllKeysUnique(IReadOnlyList<string>? keys)
        => keys is null || keys.Distinct(StringComparer.Ordinal).Count() == keys.Count;

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex TileKeyPattern();
}

public class SaveTileLayoutCommandHandler : IRequestHandler<SaveTileLayoutCommand, ServiceResult<TileLayoutDto>>
{
    private const int MaxOrderJsonLength = 2000;

    private readonly IUnitOfWork _unitOfWork;

    public SaveTileLayoutCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<TileLayoutDto>> Handle(SaveTileLayoutCommand request, CancellationToken cancellationToken)
    {
        if (TileLayoutMapping.WriteKeys(request.TileKeys ?? []).Length > MaxOrderJsonLength
            || TileLayoutMapping.WriteKeys(request.HiddenTileKeys ?? []).Length > MaxOrderJsonLength)
        {
            return ServiceResult<TileLayoutDto>.Failure(ResultType.BadRequest, UsersErrors.LayoutTooLarge, "Tile layout is too large.");
        }

        var existing = await _unitOfWork.UserTileLayouts.GetByBoardAsync(request.UserKey, request.BoardKey, cancellationToken);

        if (existing == null)
        {
            existing = new UserTileLayout
            {
                UserTileLayoutKey = Guid.NewGuid(),
                UserKey = request.UserKey,
                BoardKey = request.BoardKey
            };
            request.ApplyTo(existing, DateTime.UtcNow);
            _unitOfWork.UserTileLayouts.Create(existing, cancellationToken);
        }
        else
        {
            request.ApplyTo(existing, DateTime.UtcNow);
            _unitOfWork.UserTileLayouts.Update(existing, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ServiceResult<TileLayoutDto>(ResultType.Success, existing.ToDto());
    }
}
