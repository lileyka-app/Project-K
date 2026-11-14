using FluentValidation;
using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KurinModule;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

/// <summary>
/// Creates a tag (no key) or renames and recolours one. The name is unique within the kurin and the
/// colour comes from the palette. Gated at the controller by <c>MemberTag:Update</c>.
/// </summary>
public sealed record UpsertKurinTagCommand(Guid KurinKey, Guid? KurinTagKey, string Name, string ColorHex)
    : IRequest<ServiceResult<KurinTagDto>>;

public sealed class UpsertKurinTagCommandHandler : IRequestHandler<UpsertKurinTagCommand, ServiceResult<KurinTagDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IKurinTagVisibility _visibility;
    private readonly IKurinAudit _audit;

    public UpsertKurinTagCommandHandler(IUnitOfWork uow, IKurinTagVisibility visibility, IKurinAudit audit)
    {
        _uow = uow;
        _visibility = visibility;
        _audit = audit;
    }

    public async Task<ServiceResult<KurinTagDto>> Handle(UpsertKurinTagCommand request, CancellationToken cancellationToken)
    {
        var settings = await _visibility.SettingsAsync(request.KurinKey, cancellationToken);
        if (settings is null)
        {
            return KurinErrors.NotFound<KurinTagDto>();
        }

        if (!settings.Enabled)
        {
            return ServiceResult<KurinTagDto>.Failure(ResultType.Conflict, KurinErrors.TagsDisabled, "The kurin has tags turned off.");
        }

        var name = request.Name.Trim();
        // The validator has let only palette colours through; store the palette's own spelling.
        var color = KurinTagRules.Palette.First(c => c.Equals(request.ColorHex.Trim(), StringComparison.OrdinalIgnoreCase));
        var isNew = request.KurinTagKey is null || request.KurinTagKey == Guid.Empty;

        if (await _uow.KurinTags.NameTakenAsync(request.KurinKey, name, isNew ? null : request.KurinTagKey, cancellationToken))
        {
            return ServiceResult<KurinTagDto>.Failure(ResultType.Conflict, KurinErrors.TagNameTaken, "The kurin already has a tag with this name.");
        }

        KurinTag tag;
        if (isNew)
        {
            tag = new KurinTag { KurinKey = request.KurinKey, Name = name, ColorHex = color };
            _uow.KurinTags.Create(tag, cancellationToken);
            await _audit.RecordAsync(
                new AuditRecord(AuditArea.Tag, AuditActions.Tag.Created, tag.KurinTagKey, name,
                    Changes: AuditChanges.Added(("name", name), ("color", color)), KurinKey: request.KurinKey),
                cancellationToken);
        }
        else
        {
            var existing = await _uow.KurinTags.GetByKeyAsync(request.KurinTagKey!.Value, cancellationToken);
            if (existing is null || existing.KurinKey != request.KurinKey)
            {
                return ServiceResult<KurinTagDto>.Failure(ResultType.NotFound, KurinErrors.TagNotFound, "Tag was not found in this kurin.");
            }

            tag = existing;
            var changes = AuditChanges.Of(("name", tag.Name, name), ("color", tag.ColorHex, color));
            tag.Name = name;
            tag.ColorHex = color;
            tag.UpdatedDate = DateTime.UtcNow;
            if (changes.Count > 0)
            {
                await _audit.RecordAsync(
                    new AuditRecord(AuditArea.Tag, AuditActions.Tag.Changed, tag.KurinTagKey, name, Changes: changes, KurinKey: request.KurinKey),
                    cancellationToken);
            }
        }

        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateRowException)
        {
            // Someone saved a tag of that name between the check above and this save.
            return ServiceResult<KurinTagDto>.Failure(ResultType.Conflict, KurinErrors.TagNameTaken, "The kurin already has a tag with this name.");
        }

        return new ServiceResult<KurinTagDto>(
            isNew ? ResultType.Created : ResultType.Success,
            new KurinTagDto(tag.KurinTagKey, tag.Name, tag.ColorHex, null));
    }
}

public sealed class UpsertKurinTagCommandValidator : AbstractValidator<UpsertKurinTagCommand>
{
    public UpsertKurinTagCommandValidator()
    {
        RuleFor(c => c.KurinKey).NotEmpty();
        RuleFor(c => c.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= KurinTagRules.MaxNameLength);
        RuleFor(c => c.ColorHex)
            .Must(KurinTagRules.IsPaletteColor)
            .WithErrorCode(KurinErrors.TagColorNotInPalette);
    }
}
