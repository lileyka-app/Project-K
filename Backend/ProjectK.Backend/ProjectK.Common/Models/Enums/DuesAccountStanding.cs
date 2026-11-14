namespace ProjectK.Common.Models.Enums;

/// <summary>
/// Where the person stands towards a box. The registers list accounts in this order, so it is the
/// order on the screen too.
/// </summary>
public enum DuesAccountStanding
{
    /// <summary>A youth of this гурток today.</summary>
    Current,

    /// <summary>Moved to another гурток of the kurin, with quarters still owed here.</summary>
    Moved,

    /// <summary>Left the kurin, with quarters still owed here.</summary>
    Left
}
