using System.Text;
using SupportMemoryService.Domain;
using SupportMemoryService.Memory;

namespace SupportMemoryService.Context;

public static class ExplainService
{
    public static string Explain(MemoryStore store, string factId)
    {
        var fact = store.Facts.FirstOrDefault(f => f.FactId == factId);
        if (fact is null)
            return $"No fact found with id '{factId}'. Use 'facts <entityId>' to list fact ids first.";

        var sb = new StringBuilder();
        sb.AppendLine($"Explain: {fact.FactId}");
        sb.AppendLine($"  Entity: {fact.EntityId} ({fact.EntityType})");
        sb.AppendLine($"  Key: {fact.Key}   Value: {fact.Value}   Status: {fact.Status}");
        sb.AppendLine($"  Why: {fact.StatusReason}");
        sb.AppendLine();
        sb.AppendLine("  All sources considered for this fact key (most to least trusted):");

        var siblings = store.Facts
            .Where(f => f.EntityId == fact.EntityId && f.Key == fact.Key)
            .OrderByDescending(f => f.Status == FactStatus.Active)
            .ThenByDescending(f => f.OccurredAt)
            .ToList();

        foreach (var s in siblings)
        {
            var evt = store.Events.FirstOrDefault(e => e.EventId == s.SourceEventIds.First());
            sb.AppendLine($"   - [{s.Status}] {s.Value}  (event {string.Join(",", s.SourceEventIds)}, " +
                           $"{s.Reliability} reliability, {s.OccurredAt:yyyy-MM-dd})" +
                           (evt is not null ? $" -- \"{evt.Text}\"" : ""));
        }

        return sb.ToString();
    }

    public static string ListFacts(MemoryStore store, string entityId)
    {
        var sb = new StringBuilder();
        var facts = store.Facts
            .Where(f => f.EntityId == entityId)
            .OrderBy(f => f.Key)
            .ThenByDescending(f => f.Status == FactStatus.Active);

        foreach (var f in facts)
            sb.AppendLine($"{f.FactId}\t[{f.Status}]\t{f.Key} = {f.Value}");

        return sb.ToString();
    }
}
