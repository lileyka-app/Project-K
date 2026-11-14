using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// One answer per (event, occurrence, account): a fresh answer overwrites the old one for that
/// occurrence only. Shared by the calendar and the dashboard so both write the same row the same
/// way. The occurrence key comes resolved (<see cref="AgendaOccurrenceRules"/>). Does not save.
/// </summary>
public sealed class AgendaRsvpWriter
{
    private readonly IUnitOfWork _unitOfWork;

    public AgendaRsvpWriter(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task UpsertAsync(AgendaItem item, Guid userKey, DateTime? occurrenceKey, AgendaRsvpStatus status, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.AgendaResponses.GetForItemAndUserAsync(item.AgendaItemKey, userKey, occurrenceKey, cancellationToken);
        if (existing is null)
        {
            _unitOfWork.AgendaResponses.Create(new AgendaResponse
            {
                AgendaItemKey = item.AgendaItemKey,
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
