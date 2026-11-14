using MediatR;
using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Enums.Shared;
using ProjectK.Common.Records.Shared;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>Why a гурток's operation can no longer be changed or deleted, if it cannot.</summary>
public static class DuesEntryLock
{
    public static ServiceResult<Unit>? Check(DuesEntry entry)
    {
        if (entry.IsVerified)
        {
            return DuesErrors.Locked<Unit>("впорядник");
        }

        // The kurin counted this money as received; changing it here would quietly change the kurin's box.
        if (entry.ReceivedAtUtc.HasValue)
        {
            return ServiceResult<Unit>.Failure(ResultType.Conflict, DuesErrors.TransferReceived,
                "The kurin treasurer has received this transfer. They have to take the confirmation back first.");
        }

        return null;
    }
}
