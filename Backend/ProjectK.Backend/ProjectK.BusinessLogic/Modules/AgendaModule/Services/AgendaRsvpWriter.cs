using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.KraiModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;
using ProjectK.Common.Records.AgendaModule;
using ProjectK.Common.Rules.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <inheritdoc />
public sealed class AgendaRsvpWriter : IAgendaRsvpWriter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKraiProfileDirectory _profiles;

    public AgendaRsvpWriter(IUnitOfWork unitOfWork, IKraiProfileDirectory profiles)
    {
        _unitOfWork = unitOfWork;
        _profiles = profiles;
    }

    public async Task<AgendaRsvpOutcome> AnswerAsync(
        IReadOnlyCollection<AgendaViewerContext> viewers,
        Guid agendaItemKey,
        DateTime? occurrenceStartUtc,
        AgendaRsvpStatus status,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var item = await _unitOfWork.AgendaItems.GetByKeyWithAssignmentsAsync(agendaItemKey, cancellationToken);
        if (item is null || item.Kind != AgendaItemKind.Event)
        {
            return AgendaRsvpOutcome.NoSuchEvent;
        }

        var viewer = viewers.FirstOrDefault(v => v.KurinKey == item.KurinKey);
        if (viewer?.ViewerUserKey is not { } userKey || !AgendaVisibilityRules.IsVisible(item, viewer.ToScope()))
        {
            return AgendaRsvpOutcome.NotVisible;
        }

        var zone = await _profiles.ForKurinAsync(item.KurinKey, cancellationToken);
        if (!AgendaOccurrenceRules.TryResolveKey(item, occurrenceStartUtc, zone, out var occurrenceKey))
        {
            return AgendaRsvpOutcome.NoSuchOccurrence;
        }

        await UpsertAsync(item.AgendaItemKey, userKey, occurrenceKey, status, nowUtc, cancellationToken);
        return AgendaRsvpOutcome.Staged;
    }

    public async Task UpsertAsync(Guid agendaItemKey, Guid userKey, DateTime? occurrenceKey, AgendaRsvpStatus status, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.AgendaResponses.GetForItemAndUserAsync(agendaItemKey, userKey, occurrenceKey, cancellationToken);
        if (existing is null)
        {
            _unitOfWork.AgendaResponses.Create(new AgendaResponse
            {
                AgendaItemKey = agendaItemKey,
                UserKey = userKey,
                OccurrenceStartUtc = occurrenceKey,
                Status = status,
                RespondedAtUtc = nowUtc
            }, cancellationToken);
        }
        else if (existing.Status != status)
        {
            // Re-time on change so switching to «Going» joins the back of the queue rather than keeping
            // an earlier «Maybe» slot.
            existing.Status = status;
            existing.RespondedAtUtc = nowUtc;
            existing.UpdatedDate = nowUtc;
            _unitOfWork.AgendaResponses.Update(existing, cancellationToken);
        }
    }
}
