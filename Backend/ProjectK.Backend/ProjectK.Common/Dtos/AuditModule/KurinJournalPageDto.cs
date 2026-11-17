namespace ProjectK.Common.Dtos.AuditModule;

/// <summary>One page of the kurin's journal, newest first.</summary>
/// <param name="Items">The entries.</param>
/// <param name="HasMore">Whether an older page follows.</param>
public sealed record KurinJournalPageDto(IReadOnlyList<KurinJournalEntryDto> Items, bool HasMore);
