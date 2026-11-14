using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Models.Enums;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Services;

/// <summary>
/// One answer per (event, occurrence, account): a fresh answer overwrites the old one for that
/// occurrence only. Shared by the calendar and the dashboard so both write the same row the same
/// way. The occurrence key comes resolved (<see cref="AgendaOccurrences"/>). Does not save.
/// </summary>
public static class AgendaRsvpWriter
{
    public static async Task UpsertAsync(IUnitOfWork unitOfWork, AgendaItem item, Guid userKey, DateTime? occurrenceKey, AgendaRsvpStatus status, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var existing = await unitOfWork.AgendaResponses.GetForItemAndUserAsync(item.AgendaItemKey, userKey, occurrenceKey, cancellationToken);
        if (existing is null)
        {
            unitOfWork.AgendaResponses.Create(new AgendaResponse
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
            unitOfWork.AgendaResponses.Update(existing, cancellationToken);
        }
    }
}
