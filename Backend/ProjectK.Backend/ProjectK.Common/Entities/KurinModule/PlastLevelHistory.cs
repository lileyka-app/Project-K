using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

public class PlastLevelHistory
{
    public Guid PlastLevelHistoryKey { get; set; }

    public Guid MemberKey { get; set; }

    /// <summary>
    /// Where the level was reached. Nullable because entries recorded before memberships existed
    /// carry no such context — the level is the person's either way.
    /// </summary>
    public Guid? KurinKey { get; set; }
    public Member Member { get; set; }

    public PlastLevel PlastLevel { get; set; }

    /// <summary>
    /// A пластунка's ступінь rather than a пластун's: the same step, its own name and відзнака in the
    /// catalogue. Chosen when the ступінь is recorded; the card keeps no sex of its own.
    /// </summary>
    public bool IsPlastunka { get; set; }

    public DateOnly DateAchieved { get; set; }
}
