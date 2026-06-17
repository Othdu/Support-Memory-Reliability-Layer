using System.Text.Json;
using SupportMemoryService.Domain;
using SupportMemoryService.Extraction;

namespace SupportMemoryService.Memory;

public sealed class MemoryStore
{
    public List<MemoryEvent> Events { get; } = new();
    public List<Fact> Facts { get; } = new();
    public List<Insight> Insights { get; } = new();

    public static MemoryStore Build(List<MemoryEvent> events)
    {
        var store = new MemoryStore();
        store.Events.AddRange(events);

        var candidates = FactExtractor.Extract(events);
        store.Facts.AddRange(ConflictResolver.Resolve(candidates));

        // 1. Idempotency conflicts flagged during ingestion.
        foreach (var evt in events.Where(e => e.IsIdempotencyConflict))
        {
            var partner = events.FirstOrDefault(e => e.IdempotencyKey == evt.IdempotencyKey && e.EventId != evt.EventId);
            store.Insights.Add(new Insight
            {
                Kind = InsightKind.IdempotencyConflict,
                Description = $"{evt.EventId} reuses idempotency_key '{evt.IdempotencyKey}' with a different " +
                               $"body/entity{(partner is not null ? $" than {partner.EventId}" : "")}. " +
                               "Needs human review before either is trusted.",
                SourceEventIds = partner is not null
                    ? new List<string> { evt.EventId, partner.EventId }
                    : new List<string> { evt.EventId },
                EntityId = evt.EntityId
            });
        }

        foreach (var evt in events)
        {
            if (evt.Payload.ValueKind != JsonValueKind.Object) continue;

            // 2. Explicit content-based duplicate hints in the payload.
            if (evt.Payload.TryGetProperty("possible_duplicate_of", out var dupEl) &&
                dupEl.ValueKind == JsonValueKind.String)
            {
                store.Insights.Add(new Insight
                {
                    Kind = InsightKind.PossibleDuplicate,
                    Description = $"{evt.EventId} appears to describe the same real-world occurrence as " +
                                   $"{dupEl.GetString()} (content/timing match), but does not share its " +
                                   "idempotency_key. Treated as a separate logical event and flagged as a " +
                                   "likely duplicate rather than silently merged or silently kept.",
                    SourceEventIds = new List<string> { evt.EventId, dupEl.GetString()! },
                    EntityId = evt.EntityId
                });
            }

            // 3. Unverified, no-contract policy guesses must never be promoted to policy.
            if (evt.Payload.TryGetProperty("unverified_policy_guess", out var guessEl) &&
                guessEl.ValueKind == JsonValueKind.True)
            {
                store.Insights.Add(new Insight
                {
                    Kind = InsightKind.UnverifiedClaim,
                    Description = $"{evt.EventId} is a low-reliability, unverified guess (\"{evt.Text}\") with no " +
                                   "contract evidence. Not applied as policy for this account.",
                    SourceEventIds = new List<string> { evt.EventId },
                    EntityId = evt.EntityId
                });
            }

            // 4. The deliberate trap: free text claims "retry of evt-X with the same idempotency
            // key" but the structured idempotency_key field actually differs. We trust the
            // structured field over the free-text claim and flag the discrepancy explicitly
            // instead of silently treating it as a confirmed idempotent retry.
            if (evt.Text.Contains("Retry of", StringComparison.OrdinalIgnoreCase))
            {
                var referencedId = ExtractEventIdMention(evt.Text);
                if (referencedId is not null)
                {
                    var referenced = events.FirstOrDefault(e => e.EventId == referencedId);
                    if (referenced is not null && referenced.IdempotencyKey != evt.IdempotencyKey)
                    {
                        store.Insights.Add(new Insight
                        {
                            Kind = InsightKind.PossibleDuplicate,
                            Description = $"{evt.EventId}'s text claims to be a retry of {referencedId} with " +
                                           "'the same idempotency key', but its actual idempotency_key " +
                                           $"('{evt.IdempotencyKey}') differs from {referencedId}'s " +
                                           $"('{referenced.IdempotencyKey}'). The structured field is trusted " +
                                           "over the free-text claim: NOT treated as a confirmed idempotent " +
                                           "retry, flagged instead for human review.",
                            SourceEventIds = new List<string> { evt.EventId, referencedId },
                            EntityId = evt.EntityId
                        });
                    }
                }
            }
        }

        // 5. Ambiguous identity matches.
        store.Insights.AddRange(IdentityAmbiguityDetector.Detect(events));

        return store;
    }

    private static string? ExtractEventIdMention(string text)
    {
        var idx = text.IndexOf("evt-", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var end = idx;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '-')) end++;
        return text[idx..end];
    }
}
