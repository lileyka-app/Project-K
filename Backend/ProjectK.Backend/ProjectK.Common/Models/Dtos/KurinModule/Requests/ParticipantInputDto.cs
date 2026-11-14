namespace ProjectK.Common.Models.Dtos.KurinModule.Requests;

public class ParticipantInputDto
{
    public Guid MemberKey { get; set; }
    public string FullName { get; set; }
    public double RoleWeight { get; set; }
    public List<DateRangeDto> BusyRanges { get; set; } = [];
}
