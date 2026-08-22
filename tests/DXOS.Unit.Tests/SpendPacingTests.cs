using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class SpendPacingTests
{
    [Fact]
    public void DaysUntilEmpty_UsesLinearPacing()
    {
        Assert.Equal(3m, SpendPacing.DaysUntilEmpty(budget: 30_000_000, spent: 0, dailySpend: 10_000_000));
        Assert.Equal(0m, SpendPacing.DaysUntilEmpty(30_000_000, 30_000_000, 10_000_000));
        Assert.Equal(0m, SpendPacing.DaysUntilEmpty(30_000_000, 0, 0));
    }

    [Fact]
    public void ProjectedLeads_DividesRemainingBudgetByCpl()
    {
        Assert.Equal(50, SpendPacing.ProjectedLeads(10_000_000, 200_000));
        Assert.Equal(0, SpendPacing.ProjectedLeads(10_000_000, 0));
    }
}
