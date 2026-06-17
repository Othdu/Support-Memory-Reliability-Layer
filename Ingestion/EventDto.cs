using System.Text.Json;
using System.Text.Json.Serialization;

namespace SupportMemoryService.Ingestion;

public sealed class EventDto
{
    [JsonPropertyName("event_id")] public string EventId { get; set; } = "";
    [JsonPropertyName("idempotency_key")] public string IdempotencyKey { get; set; } = "";
    [JsonPropertyName("occurred_at")] public DateTime OccurredAt { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    [JsonPropertyName("actor")] public string Actor { get; set; } = "";
    [JsonPropertyName("entity_type")] public string EntityType { get; set; } = "";
    [JsonPropertyName("entity_id")] public string EntityId { get; set; } = "";
    [JsonPropertyName("related_entity_ids")] public List<string> RelatedEntityIds { get; set; } = new();
    [JsonPropertyName("reliability")] public string Reliability { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("payload")] public JsonElement Payload { get; set; }
}
