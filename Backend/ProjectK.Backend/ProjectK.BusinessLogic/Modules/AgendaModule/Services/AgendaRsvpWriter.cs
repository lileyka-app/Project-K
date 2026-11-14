using ProjectK.Common.Contracts.AgendaModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.AgendaModule;
using ProjectK.Common.Enums.AgendaModule;

namespace ProjectK.BusinessLogic.Modules.AgendaModule.Services;

/// <inheritdoc />
public sealed class AgendaRsvpWriter : IAgendaRsvpWriter
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
