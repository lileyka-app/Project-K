using MediatR;
using ProjectK.BusinessLogic.Services.Caching;
using ProjectK.Common.Interfaces;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Models.Audit;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.KraiProfiles;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KraiModule.Features.Profile;

/// <summary>One field a kurin may leave off its card, and whether it does.</summary>
public sealed record KurinCardFieldResponse(string Key, string Label, bool Hidden);

/// <summary>The card fields a kurin may hide, for its settings page.</summary>
public sealed record GetKurinCardFieldsQuery(Guid KurinKey) : IRequest<ServiceResult<IReadOnlyList<KurinCardFieldResponse>>>;

/// <summary>Which of them the kurin hides; a key it may not hide is dropped.</summary>
public sealed record SetKurinCardFieldsCommand(Guid KurinKey, IReadOnlyCollection<string> Hidden) : IRequest<ServiceResult<IReadOnlyList<KurinCardFieldResponse>>>;

public sealed class GetKurinCardFieldsQueryHandler : IRequestHandler<GetKurinCardFieldsQuery, ServiceResult<IReadOnlyList<KurinCardFieldResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKraiProfileProvider _profiles;

    public GetKurinCardFieldsQueryHandler(IUnitOfWork unitOfWork, IKraiProfileProvider profiles)
    {
        _unitOfWork = unitOfWork;
        _profiles = profiles;
    }

    public async Task<ServiceResult<IReadOnlyList<KurinCardFieldResponse>>> Handle(GetKurinCardFieldsQuery request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        var profile = kurin is null ? null : await _profiles.GetForKraiAsync(kurin.KraiCode, cancellationToken);
        return kurin is null || profile is null
            ? KurinCardFieldsResult.NotFound()
            : KurinCardFieldsResult.Of(profile.CardFields, kurin.HiddenCardFields);
    }
}

public sealed class SetKurinCardFieldsCommandHandler : IRequestHandler<SetKurinCardFieldsCommand, ServiceResult<IReadOnlyList<KurinCardFieldResponse>>>
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

    public async Task<ServiceResult<IReadOnlyList<KurinCardFieldResponse>>> Handle(SetKurinCardFieldsCommand request, CancellationToken cancellationToken)
    {
        var kurin = await _unitOfWork.Kurins.GetByKeyAsync(request.KurinKey, cancellationToken);
        var profile = kurin is null ? null : await _profiles.GetForKraiAsync(kurin.KraiCode, cancellationToken);
        if (kurin is null || profile is null)
        {
            return KurinCardFieldsResult.NotFound();
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

        return KurinCardFieldsResult.Of(profile.CardFields, kurin.HiddenCardFields);
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

internal static class KurinCardFieldsResult
{
    public static ServiceResult<IReadOnlyList<KurinCardFieldResponse>> Of(KraiCardFieldsSettings card, string? hiddenJson)
    {
        var hidden = KurinHiddenCardFields.Read(hiddenJson);
        return new ServiceResult<IReadOnlyList<KurinCardFieldResponse>>(
            ResultType.Success,
            card.Hideable().Select(field => new KurinCardFieldResponse(field.Key, field.Label, hidden.Contains(field.Key))).ToList());
    }

    public static ServiceResult<IReadOnlyList<KurinCardFieldResponse>> NotFound()
    {
        return ServiceResult<IReadOnlyList<KurinCardFieldResponse>>.Failure(ResultType.NotFound, "KurinNotFound", "Kurin not found.");
    }
}
