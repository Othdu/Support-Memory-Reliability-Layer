# Support Memory Reliability Layer

A small CLI that ingests raw support/CRM/contract events, derives trust-scored facts from
them, keeps everything scoped by entity and traceable back to source events, and surfaces
duplicates, conflicts, stale data, and ambiguous identity matches instead of silently
flattening them.

## Why this design

The brief asks for the smallest credible version that proves architecture, reliability
thinking, and product judgment - not a production service. So this ships as a single console
app with **zero external NuGet packages** (only the .NET base class library / `System.Text.Json`).
That was a deliberate tradeoff: it removes any risk of a package-restore failure costing time
under a hard 3-hour clock, and it keeps the whole reasoning pipeline (ingest → extract →
resolve → context/explain) readable in one sitting. EF Core, a real database, and xUnit are
the obvious next upgrades - see `NEXT.md`.

## Requirements

The shipped project targets `net9.0` to match a typical current .NET toolchain. Everything
was actually built, compiled, and run end-to-end against a .NET **8** SDK in the sandbox I
built this in (it had no network access to fetch a .NET 9 SDK or any NuGet package - more on
that below). The code itself has zero version-specific syntax, so if `dotnet build` complains
about a missing SDK/runtime on your machine, just change `<TargetFramework>` in
`SupportMemoryService.csproj` to whatever major version you have (`net8.0` is known-good).
No internet access is needed to build or run either way.

## Setup & run

```bash
cd SupportMemoryService
dotnet build
dotnet run -- selftest                          # run the built-in verification checks
dotnet run -- context acct_helios_734            # compact, evidence-linked account context
dotnet run -- facts acct_helios_734               # list fact ids for an entity
dotnet run -- explain fact-acct_helios_734-plan-1  # why the system believes a specific fact
dotnet run -- diff acct_helios_734                 # what changed since the last context build
dotnet run -- insights                             # all flagged duplicates/conflicts/ambiguity
dotnet run -- ingestion-notes                      # raw ingestion-time dedupe/conflict log
```

Seed data lives in `Data/events.json` (the 20 events from the brief, verbatim) and is loaded
automatically - no separate ingest step needed for this slice.

## Design summary

**Pipeline:** `EventIngestor` loads the raw JSON and does idempotency-key bookkeeping (see
below) → `FactExtractor` pulls candidate facts out of a small allowlist of payload keys per
event → `ConflictResolver` picks one Active value per `(entity, key)` using a trust score, not
recency alone → `MemoryStore` also runs a handful of insight detectors (duplicate hints,
unverified claims, identity ambiguity, idempotency conflicts) → `ContextBuilder` and
`ExplainService` read from that store on demand. `ChangeTracker` snapshots each entity's
belief-state to disk so a later run can diff against it.

**Trust scoring (the core reliability decision):** each candidate fact starts from its source
event's reliability tier (`high=30, medium=20, low=10`). An explicit `correction: true` in the
payload adds +50 (a human correction should win outright). An explicit `supersedes` key adds
+15. A self-flagged `old_fact: true` subtracts 100, so it can never win even if it is the most
recent record chronologically. This is what makes the Helios plan resolve correctly: the
April 2 contract addendum (high reliability) beats both the original Starter record (medium,
earlier) **and** a later, low-reliability note that explicitly calls itself possibly stale -
pure "latest wins" logic would have gotten this wrong.

**Idempotency handling:** every event's `idempotency_key` is tracked. Same key + identical
body = suppressed duplicate retry (kept in the raw log, contributes no fact). Same key +
different body = a flagged `IdempotencyConflict` insight - both raw records are kept, neither
is auto-merged, and it surfaces in `insights` and in `context` for the affected entity.

**The deliberate trap in the seed data:** `evt-1008`'s free text claims to be "a retry of
evt-1004 with the same idempotency key," but its actual `idempotency_key` field
(`idem-1008`) does not match `evt-1004`'s (`idem-1004`). The structured field is trusted over
the free-text claim, and the mismatch itself is surfaced as a `PossibleDuplicate` insight
rather than silently accepted. This is exercised by `selftest` and visible in
`sample_outputs/context_acct_helios_734.txt`.

**Identity ambiguity:** contacts that share a phone number or email are flagged, never merged.
This catches both the Mona Salem / M. Salem case and the Mona / Omar Adel (different account!)
shared-phone case - merging across accounts here would also risk leaking Nova's
no-cross-account-analytics policy onto Helios data, so the conservative default matters.

**Policy scoping:** policy facts only ever apply to the account in their own
`scope_account_id`. Delta Stores' low-reliability, no-contract guess (`evt-1012`) is surfaced
as an `UnverifiedClaim` insight, never promoted to an active policy fact - confirmed by
`selftest`.

**Mid-test scope change ("what changed since the last context build?"):** implemented, not
just designed. `ChangeTracker` snapshots each entity's Active/Ambiguous fact set to
`snapshots/<entity>.json` and diffs against it on the next `diff <entityId>` call.

## Tradeoffs / what's intentionally small

- Fact extraction works off a fixed allowlist of payload keys rather than general NLP - fine
  for 20 seed events, would need a smarter extraction strategy at real scale (see `NEXT.md`).
- No persistence beyond the JSON event file and the diff snapshots - everything else is
  rebuilt in memory on every run. Fine for this slice; a production version needs real storage.
- Tests are a hand-built `selftest` command, not xUnit, specifically so this never depends on
  a NuGet restore succeeding during the assessment window. See `NEXT.md` for the upgrade path.

## Verification

`dotnet run -- selftest` runs 10 checks against the seed data, including the two hardest
judgment calls in the brief (reliability-over-recency for Helios's plan, and not promoting
Delta Stores' unverified guess to policy). All 10 pass as of this submission.
