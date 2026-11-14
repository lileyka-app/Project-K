namespace ProjectK.Common.Dtos.ProbesAndBadgesModule.Requests;

/// <summary>
/// A review is a verdict, and the verdict has to be said. It used to be a plain <c>bool</c>: a body
/// that failed to mention it — a typo in the field name is enough — was read as <c>false</c> and
/// silently **refused** the thing under review. Nullable so that "not said" is its own answer, and
/// the validator turns it into a 400 instead of a rejection nobody asked for.
/// </summary>
public class ReviewBadgeProgressRequest
{
    public bool? IsApproved { get; set; }
    public string? Note { get; set; }
}
