namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

public class UpsertLeadershipRequest
{
    public string? Type { get; set; }
    public Guid? EntityKey { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public IEnumerable<LeadershipHistoryMemberDto> LeadershipHistories { get; set; } = [];
}
