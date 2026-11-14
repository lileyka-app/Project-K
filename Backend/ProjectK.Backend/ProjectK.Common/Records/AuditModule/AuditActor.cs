namespace ProjectK.Common.Records.AuditModule;

/// <summary>Who did it, as a trail or the kurin's journal will remember them.</summary>
/// <param name="UserKey">The account; null when nobody is signed in.</param>
/// <param name="Name">The name a person will read.</param>
/// <param name="Role">The office that authorised the action.</param>
public sealed record AuditActor(Guid? UserKey, string? Name, string Role);
