using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Dtos.DuesModule;

public sealed record DuesArticleDto
{
    public required string Name { get; init; }
    public required DuesRecipient Recipient { get; init; }
    public required decimal Amount { get; init; }
    public required decimal ReducedAmount { get; init; }
}
