namespace ProjectK.Common.Dtos.MemberModule;

/// <summary>
/// One folder on the shelf: what it is called and how much is in it. The index is answered without
/// opening anything, so a caller can see what a person has before deciding what to read.
/// </summary>
public sealed record MemberDossierFolder(string Name, int Count);
