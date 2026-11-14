namespace ProjectK.Common.Records.AgendaModule;

/// <summary>One materialised occurrence of a (possibly recurring) item.</summary>
public readonly record struct AgendaOccurrence(DateTime StartUtc, DateTime? EndUtc);
