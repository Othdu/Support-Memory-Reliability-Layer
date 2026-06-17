using SupportMemoryService.Domain;
using SupportMemoryService.Memory;

namespace SupportMemoryService;

// A self-contained verification script, deliberately not xUnit: this project intentionally
// ships with zero NuGet dependencies, so this is the "runnable verification script" the
// brief allows as an alternative to a separate test project. See NEXT.md for the plan to
// promote these into real xUnit tests in a production version.
public static class SelfTest
{
    public static bool Run(MemoryStore store)
    {
        int pass = 0, fail = 0;

        void Check(string name, bool condition)
        {
            if (condition) { pass++; Console.WriteLine($"PASS  {name}"); }
            else { fail++; Console.WriteLine($"FAIL  {name}"); }
        }

        var planFact = store.Facts.FirstOrDefault(f =>
            f.EntityId == "acct_helios_734" && f.Key == "plan" && f.Status == FactStatus.Active);
        Check("Helios active plan is Enterprise Support (high-reliability contract wins)", planFact?.Value == "Enterprise Support");

        var staleStarter = store.Facts.FirstOrDefault(f =>
            f.EntityId == "acct_helios_734" && f.Key == "plan" && f.Value == "Starter" &&
            f.OccurredAt.Month == 4 && f.OccurredAt.Day == 9);
        Check("Later but self-flagged-old Starter claim is marked Stale, not Active", staleStarter?.Status == FactStatus.Stale);

        var seatsFact = store.Facts.FirstOrDefault(f =>
            f.EntityId == "ticket_h_734_p1" && f.Key == "affected_seats" && f.Status == FactStatus.Active);
        Check("Ticket affected_seats resolves to the corrected value 48 (not 42)", seatsFact?.Value == "48");

        var prefFact = store.Facts.FirstOrDefault(f =>
            f.EntityId == "contact_mona_734" && f.Key == "preference" && f.Status == FactStatus.Active);
        Check("Mona's active contact preference is email-only-except-P1 (not WhatsApp)", prefFact?.Value == "email_only_except_p1");

        Check("Idempotency conflict (idem-1008 reused with a different body) is flagged",
            store.Insights.Any(i => i.Kind == InsightKind.IdempotencyConflict &&
                                     i.SourceEventIds.Contains("evt-1008") && i.SourceEventIds.Contains("evt-1009")));

        Check("Text claim 'retry of evt-1004' with a mismatched idempotency_key is flagged, not silently trusted",
            store.Insights.Any(i => i.SourceEventIds.Contains("evt-1008") && i.SourceEventIds.Contains("evt-1004")));

        Check("Shared phone number across multiple contacts is flagged as ambiguous identity",
            store.Insights.Any(i => i.Kind == InsightKind.AmbiguousIdentity &&
                                     i.Description.Contains("+20 10 5555 0142")));

        Check("Delta Stores has no active policy fact derived from the unverified guess",
            !store.Facts.Any(f => f.EntityId == "acct_delta_734" && f.Status == FactStatus.Active &&
                                   (f.Key == "no_training" || f.Key == "no_cross_account_analytics")));

        Check("Delta Stores' unverified guess is surfaced as an UnverifiedClaim insight, not policy",
            store.Insights.Any(i => i.Kind == InsightKind.UnverifiedClaim && i.EntityId == "acct_delta_734"));

        var novaPolicy = store.Facts.FirstOrDefault(f =>
            f.EntityId == "policy_nova_734_privacy" && f.Key == "no_training" && f.Status == FactStatus.Active);
        Check("Nova's no-training policy is Active and scoped to its own policy entity", novaPolicy?.Value == "true");

        Console.WriteLine();
        Console.WriteLine($"Result: {pass} passed, {fail} failed.");
        return fail == 0;
    }
}
