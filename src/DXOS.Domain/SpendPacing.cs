namespace DXOS.Domain;

public static class SpendPacing
{
    public static decimal DaysUntilEmpty(decimal budget, decimal spent, decimal dailySpend)
    {
        var remaining = budget - spent;
        if (dailySpend <= 0 || remaining <= 0)
        {
            return 0;
        }

        return decimal.Round(remaining / dailySpend, 1, MidpointRounding.AwayFromZero);
    }

    public static int ProjectedLeads(decimal remainingBudget, decimal cpl)
    {
        if (cpl <= 0 || remainingBudget <= 0)
        {
            return 0;
        }

        return (int)decimal.Floor(remainingBudget / cpl);
    }
}
