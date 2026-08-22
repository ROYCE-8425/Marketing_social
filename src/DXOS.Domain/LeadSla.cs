namespace DXOS.Domain;

public static class LeadSla
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan HotDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan WarmDuration = TimeSpan.FromMinutes(30);

    public static TimeSpan? GetDuration(LeadLabel label)
    {
        return label switch
        {
            LeadLabel.Hot => HotDuration,
            LeadLabel.Warm => WarmDuration,
            _ => null
        };
    }

    public static bool IsExpired(DateTimeOffset startedAtUtc, DateTimeOffset nowUtc)
    {
        return nowUtc - startedAtUtc >= Duration;
    }

    public static bool IsExpired(DateTimeOffset startedAtUtc, DateTimeOffset nowUtc, LeadLabel label)
    {
        var duration = GetDuration(label);
        if (duration is null)
        {
            return false;
        }

        return nowUtc - startedAtUtc >= duration.Value;
    }
}
