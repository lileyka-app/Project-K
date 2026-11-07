using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// What a person lets their shared card show, and their permanent card link if they made one. A
/// person with no row shares <see cref="DefaultFields"/> and has no link. Whether they may share at
/// all is decided again on every read (by age and the kurin's choice), never stored here.
/// </summary>
public class MemberCardShare : Entity
{
    public const CardField DefaultFields = CardField.Kurin | CardField.Level;

    public Guid MemberCardShareKey { get; set; } = Guid.NewGuid();
    public Guid MemberKey { get; set; }

    public CardField Fields { get; set; } = DefaultFields;

    /// <summary>
    /// The permanent link's token, kept as is: the card behind it is public by the person's choice, and
    /// they come back for the link to share it again. Null while there is no link.
    /// </summary>
    public string? LinkToken { get; set; }

    public DateTime? LinkIssuedAtUtc { get; set; }
}
