using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Interfaces.Modules.DuesModule;
using ProjectK.Common.Interfaces.Modules.KraiModule;
using ProjectK.Common.Interfaces.Modules.KurinModule;
using ProjectK.Common.Interfaces.Modules.MemberModule;
using ProjectK.Common.Models.Dtos.DuesModule.Requests;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>
/// What creating and editing an operation have in common: the checks that need the database, and
/// copying the request onto the row.
/// </summary>
public sealed class DuesEntryWriter
{
    private readonly IDuesUnitOfWork _dues;
    private readonly IMembershipDirectory _memberships;
    private readonly IMemberDirectory _members;
    private readonly IKraiProfileProvider _profiles;

    public DuesEntryWriter(IDuesUnitOfWork dues, IMembershipDirectory memberships, IMemberDirectory members, IKraiProfileProvider profiles)
    {
        _dues = dues;
        _memberships = memberships;
        _members = members;
        _profiles = profiles;
    }

    /// <summary>
    /// Why the request cannot stand against this гурток, or null. A personal operation needs someone
    /// with an account here: a youth of the гурток today, or one who was charged here and moved on —
    /// an old debt is still paid to the гурток it arose in.
    /// </summary>
    public async Task<ServiceResult<T>?> CheckForGroupAsync<T>(Common.Entities.KurinModule.Group group, UpsertDuesEntryRequest request, CancellationToken cancellationToken)
    {
        if (request.MembershipKey is { } membershipKey)
        {
            var membership = (await _memberships.GetInKurinAsync(group.KurinKey, cancellationToken))
                .FirstOrDefault(m => m.MembershipKey == membershipKey);
            var standsHere = membership is not null && membership.GroupKey == group.GroupKey && membership.LeftAtUtc is null;
            var chargedHere = membership is not null
                && (await _dues.DuesCharges.GetForKurinAsync(group.KurinKey, cancellationToken))
                    .Any(c => c.MembershipKey == membershipKey && c.GroupKey == group.GroupKey);
            if (!standsHere && !chargedHere)
            {
                return ServiceResult<T>.Failure(ResultType.BadRequest, "NotOfThisGroup", "They have no вкладка in this гурток.");
            }
        }

        return await CheckCollectorAsync<T>(group.KurinKey, request, cancellationToken);
    }

    /// <summary>
    /// Why the request cannot stand against the kurin's own box, or null. Money goes up only the way
    /// the край takes it.
    /// </summary>
    public async Task<ServiceResult<T>?> CheckForKurinAsync<T>(Guid kurinKey, UpsertDuesEntryRequest request, CancellationToken cancellationToken)
    {
        if (!(await _profiles.DuesKraiForKurinAsync(kurinKey, cancellationToken)).Takes(request.Kind))
        {
            return ServiceResult<T>.Failure(ResultType.BadRequest, "DuesTransferOff", "The край does not take вкладка that way.");
        }

        return await CheckCollectorAsync<T>(kurinKey, request, cancellationToken);
    }

    private async Task<ServiceResult<T>?> CheckCollectorAsync<T>(Guid kurinKey, UpsertDuesEntryRequest request, CancellationToken cancellationToken)
    {
        if (request.CollectedByMemberKey is { } collector
            && await _members.FindKurinKeyAsync(collector, cancellationToken) != kurinKey)
        {
            return ServiceResult<T>.Failure(ResultType.BadRequest, "CollectorNotInKurin", "Whoever collected it has to be of this kurin.");
        }

        return null;
    }

    public static void Apply(DuesEntry entry, UpsertDuesEntryRequest request)
    {
        entry.Kind = request.Kind;
        entry.Method = request.Method;
        entry.CounterMethod = request.Kind == DuesEntryKind.Exchange ? request.CounterMethod : null;
        entry.Amount = request.Amount;
        entry.OccurredOn = request.OccurredOn;
        entry.MembershipKey = DuesEntryRules.PersonalKinds.Contains(request.Kind) ? request.MembershipKey : null;
        entry.CollectedByMemberKey = request.CollectedByMemberKey;
        entry.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
    }
}
