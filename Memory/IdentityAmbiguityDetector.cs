using System.Text.Json;
using SupportMemoryService.Domain;

namespace SupportMemoryService.Memory;

// Surfaces "these might be the same person, but don't assume it" cases. Deliberately
// conservative: it never merges anything, it only flags shared identifiers for human review.
public static class IdentityAmbiguityDetector
{
    public static List<Insight> Detect(List<MemoryEvent> events)
    {
        var insights = new List<Insight>();

        var contactEvents = events
            .Where(e => e.EntityType == EntityType.Contact && !e.IsSuppressedDuplicate)
            .ToList();

        var byPhone = new Dictionary<string, List<(string ContactId, string EventId)>>();
        var byEmail = new Dictionary<string, List<(string ContactId, string EventId)>>();

        foreach (var e in contactEvents)
        {
            if (e.Payload.ValueKind != JsonValueKind.Object) continue;

            if (e.Payload.TryGetProperty("phone", out var phoneEl) && phoneEl.ValueKind == JsonValueKind.String)
            {
                var phone = phoneEl.GetString()!;
                if (!byPhone.TryGetValue(phone, out var list)) byPhone[phone] = list = new();
                list.Add((e.EntityId, e.EventId));
            }

            if (e.Payload.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String)
            {
                var email = emailEl.GetString()!;
                if (!byEmail.TryGetValue(email, out var list)) byEmail[email] = list = new();
                list.Add((e.EntityId, e.EventId));
            }

            // An explicit textual warning is a strong corroborating signal on its own.
            if (e.Payload.TryGetProperty("warning", out var warnEl) && warnEl.ValueKind == JsonValueKind.String)
            {
                insights.Add(new Insight
                {
                    Kind = InsightKind.AmbiguousIdentity,
                    Description = $"Event explicitly warns of ambiguous identity ({warnEl.GetString()}): \"{e.Text}\"",
                    SourceEventIds = new List<string> { e.EventId },
                    EntityId = e.EntityId
                });
            }
        }

        foreach (var (phone, owners) in byPhone)
        {
            var distinctContacts = owners.Select(o => o.ContactId).Distinct().ToList();
            if (distinctContacts.Count > 1)
            {
                insights.Add(new Insight
                {
                    Kind = InsightKind.AmbiguousIdentity,
                    Description = $"Phone number {phone} is shared by contacts: {string.Join(", ", distinctContacts)}. " +
                                   "Do not merge without additional evidence.",
                    SourceEventIds = owners.Select(o => o.EventId).Distinct().ToList(),
                    EntityId = null
                });
            }
        }

        foreach (var (email, owners) in byEmail)
        {
            var distinctContacts = owners.Select(o => o.ContactId).Distinct().ToList();
            if (distinctContacts.Count > 1)
            {
                insights.Add(new Insight
                {
                    Kind = InsightKind.AmbiguousIdentity,
                    Description = $"Email {email} is shared by contacts: {string.Join(", ", distinctContacts)}. " +
                                   "Do not merge without additional evidence.",
                    SourceEventIds = owners.Select(o => o.EventId).Distinct().ToList(),
                    EntityId = null
                });
            }
        }

        return insights;
    }
}
