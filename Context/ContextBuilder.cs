using System.Text;
using SupportMemoryService.Domain;
using SupportMemoryService.Memory;

namespace SupportMemoryService.Context;

public static class ContextBuilder
{
    public static string Build(MemoryStore store, string entityId)
    {
        var sb = new StringBuilder();
        var entityEvents = store.Events.Where(e => e.EntityId == entityId).ToList();
        var entityType = entityEvents.Select(e => e.EntityType).FirstOrDefault();

        sb.AppendLine($"Context for entity: {entityId} ({entityType})");
        sb.AppendLine(new string('-', 60));

        var activeFacts = store.Facts
            .Where(f => f.EntityId == entityId && f.Status == FactStatus.Active)
            .OrderBy(f => f.Key)
            .ToList();

        sb.AppendLine("Active facts:");
        if (activeFacts.Count == 0)
            sb.AppendLine("  (none derived for this entity)");
        foreach (var f in activeFacts)
            sb.AppendLine($"  - {f.Key} = {f.Value}  [source: {string.Join(",", f.SourceEventIds)}, reliability: {f.Reliability}]");

        var flaggedFacts = store.Facts
            .Where(f => f.EntityId == entityId && f.Status != FactStatus.Active)
            .OrderBy(f => f.Key)
            .ToList();

        if (flaggedFacts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Other facts considered but NOT active (superseded / stale / ambiguous):");
            foreach (var f in flaggedFacts)
                sb.AppendLine($"  - [{f.Status}] {f.Key} = {f.Value}  ({f.StatusReason})  [source: {string.Join(",", f.SourceEventIds)}]");
        }

        // Related tickets - found via related_entity_ids, not substring matching on the id.
        var relatedTickets = store.Events
            .Where(e => e.EntityType == EntityType.Ticket && e.RelatedEntityIds.Contains(entityId))
            .Select(e => e.EntityId)
            .Distinct()
            .ToList();

        if (relatedTickets.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Related tickets:");
            foreach (var ticketId in relatedTickets)
            {
                var ticketFacts = store.Facts.Where(f => f.EntityId == ticketId && f.Status == FactStatus.Active);
                sb.AppendLine($"  - {ticketId}: " + string.Join(", ", ticketFacts.Select(f => $"{f.Key}={f.Value}")));
            }
        }

        var relevantInsights = store.Insights
            .Where(i => i.EntityId == entityId ||
                        i.SourceEventIds.Any(id => entityEvents.Any(e => e.EventId == id)) ||
                        relatedTickets.Any(t => i.SourceEventIds.Any(sid =>
                            store.Events.Any(e => e.EventId == sid && e.EntityId == t))))
            .ToList();

        if (relevantInsights.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Flags - things needing human review before acting on this context:");
            foreach (var insight in relevantInsights)
                sb.AppendLine($"  - [{insight.Kind}] {insight.Description}");
        }

        return sb.ToString();
    }
}
