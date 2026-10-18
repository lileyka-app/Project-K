using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Entities.DuesModule;

/// <summary>
/// One стаття of the kurin's quarterly вкладка, such as «Станична вкладка» or «Табірний фонд»: its
/// amount, its amount with a пільга, and where the money goes. Kept inside its <see cref="KurinDuesRate"/>.
/// </summary>
public class KurinDuesArticle
{
    public string Name { get; set; } = string.Empty;
    public DuesRecipient Recipient { get; set; }
    public decimal Amount { get; set; }

    /// <summary>What a person with a пільга pays for it. Never above <see cref="Amount"/>.</summary>
    public decimal ReducedAmount { get; set; }
}
