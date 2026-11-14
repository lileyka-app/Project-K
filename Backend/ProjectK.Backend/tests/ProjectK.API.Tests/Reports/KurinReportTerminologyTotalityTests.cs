using ProjectK.Common.Models.Enums;
using ProjectK.Common.Models.Reports;

namespace ProjectK.API.Tests.Reports;

/// <summary>
/// The report's label tables must answer for every enum value.
/// <para>
/// Ступені are no longer one of those tables: the report, the реєстр and its .xlsx all read
/// <see cref="PlastLevelNames"/>, so there is one wording and this asserts it covers the enum.
/// Offices still have a table of their own here. The frontend mirrors both — a new value added on
/// one side and left unnamed on the other would print as its raw enum name, which is what these
/// assertions exist to catch.
/// </para>
/// </summary>
public class KurinReportTerminologyTotalityTests
{
    public static TheoryData<LeadershipRole> LeadershipRoles()
    {
        var data = new TheoryData<LeadershipRole>();
        foreach (var role in Enum.GetValues<LeadershipRole>())
        {
            data.Add(role);
        }

        return data;
    }

    public static TheoryData<PlastLevel> PlastLevels()
    {
        var data = new TheoryData<PlastLevel>();
        foreach (var level in Enum.GetValues<PlastLevel>())
        {
            data.Add(level);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(LeadershipRoles))]
    public void EveryLeadershipRole_HasALabel(LeadershipRole role)
    {
        var label = KurinReportTerminology.LeadershipRole(role);

        Assert.True(label != role.ToString(), $"{role} would print as its enum name in the report.");
    }

    [Theory]
    [MemberData(nameof(PlastLevels))]
    public void EveryPlastLevel_HasALabel(PlastLevel level)
    {
        var label = KurinReportTerminology.PlastLevel(level);

        Assert.True(label != level.ToString(), $"{level} would print as its enum name in the report.");
    }
}
