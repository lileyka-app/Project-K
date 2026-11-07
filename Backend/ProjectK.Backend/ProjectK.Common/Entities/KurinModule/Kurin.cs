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
using ProjectK.Common.Models.Records;

namespace ProjectK.Common.Entities.KurinModule;

public class Kurin : Entity
{
    /// <summary>
    /// EF's way in: it fills the гілки from their own table. The other constructors' parameters are
    /// named apart from the properties on purpose, so EF never binds to them and invents a гілка row.
    /// </summary>
    private Kurin()
    {
    }

    /// <summary>A kurin of one гілка — УПЮ unless said otherwise.</summary>
    public Kurin(int? kurinNumber, KurinBranch inBranch = KurinBranch.UPYu)
    {
        SetBranches([new KurinNumbering(inBranch, kurinNumber)]);
    }

    /// <summary>
    /// A kurin of several гілки — a senior one in УСП and УПС. A factory rather than a constructor, so
    /// that <c>new Kurin(null)</c> keeps meaning a підготовчий курінь.
    /// </summary>
    public static Kurin InBranches(IReadOnlyCollection<KurinNumbering> numbering)
    {
        var kurin = new Kurin();
        kurin.SetBranches(numbering);
        return kurin;
    }

    public Guid KurinKey { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The kurin's number in its main гілка (<see cref="Branch"/>). Null for a підготовчий курінь,
    /// which has not been given one yet; its plaque renders empty until a number is set.
    /// A mirror of the first row of <see cref="Branches"/>, written only by <see cref="SetBranches"/>,
    /// so that everything that names a kurin by one number keeps reading one column.
    /// </summary>
    public int? Number { get; private set; }

    /// <summary>
    /// Every гілка the kurin belongs to with its number there — the source of truth for numbering.
    /// Load it before <see cref="SetBranches"/> or <see cref="SetNumber"/>, which rewrite it.
    /// </summary>
    public ICollection<KurinBranchNumber> Branches { get; private set; } = new List<KurinBranchNumber>();

    /// <summary>The kurin's гілки and numbers, in the order they are shown.</summary>
    public IReadOnlyList<KurinNumbering> Numbering
        => [.. KurinBranchSet.Ordered(Branches, row => row.Branch).Select(row => new KurinNumbering(row.Branch, row.Number))];

    /// <summary>
    /// Replaces the kurin's гілки. The caller checks the set with <see cref="KurinBranchSet.IsValid"/>
    /// and the numbers against the край; this keeps <see cref="Branch"/> and <see cref="Number"/>
    /// pointing at the first гілка.
    /// </summary>
    public void SetBranches(IReadOnlyCollection<KurinNumbering> branches)
    {
        var wanted = KurinBranchSet.Ordered(branches, b => b.Branch).ToList();
        if (wanted.Count == 0)
        {
            throw new ArgumentException("A kurin belongs to at least one гілка.", nameof(branches));
        }

        foreach (var gone in Branches.Where(row => wanted.All(b => b.Branch != row.Branch)).ToList())
        {
            Branches.Remove(gone);
        }

        foreach (var numbering in wanted)
        {
            var row = Branches.FirstOrDefault(r => r.Branch == numbering.Branch);
            if (row is null)
            {
                Branches.Add(new KurinBranchNumber { Branch = numbering.Branch, Number = numbering.Number, KraiCode = _kraiCode });
            }
            else
            {
                row.Number = numbering.Number;
            }
        }

        Branch = wanted[0].Branch;
        Number = wanted[0].Number;
    }

    /// <summary>Gives the main гілка a new number; the other гілки keep theirs.</summary>
    public void SetNumber(int? number)
        => SetBranches([.. Numbering.Select(n => n.Branch == Branch ? n with { Number = number } : n)]);

    private string _kraiCode = Krai.Ukraine;

    /// <summary>
    /// The край this kurin belongs to; its numbers are unique within that край only. Every гілка row
    /// carries it too, for the database's uniqueness, so setting it rewrites them.
    /// </summary>
    public string KraiCode
    {
        get => _kraiCode;
        set
        {
            _kraiCode = value;
            foreach (var row in Branches)
            {
                row.KraiCode = value;
            }
        }
    }
    public string? Stanytsia { get; set; }
    public string? RegionOrCountry { get; set; }
    public string? NamedAfter { get; set; }
    public string? Description { get; set; }

    /// <summary>The kurin's main гілка: the first of <see cref="Branches"/>, mirrored like <see cref="Number"/>.</summary>
    public KurinBranch Branch { get; private set; } = KurinBranch.UPYu;
    public bool ProfileVerificationEnabled { get; set; }

    /// <summary>
    /// Whether the kurin keeps точкування, as its settings say; null is the default for its гілка.
    /// Switching it off hides the menu, tiles and pages and keeps every score already recorded.
    /// </summary>
    public bool? ScoreEnabled { get; set; }

    /// <summary>
    /// The kurin's own names for its roles, as <c>{"Pysar":"Скриптор"}</c>; null keeps the usual ones.
    /// Read it through <c>KurinRoleNames.Read</c>. Names only: rights stay with the role.
    /// </summary>
    public string? RoleNames { get; set; }

    /// <summary>A senior kurin's emblem, shown instead of the УПЮ number plate; null shows a plain placeholder.</summary>
    public string? EmblemBlobName { get; set; }

    /// <summary>Точкування as the kurin has it now: on in УПЮ and off in the older гілки unless the kurin chose.</summary>
    public bool IsScoreEnabled => ScoreEnabled ?? !KurinBranchSet.IsSenior(Branch);

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
