namespace ProjectK.Common.Enums.KurinModule;

/// <summary>
/// What a person is in a kurin. A юнак and the виховник who runs their гурток are both members of
/// it, but only one of them is passing проби there — and the same person can be Youth in one kurin
/// and Staff in another.
/// </summary>
public enum MembershipKind
{
    /// <summary>A member of the kurin in the ordinary sense: юнак, старший пластун, сеньйор.</summary>
    Youth = 0,

    /// <summary>Part of the kurin's виховний склад without being a member of its youth body.</summary>
    Staff = 1
}
