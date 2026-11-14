using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.DuesModule.Requests;

/// <summary>One стаття of the вкладка: its name, where the money goes, and what it costs.</summary>
public sealed class DuesArticleRequest
{
    public string Name { get; set; } = string.Empty;
    public DuesRecipient Recipient { get; set; }
    public decimal Amount { get; set; }

    /// <summary>With a пільга. Left out, it is the full amount.</summary>
    public decimal? ReducedAmount { get; set; }
}
