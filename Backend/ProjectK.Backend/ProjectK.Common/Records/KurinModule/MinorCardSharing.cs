using ProjectK.Common.Enums.MemberModule;

namespace ProjectK.Common.Records.KurinModule;

/// <summary>What a kurin lets its people under 18 do with their card: share it, which parts, and whether by a permanent link.</summary>
public sealed record MinorCardSharing(Guid KurinKey, bool MayShare, CardField Fields, bool MayLink);
