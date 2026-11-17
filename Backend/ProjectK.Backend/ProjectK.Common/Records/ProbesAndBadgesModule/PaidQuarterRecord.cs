namespace ProjectK.Common.Records.ProbesAndBadgesModule;

/// <summary>A quarter of вкладка that one membership closed with nothing owed, and the day it closed.</summary>
public sealed record PaidQuarterRecord(Guid MembershipKey, int QuarterIndex, DateOnly LastDay);
