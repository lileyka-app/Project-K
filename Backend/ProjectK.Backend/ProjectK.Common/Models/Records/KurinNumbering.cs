using ProjectK.Common.Models.Enums;

namespace ProjectK.Common.Models.Records;

/// <summary>A гілка and the kurin's number in it, as it is read and shown.</summary>
public sealed record KurinNumbering(KurinBranch Branch, int? Number);
