using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Contracts.AuditModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Dtos.KraiModule;
using ProjectK.Common.Enums.AuditModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.AuditModule;
using ProjectK.Common.Records.Shared;
using ProjectK.Common.Rules.AuditModule;
using ProjectK.Common.Rules.KraiModule;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

/// <summary>Which of them the kurin hides; a key it may not hide is dropped.</summary>
public sealed record SetKurinCardFieldsCommand(Guid KurinKey, IReadOnlyCollection<string> Hidden) : IRequest<ServiceResult<IReadOnlyList<KurinCardFieldDto>>>;

public sealed class SetKurinCardFieldsCommandHandler : IRequestHandler<SetKurinCardFieldsCommand, ServiceResult<IReadOnlyList<KurinCardFieldDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKraiProfileProvider _profiles;
    private readonly IBackendCache _cache;
    private readonly IKurinAudit _audit;

    /// <summary>What the kurin's journal calls this setting.</summary>
    public const string Label = "Поля картки";

    public SetKurinCardFieldsCommandHandler(IUnitOfWork unitOfWork, IKraiProfileProvider profiles, IBackendCache cache, IKurinAudit audit)
    {
        _unitOfWork = unitOfWork;
        _profiles = profiles;
        _cache = cache;
        _audit = audit;
    }

    public async Task<ServiceResult<IReadOnlyList<KurinCardFieldDto>>> Handle(SetKurinCardFieldsCommand request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        var profile = kurin is null ? null : await _profiles.GetForKraiAsync(kurin.KraiCode, cancellationToken);
        if (kurin is null || profile is null)
        {
            return ServiceResult<IReadOnlyList<KurinCardFieldDto>>.Failure(ResultType.NotFound, KraiErrors.KurinNotFound, "Kurin not found.");
        }

        var hideable = profile.CardFields.Hideable().Select(field => field.Key).ToHashSet(StringComparer.Ordinal);
        var hiddenBefore = kurin.HiddenCardFields;
        kurin.HiddenCardFields = KurinHiddenCardFields.Write(request.Hidden.Where(hideable.Contains));
        _unitOfWork.Kurins.Update(kurin, cancellationToken);

        // One change for the whole list: a card field's key can be the very name the journal
        // keeps private (address, school), and a field per key would be recorded without its values.
        var changes = AuditChanges.Of((
            "hiddenFields",
            HiddenLabels(profile.CardFields, hiddenBefore),
            HiddenLabels(profile.CardFields, kurin.HiddenCardFields)));
        if (changes.Count > 0)
        {
            await _audit.RecordAsync(
                new AuditRecord(AuditArea.Settings, AuditActions.Settings.CardFieldsChanged, kurin.KurinKey, Label, Changes: changes, KurinKey: kurin.KurinKey),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _cache.Invalidate(BackendCachePolicies.KurinReads);

        return new ServiceResult<IReadOnlyList<KurinCardFieldDto>>(ResultType.Success, profile.CardFields.ToCardFieldDtos(kurin.HiddenCardFields));
    }

    /// <summary>The hidden fields by the names the settings page shows, in its order; a key no longer offered is named by itself.</summary>
    private static string HiddenLabels(KraiCardFieldsSettings card, string? hiddenJson)
    {
        var hidden = KurinHiddenCardFields.Read(hiddenJson);
        var offered = card.Hideable();
        var labels = offered.Where(field => hidden.Contains(field.Key)).Select(field => field.Label)
            .Concat(hidden.Where(key => offered.All(field => field.Key != key)).Order(StringComparer.Ordinal));
        return string.Join(", ", labels);
    }
}
