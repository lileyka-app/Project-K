namespace ProjectK.Common.Dtos.ScoreModule.Requests;

/// <summary>Marks these people present at one occurrence; those already marked are left as they are.</summary>
public sealed class MarkAttendanceRequest
{
    public IReadOnlyList<Guid> MembershipKeys { get; set; } = [];
}
