using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.KurinModule;

public class LeadershipHistoryDto
{
    public Guid LeadershipHistoryKey { get; set; }
    public Guid MemberKey { get; set; }
    public Guid LeadershipKey { get; set; }
    public LeadershipRole Role { get; set; }
    public LeadershipType? LeadershipType { get; set; }
    public string? GroupName { get; set; }

    /// <summary>
    /// The kurin the office is in — its own for a курінний провід or КВ, its гурток's for a
    /// гуртковий. A card reads offices from every kurin the person stands in; this says which is
    /// whose, so each is shown in its kurin and named by that kurin's own role names.
    /// </summary>
    public Guid? KurinKey { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
