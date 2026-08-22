namespace DXOS.Domain;

public static class SalesRoundRobin
{
    public static string? Next(IReadOnlyList<string> salesActors, string? lastAssignedActor)
    {
        var roster = salesActors
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .Select(actor => actor.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (roster.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(lastAssignedActor))
        {
            return roster[0];
        }

        var currentIndex = roster.FindIndex(actor => string.Equals(actor, lastAssignedActor.Trim(), StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            return roster[0];
        }

        return roster[(currentIndex + 1) % roster.Count];
    }
}
