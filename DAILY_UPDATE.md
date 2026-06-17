# Daily Update

**Branch:** `master`
**Commit range:** `47d26e8..ddc63fc`

## Files changed

- `SupportMemoryService.csproj` - net8.0 console app, zero NuGet dependencies
- `Data/events.json` - the 20 seed events from the brief
- `Domain/` - `MemoryEvent`, `Fact`, `Insight`, and the shared enums
- `Ingestion/EventDto.cs`, `Ingestion/EventIngestor.cs` - JSON load + idempotency dedupe/conflict detection
- `Extraction/FactExtractor.cs` - candidate fact extraction from tracked payload keys
- `Memory/ConflictResolver.cs` - trust-scored fact resolution (Active/Superseded/Stale/Ambiguous)
- `Memory/IdentityAmbiguityDetector.cs` - shared phone/email -> flagged, never auto-merged
- `Memory/MemoryStore.cs` - wires ingestion + extraction + resolution + insight detectors together
- `Context/ContextBuilder.cs` - compact, evidence-linked context per entity
- `Context/ExplainService.cs` - full reasoning chain for a single fact
- `Context/ChangeTracker.cs` - snapshot + diff per entity (the mid-test scope change)
- `Program.cs` - CLI command router
- `SelfTest.cs` - 10-check built-in verification script
- `README.md`, `ARCHITECTURE.md`, `NEXT.md` - documentation
- `sample_outputs/` - real captured output from `context`, `explain`, `diff`, and `insights`

## What shipped

A working CLI covering every core requirement in the brief: raw event preservation, fact
derivation linked to source events, entity-scoped memory, duplicate/contradiction/staleness/
ambiguity detection, compact evidence-linked context, a per-fact explain view, and idempotency
handling (both the clean suppressed-retry case and the deliberate same-key-different-body
conflict case). The mid-test "what changed since last context build" question is fully
implemented, not just designed.

## Tests / results

`dotnet run -- selftest`: **10/10 passed.** Covers the two hardest judgment calls in the brief
- that Helios's plan correctly resolves to the high-reliability "Enterprise Support" over a
later but self-flagged-old "Starter" claim (reliability beats recency), and that Delta Stores'
unverified policy guess is surfaced but never promoted to an active policy fact.

`dotnet build`: 0 warnings, 0 errors.

## Blockers

None blocking. One known limitation carried into `NEXT.md`: fact extraction only looks at a
fixed allowlist of payload keys, not free text, so a few human-readable details (e.g. "usually
answers after 4pm Cairo time") never become structured facts in this slice.

## Next step

Capture this AI chat log link and push the repo, including `sample_outputs/`. If time remains,
add a synthetic test case that actually exercises the `Ambiguous` tie-break path in
`ConflictResolver`, since the seed data doesn't happen to produce a real tie.
