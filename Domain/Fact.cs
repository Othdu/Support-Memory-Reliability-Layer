namespace SupportMemoryService.Domain;

// A derived claim, scoped to one entity, always traceable back to source events.
// Losing candidates for the same (entity, key) are kept around with a Status + reason
// instead of being deleted, so conflicts are visible rather than silently flattened.
public sealed class Fact
{
    public required string FactId { get; init; }
    public required EntityType EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required DateTime OccurredAt { get; init; }
    public required Reliability Reliability { get; init; }
    public required List<string> SourceEventIds { get; init; }
    public FactStatus Status { get; set; } = FactStatus.Active;
    public string? StatusReason { get; set; }
}
