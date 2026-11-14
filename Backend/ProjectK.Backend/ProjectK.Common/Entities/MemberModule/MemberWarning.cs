using ProjectK.Common.Entities.Shared;
using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Entities.MemberModule;

public class MemberWarning : Entity
{
    public Guid MemberWarningKey { get; set; } = Guid.NewGuid();
    public Guid MemberKey { get; set; }

    /// <summary>
    /// The kurin that issued it. A пересторога is a kurin's act, so it stays with the kurin that
    /// wrote it even after the person joins another one — and it is what scopes who may see it.
    /// </summary>
    public Guid KurinKey { get; set; }
    public Member Member { get; set; } = null!;
    public MemberWarningLevel Level { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public Guid IssuedByUserKey { get; set; }
    public Guid? RevokedByUserKey { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
