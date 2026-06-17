using SupportMemoryService.Domain;
using SupportMemoryService.Extraction;

namespace SupportMemoryService.Memory;

// Picks a winner per (entity, key) using a trust score - NOT "latest wins". Reliability tier
// dominates; an explicit correction or supersede signal can outrank within/across tiers;
// a source that self-flags as old/stale is penalized hard so it can never win even if it is
// chronologically the most recent record. Ties on value get a single winner by recency; ties
// that genuinely disagree are left Ambiguous instead of guessed at.
public static class ConflictResolver
{
    public static List<Fact> Resolve(List<CandidateFact> candidates)
    {
        var facts = new List<Fact>();
        var groups = candidates.GroupBy(c => (c.EntityType, c.EntityId, c.Key));
        int factCounter = 0;

        foreach (var group in groups)
        {
            var scored = group
                .Select(c => (Candidate: c, Score: Score(c)))
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Candidate.OccurredAt)
                .ToList();

            var topScore = scored[0].Score;
            var topTier = scored.Where(x => x.Score == topScore).ToList();
            var topTierDistinctValues = topTier.Select(x => x.Candidate.Value).Distinct().Count();

            if (topTier.Count > 1 && topTierDistinctValues > 1)
            {
                // Two-or-more equally-credible sources disagree. Don't guess - surface both.
                foreach (var (candidate, _) in topTier)
                {
                    facts.Add(BuildFact(ref factCounter, candidate, FactStatus.Ambiguous,
                        "Tied with another equally-credible, conflicting source. Needs human review."));
                }
                foreach (var (candidate, _) in scored.Skip(topTier.Count))
                {
                    facts.Add(BuildFact(ref factCounter, candidate, FactStatus.Superseded,
                        "Outranked by a higher-trust or more recent source for this fact."));
                }
                continue;
            }

            // Clear winner (or a tie on the same value, which isn't a real conflict).
            var winner = scored[0].Candidate;
            facts.Add(BuildFact(ref factCounter, winner, FactStatus.Active,
                "Highest-trust current value for this fact."));

            foreach (var (candidate, _) in scored.Skip(1))
            {
                var status = candidate.IsExplicitlyOld ? FactStatus.Stale : FactStatus.Superseded;
                var reason = candidate.IsExplicitlyOld
                    ? "Source explicitly flags itself as an old/stale note; a higher-trust value is active."
                    : "Outranked by a higher-trust or more recent source for this fact.";
                facts.Add(BuildFact(ref factCounter, candidate, status, reason));
            }
        }

        return facts;
    }

    private static int Score(CandidateFact c)
    {
        int score = c.Reliability switch
        {
            Reliability.High => 30,
            Reliability.Medium => 20,
            Reliability.Low => 10,
            _ => 0
        };

        if (c.IsExplicitCorrection) score += 50; // an explicit human correction is trusted strongly
        if (c.IsExplicitSupersede) score += 15;  // explicit "this supersedes X" signal
        if (c.IsExplicitlyOld) score -= 100;     // self-flagged as outdated - must never win

        return score;
    }

    private static Fact BuildFact(ref int counter, CandidateFact c, FactStatus status, string reason)
    {
        counter++;
        return new Fact
        {
            FactId = $"fact-{c.EntityId}-{c.Key}-{counter}",
            EntityType = c.EntityType,
            EntityId = c.EntityId,
            Key = c.Key,
            Value = c.Value,
            OccurredAt = c.OccurredAt,
            Reliability = c.Reliability,
            SourceEventIds = new List<string> { c.SourceEventId },
            Status = status,
            StatusReason = reason
        };
    }
}
