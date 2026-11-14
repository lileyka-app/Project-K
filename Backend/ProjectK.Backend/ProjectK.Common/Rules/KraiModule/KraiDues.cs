namespace ProjectK.Common.Rules.KraiModule;

public static class KraiDues
{
    /// <summary>Everything goes up through the станиця, which pays the край itself. How Lilejka began.</summary>
    public const string Stanytsia = "stanytsia";

    /// <summary>The край has no станиці in between: the kurins pay it straight.</summary>
    public const string Krai = "krai";

    /// <summary>Some статті go to the станиця, others to the край on their own transfer.</summary>
    public const string Both = "both";

    public static readonly IReadOnlyList<string> Upstreams = [Stanytsia, Krai, Both];

    public const string Percent = "percent";
    public const string Amount = "amount";
    public const string Exempt = "exempt";

    public static readonly IReadOnlyList<string> ConcessionKinds = [Percent, Amount, Exempt];

    public const string PartStanytsia = "stanytsia";
    public const string PartKrai = "krai";
    public const string PartKurin = "kurin";
    public const string PartGroup = "group";

    public static readonly IReadOnlyList<string> Parts = [PartStanytsia, PartKrai, PartKurin, PartGroup];
}
