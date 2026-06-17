using System.Text.Json;

namespace SupportMemoryService.Domain;

// The raw, preserved record. Nothing here is ever mutated except the two ingestion-time
// bookkeeping flags below, which exist so downstream layers know whether this record is
// safe to derive facts from.
public sealed class MemoryEvent
{
    public required string EventId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required DateTime OccurredAt { get; init; }
    public required SourceSystem Source { get; init; }
    public required string Actor { get; init; }
    public required EntityType EntityType { get; init; }
    public required string EntityId { get; init; }
    public required List<string> RelatedEntityIds { get; init; }
    public required Reliability Reliability { get; init; }
    public required string Text { get; init; }
    public required JsonElement Payload { get; init; }

    public bool IsSuppressedDuplicate { get; set; }
    public bool IsIdempotencyConflict { get; set; }
}
