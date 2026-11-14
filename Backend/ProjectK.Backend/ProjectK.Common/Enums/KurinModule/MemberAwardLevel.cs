namespace ProjectK.Common.Enums.KurinModule;

// Values start at 1 by design: there is no meaningful "no award" level, so the unset
// default (0) is an invalid state rather than a valid member. Do not add None = 0.
public enum MemberAwardLevel
{
    First = 1,
    Second = 2,
    Third = 3,
    Fourth = 4
}
