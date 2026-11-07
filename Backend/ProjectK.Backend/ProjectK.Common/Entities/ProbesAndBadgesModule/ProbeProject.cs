using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.ProbesAndBadgesModule;

/// <summary>
/// One project of the third probe, laid out as the УПЮ template asks: what, where and for whom,
/// who coordinates it, then the goal, the plan and, once done, what came of it. The probe is the one
/// at the third place of the person's line, kept by its catalogue id so a switch of edition does not
/// carry the project over to a probe it was not written for.
/// </summary>
public class ProbeProject : Entity
{
    public Guid ProbeProjectKey { get; set; } = Guid.NewGuid();

    /// <summary>The person. A key, not a relationship: the member lives in another module.</summary>
    public Guid MemberKey { get; set; }
    public string ProbeId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>The programme asks for one project from each of its five areas.</summary>
    public ProbeProjectArea? Area { get; set; }

    /// <summary>Whom the project serves; several may apply.</summary>
    public ProbeProjectReach Reach { get; set; }

    /// <summary>Done together with other third-probers; at most two of the five may be.</summary>
    public bool IsShared { get; set; }
    public string? SharedWith { get; set; }

    /// <summary>
    /// The sixth, held in reserve: written out like the others and swapped for a main one when that
    /// one falls through.
    /// </summary>
    public bool IsSpare { get; set; }

    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public string? Location { get; set; }

    /// <summary>«Провід заходу»: who runs the event the project lives in.</summary>
    public string? EventLeads { get; set; }

    /// <summary>
    /// «Координатор / комендант»: who guides the project and recommends it. May be someone outside
    /// Plast, without an account, hence a name and a contact.
    /// </summary>
    public string? CoordinatorName { get; set; }
    public string? CoordinatorContact { get; set; }

    /// <summary>
    /// The texts, in Markdown as the editor's delta turns into it: what the project is and the
    /// person's role in it, then the template's goal, plan and results. Inline pictures are written
    /// as <c>project-image:{key}</c> rather than an address: blob links are signed and expire.
    /// </summary>
    public string Description { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string Results { get; set; } = string.Empty;

    /// <summary>«Я одержав дозвіл звʼязкового або виховника»: given by whoever may sign the probe.</summary>
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserKey { get; set; }
    public string? ApprovedByName { get; set; }

    /// <summary>«Юнак успішно виконав проєкт», with the coordinator's words as they gave them.</summary>
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedByUserKey { get; set; }
    public string? CompletedByName { get; set; }
    public string? CoordinatorComment { get; set; }

    public Guid? UpdatedByUserKey { get; set; }
    public string? UpdatedByName { get; set; }

    public ICollection<ProbeProjectImage> Images { get; set; } = new List<ProbeProjectImage>();
}
