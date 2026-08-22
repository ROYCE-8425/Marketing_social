namespace DXOS.Infrastructure.Persistence.Entities;

public sealed class SalesAssignmentState
{
    public int Id { get; set; }
    public string LastAssignedActor { get; set; } = string.Empty;
    public string SalesActors { get; set; } = string.Empty;
}
