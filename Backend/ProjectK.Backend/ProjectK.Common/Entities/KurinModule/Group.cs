using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectK.Common.Entities;

namespace ProjectK.Common.Entities.KurinModule;

public class Group : Entity
{
    public Guid GroupKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? SilhouetteBlobName { get; set; }

    /// <summary>
    /// The edition of the probe programme the гурток walks (the catalogue's <c>edition</c>, e.g.
    /// "upyu-2026"); null for the one kurins have walked from the start. A youth who has closed a
    /// first probe in another edition finishes that one — see <c>ProbeLine</c>.
    /// </summary>
    public string? ProbeEdition { get; set; }
    public Kurin Kurin { get; set; }
    public ICollection<MentorAssignment> MentorAssignments { get; set; } = new List<MentorAssignment>();
    public Leadership Leadership { get; set; }

    public Group(string name, Guid kurinKey, string? description = null)
    {
        Name = name;
        KurinKey = kurinKey;
        Description = description;
    }
}
