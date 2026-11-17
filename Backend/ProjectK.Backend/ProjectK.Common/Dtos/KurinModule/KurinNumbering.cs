using ProjectK.Common.Enums.KurinModule;

namespace ProjectK.Common.Dtos.KurinModule;

/// <summary>A гілка and the kurin's number in it, as it is read and shown.</summary>
public sealed record KurinNumbering(KurinBranch Branch, int? Number);
