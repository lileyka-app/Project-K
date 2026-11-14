using MediatR;
using ProjectK.BusinessLogic.Modules.KurinModule.Services;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule.Tags;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Dtos.KurinModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Features.Tag;

/// <summary>
/// Turns tags on or off and says whether people see their own. Turning them off hides them
/// everywhere but keeps them: on again, everything is where it was. A choice equal to the branch's
/// default is stored as no choice, so the kurin keeps following the default.
/// </summary>
public sealed record SetKurinTagSettingsCommand(Guid KurinKey, bool Enabled, bool OwnTagsVisible)
    : IRequest<ServiceResult<KurinTagSettingsDto>>;

public sealed class SetKurinTagSettingsCommandHandler : IRequestHandler<SetKurinTagSettingsCommand, ServiceResult<KurinTagSettingsDto>>
{
    /// <summary>What the kurin's journal calls this setting.</summary>
    public const string Label = "Теги";

    private readonly IUnitOfWork _uow;
    private readonly IKurinTagVisibility _visibility;
    private readonly IKurinAudit _audit;

    public SetKurinTagSettingsCommandHandler(IUnitOfWork uow, IKurinTagVisibility visibility, IKurinAudit audit)
    {
        _uow = uow;
        _visibility = visibility;
        _audit = audit;
    }

    public async Task<ServiceResult<KurinTagSettingsDto>> Handle(SetKurinTagSettingsCommand request, CancellationToken cancellationToken)
    {
        var before = await _visibility.SettingsAsync(request.KurinKey, cancellationToken);
        if (before is null)
        {
            return KurinErrors.NotFound<KurinTagSettingsDto>();
        }

        var stored = await _uow.KurinTags.GetSettingsAsync(request.KurinKey, cancellationToken);
        if (stored is null)
        {
            stored = new KurinTagSettings { KurinKey = request.KurinKey };
            _uow.KurinTags.AddSettings(stored);
        }

        stored.Enabled = request.Enabled == before.EnabledByDefault ? null : request.Enabled;
        stored.OwnTagsVisible = request.OwnTagsVisible;

        var changes = AuditChanges.Of(
            ("enabled", before.Enabled, request.Enabled),
            ("ownTagsVisible", before.OwnTagsVisible, request.OwnTagsVisible));
        if (changes.Count > 0)
        {
            await _audit.RecordAsync(
                new AuditRecord(AuditArea.Settings, AuditActions.Settings.TagSettingsChanged, request.KurinKey, Label,
                    Changes: changes, KurinKey: request.KurinKey),
                cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        return new ServiceResult<KurinTagSettingsDto>(ResultType.Success,
            new KurinTagSettingsDto(request.Enabled, before.EnabledByDefault, request.OwnTagsVisible));
    }
}
