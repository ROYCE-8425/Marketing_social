namespace DXOS.Domain;

public sealed record ScoreBreakdown(
    int Behavior,
    int Channel,
    int Campaign,
    int Time,
    int Intent,
    int Total);
