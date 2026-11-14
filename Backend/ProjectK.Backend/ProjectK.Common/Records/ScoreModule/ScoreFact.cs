using ProjectK.Common.Enums.ScoreModule;

namespace ProjectK.Common.Records.ScoreModule;

/// <summary>
/// A fact another module knows that is worth points — a вмілість confirmed, a quarter paid. The
/// score module asks for these; it does not read the tables they live in.
/// </summary>
/// <param name="Variant">The level for a пересторога; 0 otherwise.</param>
public sealed record ScoreFact(Guid MembershipKey, ScoreSource Source, int Variant, DateOnly On);
