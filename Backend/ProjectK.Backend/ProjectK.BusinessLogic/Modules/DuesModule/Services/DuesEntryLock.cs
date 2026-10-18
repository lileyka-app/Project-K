using ProjectK.Common.Entities.DuesModule;
using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

/// <summary>Why a гурток's operation can no longer be changed or deleted, if it cannot.</summary>
public static class DuesEntryLock
{
    public static ServiceResult<object>? Check(DuesEntry entry)
    {
        if (entry.IsVerified)
        {
            return ServiceResult<object>.Failure(ResultType.Conflict, "EntryVerified",
                "A verified operation is locked. Add a correction, or have the впорядник unmark it.");
        }

        // The kurin counted this money as received; changing it here would quietly change the kurin's box.
        if (entry.ReceivedAtUtc.HasValue)
        {
            return ServiceResult<object>.Failure(ResultType.Conflict, "TransferReceived",
                "The kurin treasurer has received this transfer. They have to take the confirmation back first.");
        }

        return null;
    }
}
