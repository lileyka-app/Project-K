using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.KurinModule;

/// <summary>
/// Whether a kurin lets its people under 18 share their card, and how much of it. A kurin with no row
/// lets them share nothing. Those who are 18 or older decide for themselves and are not bound by it.
/// </summary>
public class KurinCardSharing : Entity
{
    public Guid KurinCardSharingKey { get; set; } = Guid.NewGuid();
    public Guid KurinKey { get; set; }

    public bool MinorsMayShare { get; set; }

    /// <summary>The most a minor's card may show; their own choice is cut to it.</summary>
    public CardField MinorFields { get; set; } = MemberCardShare.DefaultFields;

    /// <summary>Whether a minor may also make a permanent link, not only show the card for checking.</summary>
    public bool MinorsMayLink { get; set; }

    public Guid? SetByUserKey { get; set; }
}
