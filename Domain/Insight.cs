namespace SupportMemoryService.Domain;

// Something the system noticed that a rep or reviewer needs to see, but that does NOT
// reduce cleanly to a single (entity, key) fact - duplicate retries, idempotency conflicts,
// ambiguous identity matches, unverified claims.
public sealed class Insight
{
    public required InsightKind Kind { get; init; }
    public required string Description { get; init; }
    public required List<string> SourceEventIds { get; init; }
    public string? EntityId { get; init; }
}
