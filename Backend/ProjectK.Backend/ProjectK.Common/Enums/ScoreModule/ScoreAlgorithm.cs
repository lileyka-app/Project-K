namespace ProjectK.Common.Enums.ScoreModule;

/// <summary>How a гурток's score is made of its youths' points. The kurin chooses.</summary>
public enum ScoreAlgorithm
{
    /// <summary>
    /// The points divided by how many youths the гурток had, so a small active гурток does not lose to
    /// a large idle one. Someone who was there for part of the period counts for that part.
    /// </summary>
    Average = 0,

    Sum = 1
}
