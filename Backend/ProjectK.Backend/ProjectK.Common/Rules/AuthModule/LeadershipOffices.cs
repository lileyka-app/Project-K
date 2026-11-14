using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Rules.AuthModule;

/// <summary>
/// The canonical grouping of діловодські offices under each провід (курінний / гуртковий / КВ).
/// This is the backend mirror of the frontend <c>LEADERSHIP_ROLE_MAP</c> and the single source for
/// which (<see cref="LeadershipType"/>, <see cref="LeadershipRole"/>) pairs are valid — used to seed
/// the system-role set and to enumerate assignable offices.
/// </summary>
public static class LeadershipOffices
{
    private static readonly LeadershipRole[] CommonOffices =
    {
        LeadershipRole.Suddya,
        LeadershipRole.Pysar,
        LeadershipRole.Skarbnyk,
        LeadershipRole.Horunjiy,
        LeadershipRole.Gospodar,
        LeadershipRole.Hronikar
    };

    public static readonly IReadOnlyDictionary<LeadershipType, IReadOnlyList<LeadershipRole>> Grouping =
        new Dictionary<LeadershipType, IReadOnlyList<LeadershipRole>>
        {
            [LeadershipType.Kurin] = new[] { LeadershipRole.Kurinnuy }
                .Concat(CommonOffices)
                .Append(LeadershipRole.OtherKurin)
                .ToArray(),

            [LeadershipType.Group] = new[] { LeadershipRole.Hurtkoviy }
                .Concat(CommonOffices)
                .Append(LeadershipRole.OtherGroup)
                .ToArray(),

            [LeadershipType.KV] = new[]
            {
                LeadershipRole.Instruktor,
                LeadershipRole.Vykhovnyk,
                LeadershipRole.Zvyazkovyi
            }
        };

    /// <summary>Every valid (провід, офіс) pair, flattened.</summary>
    public static IEnumerable<(LeadershipType Type, LeadershipRole Role)> All() =>
        Grouping.SelectMany(pair => pair.Value.Select(role => (pair.Key, role)));
}
