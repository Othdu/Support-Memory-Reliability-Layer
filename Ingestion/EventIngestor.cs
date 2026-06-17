using System.Text.Json;
using SupportMemoryService.Domain;

namespace SupportMemoryService.Ingestion;

public sealed class IngestionResult
{
    public List<MemoryEvent> Events { get; } = new();
    public List<string> Notes { get; } = new();
}

public static class EventIngestor
{
    public static IngestionResult Ingest(string jsonFilePath)
    {
        var json = File.ReadAllText(jsonFilePath);
        var dtos = JsonSerializer.Deserialize<List<EventDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<EventDto>();

        var result = new IngestionResult();

        // idempotency_key -> (event_id that first used it, content fingerprint)
        var seenKeys = new Dictionary<string, (string EventId, string Fingerprint)>();

        foreach (var dto in dtos)
        {
            var evt = new MemoryEvent
            {
                EventId = dto.EventId,
                IdempotencyKey = dto.IdempotencyKey,
                OccurredAt = dto.OccurredAt,
                Source = ParseSource(dto.Source),
                Actor = dto.Actor,
                EntityType = ParseEntityType(dto.EntityType),
                EntityId = dto.EntityId,
                RelatedEntityIds = dto.RelatedEntityIds,
                Reliability = ParseReliability(dto.Reliability),
                Text = dto.Text,
                Payload = dto.Payload
            };

            var fingerprint = Fingerprint(dto);

            if (seenKeys.TryGetValue(evt.IdempotencyKey, out var prior))
            {
                if (prior.Fingerprint == fingerprint)
                {
                    // True idempotent retry: same key, identical body. Keep the raw record
                    // for audit purposes, but it must never contribute a second logical fact.
                    evt.IsSuppressedDuplicate = true;
                    result.Notes.Add($"{evt.EventId}: suppressed as duplicate retry of {prior.EventId} " +
                                      $"(idempotency_key={evt.IdempotencyKey}, identical body).");
                }
                else
                {
                    // Same idempotency key, different body. This is the dangerous case -
                    // we do NOT silently pick a winner. Both raw events are preserved, both
                    // are flagged, and neither contributes a fact until a human resolves it.
                    evt.IsIdempotencyConflict = true;
                    result.Notes.Add($"{evt.EventId}: IDEMPOTENCY CONFLICT with {prior.EventId} - same " +
                                      $"idempotency_key ({evt.IdempotencyKey}) but different body/entity. " +
                                      "Flagged for human review, not auto-merged.");
                }
            }
            else
            {
                seenKeys[evt.IdempotencyKey] = (evt.EventId, fingerprint);
            }

            result.Events.Add(evt);
        }

        return result;
    }

    private static string Fingerprint(EventDto dto) =>
        $"{dto.EntityType}|{dto.EntityId}|{dto.Text}|{dto.Payload.GetRawText()}";

    private static SourceSystem ParseSource(string s) => s switch
    {
        "crm" => SourceSystem.Crm,
        "contract" => SourceSystem.Contract,
        "chat" => SourceSystem.Chat,
        "email" => SourceSystem.Email,
        "agent_note" => SourceSystem.AgentNote,
        "system" => SourceSystem.System,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown source")
    };

    private static EntityType ParseEntityType(string s) => s switch
    {
        "account" => EntityType.Account,
        "contact" => EntityType.Contact,
        "ticket" => EntityType.Ticket,
        "policy" => EntityType.Policy,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown entity_type")
    };

    private static Reliability ParseReliability(string s) => s switch
    {
        "high" => Reliability.High,
        "medium" => Reliability.Medium,
        "low" => Reliability.Low,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown reliability")
    };
}
