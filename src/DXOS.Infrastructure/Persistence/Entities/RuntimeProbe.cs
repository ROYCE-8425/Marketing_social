namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class RuntimeProbe
{
    public Guid Id { get; set; }
    public string ProbeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
