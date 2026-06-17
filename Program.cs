using SupportMemoryService.Context;
using SupportMemoryService.Ingestion;
using SupportMemoryService.Memory;

namespace SupportMemoryService;

public static class Program
{
    public static int Main(string[] args)
    {
        var dataPath = FindEventsFile();
        if (dataPath is null)
        {
            Console.WriteLine("Could not find Data/events.json. Run this from the project root.");
            return 1;
        }

        var ingestion = EventIngestor.Ingest(dataPath);
        var store = MemoryStore.Build(ingestion.Events);

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        switch (command)
        {
            case "context":
                if (args.Length < 2) { Console.WriteLine("Usage: context <entityId>"); return 1; }
                Console.WriteLine(ContextBuilder.Build(store, args[1]));
                break;

            case "facts":
                if (args.Length < 2) { Console.WriteLine("Usage: facts <entityId>"); return 1; }
                Console.WriteLine(ExplainService.ListFacts(store, args[1]));
                break;

            case "explain":
                if (args.Length < 2) { Console.WriteLine("Usage: explain <factId>"); return 1; }
                Console.WriteLine(ExplainService.Explain(store, args[1]));
                break;

            case "diff":
                if (args.Length < 2) { Console.WriteLine("Usage: diff <entityId>"); return 1; }
                Console.WriteLine(ChangeTracker.DiffAndSnapshot(store, args[1]));
                break;

            case "insights":
                if (store.Insights.Count == 0)
                {
                    Console.WriteLine("No flagged insights.");
                }
                else
                {
                    foreach (var insight in store.Insights)
                        Console.WriteLine($"[{insight.Kind}] {insight.Description}");
                }
                break;

            case "ingestion-notes":
                if (ingestion.Notes.Count == 0)
                    Console.WriteLine("No ingestion notes (no duplicate/conflicting idempotency keys found).");
                foreach (var note in ingestion.Notes)
                    Console.WriteLine(note);
                break;

            case "selftest":
                return SelfTest.Run(store) ? 0 : 1;

            default:
                PrintUsage();
                return 1;
        }

        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Support Memory Reliability Layer - CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  context <entityId>     Compact, evidence-linked context for an account/contact/ticket");
        Console.WriteLine("  facts <entityId>        List fact ids for an entity (active + superseded/stale/ambiguous)");
        Console.WriteLine("  explain <factId>        Show why the system believes a specific fact");
        Console.WriteLine("  diff <entityId>         Show what changed since the last context build for this entity");
        Console.WriteLine("  insights                List all flagged conflicts / duplicates / ambiguous identities");
        Console.WriteLine("  ingestion-notes         List ingestion-time duplicate/idempotency-conflict notes");
        Console.WriteLine("  selftest                Run built-in verification checks against the seed data");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- context acct_helios_734");
        Console.WriteLine("  dotnet run -- facts acct_helios_734");
        Console.WriteLine("  dotnet run -- explain fact-acct_helios_734-plan-2");
        Console.WriteLine("  dotnet run -- diff acct_helios_734");
    }

    private static string? FindEventsFile()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "events.json"),
            Path.Combine(AppContext.BaseDirectory, "Data", "events.json")
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
