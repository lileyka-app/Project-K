using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectK.Common.Entities;
using ProjectK.Common.Entities.KraiModule;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Entities.KurinModule.Planning;
using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

public class Kurin(int? number) : Entity
{
    public Guid KurinKey { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The kurin's number. Null for a підготовчий курінь, which has not been given one yet;
    /// its plaque renders empty until a number is set in the kurin's settings.
    /// </summary>
    public int? Number { get; set; } = number;
    /// <summary>The край this kurin belongs to; its number is unique within that край only.</summary>
    public string KraiCode { get; set; } = Krai.Ukraine;
    public string? Stanytsia { get; set; }
    public string? RegionOrCountry { get; set; }
    public string? NamedAfter { get; set; }
    public string? Description { get; set; }

    /// <summary>Which пластова гілка this kurin belongs to.</summary>
    public KurinBranch Branch { get; set; } = KurinBranch.UPYu;
    public bool ProfileVerificationEnabled { get; set; }

    /// <summary>Days a done task stays on the board before it moves to the archive by itself; null keeps it there.</summary>
    public int? TaskAutoArchiveAfterDays { get; set; } = 30;

    /// <summary>Days an archived task is kept before it is deleted for good; null keeps the archive forever.</summary>
    public int? TaskArchivePurgeAfterDays { get; set; } = 365;

    /// <summary>
    /// Card fields this kurin leaves off, as a JSON array of keys: only what its край lets a kurin
    /// drop (see <c>KraiCardFieldsSettings.Hideable</c>). A kurin can narrow its край's card, never widen it.
    /// </summary>
    public string? HiddenCardFields { get; set; }

    /// <summary>
    /// The kurin's «Права й видимість»: only the cells that differ from the defaults in code, as
    /// <c>{"v":1,"grants":{…}}</c>. Null is the defaults. Read it only through
    /// <c>KurinAccessPolicy.Read</c>, which skips what it does not understand.
    /// </summary>
    public string? AccessPolicy { get; set; }
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
    public ICollection<Leadership> Leaderships { get; set; } = new List<Leadership>();
    public ICollection<PlanningSession> PlanningSessions { get; set; } = new List<PlanningSession>();
    public ICollection<AgendaItem> AgendaItems { get; set; } = new List<AgendaItem>();
}
