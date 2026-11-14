using ProjectK.Common.Enums.DuesModule;

namespace ProjectK.BusinessLogic.Modules.DuesModule.Services;

public static class DuesAccountStandings
{
    /// <summary>Gone from the kurin first; otherwise here or moved on, by the гурток they stand in now.</summary>
    public static DuesAccountStanding Of(bool hasLeft, bool standsInThisGroup)
    {
        if (hasLeft)
        {
            return DuesAccountStanding.Left;
        }

        return standsInThisGroup ? DuesAccountStanding.Current : DuesAccountStanding.Moved;
    }
}
