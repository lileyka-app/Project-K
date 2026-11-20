using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// A position on the kurin's list — «однострій» +1, «запізнення» −1. Given at an event, it can be given
/// to a person only once there, whoever gives it: that is what stops two судді scoring the same thing
/// twice. Archived positions stay for what was already given and leave the picker.
/// </summary>
public class ScoreItem : Entity
{
    public Guid ScoreItemKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Points { get; set; }

    public bool IsArchived { get; set; }
}
