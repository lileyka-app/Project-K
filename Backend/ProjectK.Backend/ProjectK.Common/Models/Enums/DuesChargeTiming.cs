namespace ProjectK.Common.Models.Enums;

/// <summary>
/// When a kurin's вкладка falls due. Stanytsi differ: some pay for the quarter that is starting,
/// others for the one that has just passed. Stored as a number — the values are frozen.
/// </summary>
public enum DuesChargeTiming
{
    /// <summary>
    /// Наперед: a quarter is charged as it begins, so the running one is owed already.
    /// </summary>
    InAdvance = 0,

    /// <summary>
    /// За пройдений квартал: a quarter is charged once it is over, so the running one is not owed yet.
    /// </summary>
    InArrears = 1,
}
