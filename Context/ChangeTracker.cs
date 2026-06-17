using System.Text;
using System.Text.Json;
using SupportMemoryService.Domain;
using SupportMemoryService.Memory;

namespace SupportMemoryService.Context;

// Answers "what changed since the last context build?" by snapshotting the active+flagged
// fact set per entity to disk, then diffing against it on the next run. First run for an
// entity is the baseline; nothing to compare against yet.
public static class ChangeTracker
{
    private static string SnapshotDir => Path.Combine(Directory.GetCurrentDirectory(), "snapshots");

    public static string DiffAndSnapshot(MemoryStore store, string entityId)
    {
        Directory.CreateDirectory(SnapshotDir);
        var path = Path.Combine(SnapshotDir, $"{Sanitize(entityId)}.json");

        // Scope the snapshot to current *belief* a rep would actually see (Active, plus
        // Ambiguous since that's still surfaced as an unresolved live answer). Superseded/
        // Stale entries are settled history and not worth diffing on every run.
        var current = store.Facts
            .Where(f => f.EntityId == entityId && (f.Status == FactStatus.Active || f.Status == FactStatus.Ambiguous))
            .OrderBy(f => f.Key).ThenBy(f => f.Value)
            .Select(f => new SnapshotFact { Key = f.Key, Value = f.Value, Status = f.Status })
            .ToList();

        var currentJson = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });

        var sb = new StringBuilder();

        if (!File.Exists(path))
        {
            File.WriteAllText(path, currentJson);
            sb.AppendLine($"No previous snapshot for {entityId}. This context build is the baseline. Snapshot saved.");
            return sb.ToString();
        }

        var previousJson = File.ReadAllText(path);
        var previous = JsonSerializer.Deserialize<List<SnapshotFact>>(previousJson) ?? new();

        // GroupBy (not ToDictionary) deliberately: a key CAN legitimately appear more than
        // once in this scope (e.g. two tied Ambiguous facts for the same key), so a flat
        // key->value map would crash. We diff per-key sets of (Value, Status) instead.
        var prevByKey = previous.GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.Select(f => (f.Value, f.Status)).ToHashSet());
        var currByKey = current.GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.Select(f => (f.Value, f.Status)).ToHashSet());

        sb.AppendLine($"Changes since last context build for {entityId}:");
        bool any = false;

        foreach (var key in currByKey.Keys.Union(prevByKey.Keys).OrderBy(k => k))
        {
            var hasPrev = prevByKey.TryGetValue(key, out var prevSet);
            var hasCurr = currByKey.TryGetValue(key, out var currSet);

            if (hasCurr && !hasPrev)
            {
                sb.AppendLine($"  + NEW: {key} = {Describe(currSet!)}");
                any = true;
            }
            else if (!hasCurr && hasPrev)
            {
                sb.AppendLine($"  - REMOVED: {key} (was {Describe(prevSet!)})");
                any = true;
            }
            else if (hasPrev && hasCurr && !prevSet!.SetEquals(currSet!))
            {
                sb.AppendLine($"  ~ CHANGED: {key}: {Describe(prevSet)} -> {Describe(currSet!)}");
                any = true;
            }
        }

        if (!any)
            sb.AppendLine("  (no changes)");

        File.WriteAllText(path, currentJson);
        return sb.ToString();
    }

    private static string Describe(HashSet<(string Value, FactStatus Status)> set) =>
        string.Join(" | ", set.Select(s => $"{s.Value} [{s.Status}]"));

    private static string Sanitize(string entityId) => entityId.Replace("/", "_").Replace("\\", "_");

    private sealed class SnapshotFact
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public FactStatus Status { get; set; }
    }
}
