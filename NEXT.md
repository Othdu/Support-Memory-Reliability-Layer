# Next steps (if this became production)

In priority order:

1. **Real persistence.** Move from in-memory rebuild to EF Core + SQL Server/Postgres (this
   matches my existing stack - Repository Pattern + Service Layer over EF Core, same shape as
   my ECommerceAPI/SchoolAPI projects). Raw events become an append-only table; facts become a
   derived, rebuildable table keyed by `(EntityType, EntityId, Key)` with a status column.

2. **Real tests.** Promote `SelfTest.cs`'s 10 checks into an xUnit project
   (`SupportMemoryService.Tests`), plus add coverage for the genuinely untested `Ambiguous`
   branch in `ConflictResolver` (construct a synthetic tie that the seed data doesn't produce)
   and for malformed/missing payload fields.

3. **Smarter fact extraction.** Replace the fixed payload-key allowlist with a small
   classifier or LLM-assisted extraction step that can also pull structured claims out of
   `text` (e.g. "usually answers after 4pm Cairo time"), with the same trust-scoring pipeline
   downstream so the reliability story doesn't change, only the extraction surface grows.

4. **A real API surface.** Wrap the same `ContextBuilder`/`ExplainService` behind a minimal
   ASP.NET Core Web API so the AI assistant Lina's team is building can call it directly
   instead of shelling out to a CLI. JWT auth + role-based access (account manager vs admin)
   would gate which entities a caller can query.

5. **Confidence decay over time.** Right now staleness is only detected when a source
   self-flags as old. A production version should decay trust for facts past some age
   (tunable per `Key`, e.g. a `plan` fact should decay slower than a `phone_escalation`
   on-call status) so old-but-not-self-flagged facts get downgraded automatically.

6. **Better diff/digest design for "what changed."** The current `ChangeTracker` diffs a
   flat snapshot file per entity. At scale this should become an event-sourced changelog (a
   `FactChangeLog` table written whenever `ConflictResolver` flips a fact's Active winner),
   so "what changed since X" can answer for *any* prior point in time, not just "since the
   last time someone happened to run a diff."

7. **Identity resolution beyond exact phone/email match.** Real contact dedup needs fuzzy
   matching (name similarity, shared account, time-correlated activity) with a confidence
   score, not just "do these two strings match exactly." The current detector is deliberately
   conservative (never merges) - the next step is making it smarter, while staying just as
   conservative about auto-merging.
