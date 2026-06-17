# Architecture

## Layers

```
Data/events.json
      |
      v
EventIngestor          raw event -> MemoryEvent, idempotency dedupe/conflict flagging
      |
      v
FactExtractor           MemoryEvent -> CandidateFact (one per tracked payload key)
      |
      v
ConflictResolver         List<CandidateFact> -> List<Fact>  (trust-scored, per entity+key)
      |
      v
MemoryStore               owns Events + Facts + Insights (insight detectors run here too)
      |
      +--> ContextBuilder    compact, evidence-linked summary for one entity
      +--> ExplainService     full reasoning chain for one fact
      +--> ChangeTracker       diff against the last snapshot for one entity
```

Each layer only depends on the one below it. `ContextBuilder`/`ExplainService`/`ChangeTracker`
never touch raw events directly except to quote source text - they read from `Fact`/`Insight`,
which already carry `SourceEventIds` back to the originals.

## Event model

Matches the brief's schema exactly: `event_id`, `idempotency_key`, `occurred_at`, `source`,
`actor`, `entity_type`, `entity_id`, `related_entity_ids`, `reliability`, `text`, `payload`.
Raw events are never mutated except for two ingestion-time bookkeeping flags
(`IsSuppressedDuplicate`, `IsIdempotencyConflict`) that tell later layers whether this record
is safe to derive a fact from. The original record itself - including a flagged one - is
always kept, because the brief explicitly asks to "preserve the raw event records."

## Fact model

A `Fact` is `(EntityType, EntityId, Key, Value)` plus provenance (`SourceEventIds`,
`Reliability`, `OccurredAt`) and a `Status` (`Active`, `Superseded`, `Stale`, `Ambiguous`,
`Contradicted`). Losing candidates for a given `(EntityId, Key)` are **never deleted** - they
stay in the store with a status and a human-readable reason, so a reviewer can always see what
else was considered and why it lost. This is the single biggest design decision in the
project: it directly answers "which facts are contradicted, stale, ambiguous, or unsafe to use
without human review?" without needing a separate audit table.

## Entity / context model

Memory is scoped strictly by `(EntityType, EntityId)`. There is no cross-entity inheritance of
facts - a policy fact on `policy_nova_734_privacy` does not automatically apply to any other
account, even one that "feels similar." `ContextBuilder` additionally pulls in tickets related
to an account via `related_entity_ids` (never via substring-matching IDs, which would be
fragile and wrong here - `ticket_h_734_p1` does not contain `acct_helios_734` as a substring).

## Reliability assumptions

1. **Reliability tier dominates recency.** A later, low-reliability claim should not overturn
   an earlier, high-reliability one. This is modeled as a large score gap between tiers
   (10/20/30) rather than blending recency into the same number, specifically to avoid
   "the newest record always wins" - which is the wrong default for support data, where old
   tickets get reopened, agents make typos, and informal notes lag behind contracts.
2. **Explicit human signals outrank the default tier ordering.** A `correction: true` flag (a
   human explicitly fixing a number) outweighs reliability tier; an explicit `supersedes` key
   is a smaller but real boost. Both are visible in the resulting fact's `StatusReason`.
3. **Self-flagged staleness is trusted.** If a source explicitly says "this may be stale," that
   is itself reliable metadata (about the event, not the fact) and is used to mark the losing
   candidate `Stale` rather than the generic `Superseded` - useful context for a rep deciding
   how much to discount it.
4. **Free text is corroborating evidence, not authoritative.** Structured fields
   (`idempotency_key`, `reliability`, `payload` keys) are trusted over claims made in `text`.
   When the two disagree - as in the `evt-1008` "retry of evt-1004" case - the disagreement
   itself is surfaced as an insight rather than either being silently believed.
5. **Identity is never inferred from a single weak signal.** A shared phone number is a
   reason to flag, not a reason to merge - especially across different accounts, where merging
   risks leaking one account's policy/context into another's.

## What this intentionally does not build

- **No real database.** Everything is rebuilt in memory from `Data/events.json` on each run,
  except the on-disk diff snapshots. Fine for 20 events; not for production volume.
- **No general-purpose fact extraction.** A fixed allowlist of payload keys is used instead of
  NLP/LLM-based extraction from free text. This is a real limitation: any fact that only
  exists in `text` and not in `payload` is invisible to the system today (e.g. "usually
  answers after 4pm Cairo time" is never extracted as a fact, only the `WhatsApp` preference
  is, since that's the only payload key present).
- **No REST API.** A CLI was chosen because it removes a whole layer (hosting, routing,
  serialization concerns) that doesn't change the reliability story the brief is testing for.
- **No real xUnit project.** Avoided specifically to keep the build dependency-free during a
  timed assessment with no guaranteed package-restore access. See `NEXT.md`.
- **No authentication/multi-tenant concerns.** Out of scope for a local CLI memory layer.
- **Ambiguous-tie resolution is generic, and was verified against a real tie, not just left
  on paper.** The seed data's own 20 events happen to have a clean winner in every fact group
  (corrections, supersede signals, or reliability gaps always break the tie). To check the
  `Ambiguous` path actually works rather than just compiling, I injected a 21st synthetic
  event - a second high-reliability `plan` claim for Helios with no corroborating signal -
  and confirmed both competing values came back `Status=Ambiguous` instead of one winning by
  accident of LINQ ordering. See `sample_outputs/diff_demo.txt`, step 3-4, for the real run.
