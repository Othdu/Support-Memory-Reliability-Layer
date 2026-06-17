using System.Text.Json;
using SupportMemoryService.Domain;

namespace SupportMemoryService.Extraction;

// A fact before conflict resolution - one candidate value from one source event.
public sealed class CandidateFact
{
    public required EntityType EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required DateTime OccurredAt { get; init; }
    public required Reliability Reliability { get; init; }
    public required string SourceEventId { get; init; }
    public bool IsExplicitCorrection { get; init; }
    public bool IsExplicitlyOld { get; init; }
    public bool IsExplicitSupersede { get; init; }
}

public static class FactExtractor
{
    // Payload keys we treat as fact-worthy. Everything else in the payload is supporting
    // context, not a tracked fact - kept deliberately small for "smallest credible version".
    private static readonly HashSet<string> TrackedKeys = new()
    {
        "plan", "p1_response_hours", "phone_escalation", "preference",
        "affected_seats", "status", "root_cause", "priority",
        "no_training", "no_cross_account_analytics", "scope_account_id",
        "standard_response"
    };

    public static List<CandidateFact> Extract(IEnumerable<MemoryEvent> events)
    {
        var candidates = new List<CandidateFact>();

        foreach (var evt in events)
        {
            // Duplicates and unresolved idempotency conflicts never silently contribute a fact.
            if (evt.IsSuppressedDuplicate || evt.IsIdempotencyConflict)
                continue;

            if (evt.Payload.ValueKind != JsonValueKind.Object)
                continue;

            bool isCorrection = TryGetBool(evt.Payload, "correction");
            bool isOld = TryGetBool(evt.Payload, "old_fact");
            bool isSupersede = evt.Payload.TryGetProperty("supersedes", out _);

            foreach (var prop in evt.Payload.EnumerateObject())
            {
                if (!TrackedKeys.Contains(prop.Name)) continue;

                candidates.Add(new CandidateFact
                {
                    EntityType = evt.EntityType,
                    EntityId = evt.EntityId,
                    Key = prop.Name,
                    Value = ValueToString(prop.Value),
                    OccurredAt = evt.OccurredAt,
                    Reliability = evt.Reliability,
                    SourceEventId = evt.EventId,
                    IsExplicitCorrection = isCorrection,
                    IsExplicitlyOld = isOld,
                    IsExplicitSupersede = isSupersede
                });
            }
        }

        return candidates;
    }

    private static bool TryGetBool(JsonElement payload, string key) =>
        payload.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static string ValueToString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => v.GetRawText()
    };
}
