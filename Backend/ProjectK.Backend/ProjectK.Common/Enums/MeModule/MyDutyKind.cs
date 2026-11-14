namespace ProjectK.Common.Enums.MeModule;

/// <summary>What a duty is about; the dashboard knows where each kind is done.</summary>
public enum MyDutyKind
{
    /// <summary>Вмілості handed in and waiting to be confirmed.</summary>
    BadgesToReview,
    /// <summary>Transfers from гуртки the kurin's скарбник has not confirmed receiving.</summary>
    TransfersToConfirm,
    /// <summary>Entries in a box nobody has verified yet.</summary>
    EntriesToVerify,
    /// <summary>A past event worth points where nobody has been marked present.</summary>
    EventWithoutAttendance
}
