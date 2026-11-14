using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Dtos.ProbesAndBadgesModule;

/// <summary>
/// Whom the file may name as its Звʼязковий and впорядник: the Звʼязкові of the person's kurin and
/// the впорядники of their гурток first, then of the kurin. Names only; sent to whoever may write it.
/// </summary>
public sealed record ThirdProbeMentorOptionsDto(IReadOnlyList<string> Zvyazkovi, IReadOnlyList<string> Vporiadnyky);
