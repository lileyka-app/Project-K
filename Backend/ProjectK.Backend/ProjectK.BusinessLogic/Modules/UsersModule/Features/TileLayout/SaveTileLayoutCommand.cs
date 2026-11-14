using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using ProjectK.Common.Entities.AuthModule;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Models.Dtos.UsersModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

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
        var tileKeys = request.TileKeys ?? [];

        var hiddenKeys = request.HiddenTileKeys ?? [];

        var orderJson = TileOrderSerializer.Serialize(tileKeys);
        var hiddenJson = TileOrderSerializer.Serialize(hiddenKeys);
        if (orderJson.Length > MaxOrderJsonLength || hiddenJson.Length > MaxOrderJsonLength)
        {
            return ServiceResult<TileLayoutDto>.Failure(ResultType.BadRequest, "LayoutTooLarge", "Tile layout is too large.");
        }

        var schemaVersion = request.SchemaVersion <= 0 ? 1 : request.SchemaVersion;
        var existing = await _unitOfWork.UserTileLayouts.GetByBoardAsync(request.UserKey, request.BoardKey, cancellationToken);

        if (existing == null)
        {
            existing = new UserTileLayout
            {
                UserTileLayoutKey = Guid.NewGuid(),
                UserKey = request.UserKey,
                BoardKey = request.BoardKey,
                TileOrderJson = orderJson,
                HiddenTilesJson = hiddenJson,
                SchemaVersion = schemaVersion,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _unitOfWork.UserTileLayouts.Create(existing, cancellationToken);
        }
        else
        {
            existing.TileOrderJson = orderJson;
            existing.HiddenTilesJson = hiddenJson;
            existing.SchemaVersion = schemaVersion;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            _unitOfWork.UserTileLayouts.Update(existing, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new TileLayoutDto(existing.BoardKey, tileKeys, hiddenKeys, existing.SchemaVersion, existing.UpdatedAtUtc);
        return new ServiceResult<TileLayoutDto>(ResultType.Success, dto);
    }
}
