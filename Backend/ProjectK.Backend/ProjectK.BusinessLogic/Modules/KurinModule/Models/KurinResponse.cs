using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Records;

namespace ProjectK.BusinessLogic.Modules.KurinModule.Models;

public class KurinResponse
{
    public Guid KurinKey { get; set; }
    public int? Number { get; set; }

    /// <summary>A підготовчий курінь has no number yet.</summary>
    public bool IsPreparatory => Number is null;

    /// <summary>Яка гілка. Read by the UI, which shows юнацькі речі only where they mean something.</summary>
    public KurinBranch Branch { get; set; }

    /// <summary>Every гілка with the kurin's number there, main one first: «5-й курінь УСП · 20-й курінь УПС».</summary>
    public IReadOnlyList<KurinNumbering> Branches { get; set; } = [];
    public string KraiCode { get; set; } = string.Empty;
    public string? Stanytsia { get; set; }
    public string? RegionOrCountry { get; set; }
    public string? NamedAfter { get; set; }
    public string? Description { get; set; }
    public int CurrentUserCount { get; set; }
    public bool ProfileVerificationEnabled { get; set; }

    /// <summary>Whether the kurin keeps точкування now; the UI shows its menu, tiles and pages only then.</summary>
    public bool ScoreEnabled { get; set; }

    /// <summary>The kurin's own names for its roles, by role: «Pysar» → «Скриптор». Empty keeps the usual names.</summary>
    public IReadOnlyDictionary<string, string> RoleNames { get; set; } = new Dictionary<string, string>();

    /// <summary>A senior kurin's emblem, as a link that expires; null when it has none or is УПЮ.</summary>
    public string? EmblemUrl { get; set; }

    /// <summary>Whether the kurin may rename its roles at all: senior kurins for now.</summary>
    public bool CanRenameRoles { get; set; }
}
