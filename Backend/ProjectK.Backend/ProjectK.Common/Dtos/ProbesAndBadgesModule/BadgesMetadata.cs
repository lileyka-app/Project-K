namespace ProjectK.Common.Dtos.ProbesAndBadgesModule;

public sealed record BadgesMetadata(
    string ParserVersion,
    string ToolAuthor,
    string ParserComment,
    DateTimeOffset? ParsedAtUtc,
    string SourceUrl,
    bool FixerEnabled,
    string FixerMode,
    int TotalBadges);
