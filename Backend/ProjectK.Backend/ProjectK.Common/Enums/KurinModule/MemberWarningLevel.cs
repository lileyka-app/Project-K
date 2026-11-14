namespace ProjectK.Common.Enums.KurinModule;

// Values start at 1 by design: there is no meaningful "no warning" level, and the unset
// default (0) is an invalid state, rejected by Enum.IsDefined in AssignMemberWarningCommand.
// Do not add None = 0 — it would let an unset Level slip past that guard.
public enum MemberWarningLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3
}
