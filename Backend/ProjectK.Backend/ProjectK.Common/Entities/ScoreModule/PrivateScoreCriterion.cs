using ProjectK.Common.Entities.Shared;

namespace ProjectK.Common.Entities.ScoreModule;

/// <summary>
/// What the КВ scores among themselves by — «лідерство», «ініціатива» — in words of their own, apart
/// from the kurin's public list. Archived ones stay for what was already written and leave the picker.
/// </summary>
public class PrivateScoreCriterion : Entity
{
    public Guid PrivateScoreCriterionKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsArchived { get; set; }
}
