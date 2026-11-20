using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ProjectK.Infrastructure.Migrations;

namespace ProjectK.Infrastructure.Tests.Persistence;

/// <summary>
/// The waiting state of a ступінь arrives on a table full of ступені that were always confirmed: each
/// of them must stay so, and the migration must survive running on a base that already has it.
/// </summary>
public class PlastLevelConfirmationMigrationTests
{
    private static List<string> UpSql() =>
        new AddPlastLevelConfirmation().UpOperations.OfType<SqlOperation>().Select(op => op.Sql).ToList();

    [Fact]
    public void Up_LeavesEveryExistingRowConfirmed()
    {
        var awaits = Assert.Single(UpSql(), sql => sql.Contains("[AwaitsConfirmation]", StringComparison.Ordinal));

        Assert.Contains("NOT NULL", awaits, StringComparison.Ordinal);
        Assert.Contains("DEFAULT CAST(0 AS bit)", awaits, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_GuardsEveryColumn_SoItRunsTwice()
    {
        var sql = UpSql();

        Assert.Equal(3, sql.Count);
        Assert.All(sql, statement => Assert.StartsWith("IF COL_LENGTH('PlastLevelHistories',", statement.Trim(), StringComparison.Ordinal));
    }

    [Fact]
    public void Down_GuardsEveryDrop()
    {
        var down = Assert.Single(new AddPlastLevelConfirmation().DownOperations.OfType<SqlOperation>()).Sql;

        Assert.Equal(3, down.Split("IF COL_LENGTH", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, down.Split("DROP COLUMN", StringSplitOptions.None).Length - 1);
    }
}
