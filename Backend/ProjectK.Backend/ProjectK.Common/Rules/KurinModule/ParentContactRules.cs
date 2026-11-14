using ProjectK.Common.Contracts.KurinModule;

namespace ProjectK.Common.Rules.KurinModule;

/// <summary>The parents' contacts as one set of values, for whatever is done to all of them alike.</summary>
public static class ParentContactRules
{
    public sealed record Field(Func<IHasParentContacts, string?> Get, Action<IHasParentContacts, string?> Set);

    public static readonly IReadOnlyList<Field> Fields =
    [
        new(c => c.MotherFirstName, (c, v) => c.MotherFirstName = v),
        new(c => c.MotherLastName, (c, v) => c.MotherLastName = v),
        new(c => c.MotherPhone, (c, v) => c.MotherPhone = v),
        new(c => c.FatherFirstName, (c, v) => c.FatherFirstName = v),
        new(c => c.FatherLastName, (c, v) => c.FatherLastName = v),
        new(c => c.FatherPhone, (c, v) => c.FatherPhone = v)
    ];

    public static T WithParentsOf<T>(this T target, IHasParentContacts source) where T : IHasParentContacts
    {
        foreach (var field in Fields)
        {
            field.Set(target, field.Get(source));
        }

        return target;
    }

    /// <summary>Every value set to <paramref name="value"/>: blank for a viewer who may not see them.</summary>
    public static void SetAllParents(this IHasParentContacts target, string? value)
    {
        foreach (var field in Fields)
        {
            field.Set(target, value);
        }
    }

    public static bool ParentsDiffer(this IHasParentContacts left, IHasParentContacts right) =>
        Fields.Any(field => !string.Equals(field.Get(left) ?? string.Empty, field.Get(right) ?? string.Empty, StringComparison.Ordinal));
}
