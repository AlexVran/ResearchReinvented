# Phase 8 opportunity service and versioned save state

Status: complete on branch `codex/rewrite-phase-8`.

Phase 8 makes immutable rewrite specifications and `OpportunityKey` progress
authoritative through one per-game service. Existing jobs, Harmony event paths,
UI widgets, availability checks, and prototype execution continue to consume
legacy `ResearchOpportunity` projections. Those projections no longer own saved
progress.

This phase does not migrate execution or work-giver caches, introduce an
activity-handler registry, change UI/settings architecture, replace prototypes,
repair Argonic Core, add Fishing or other curated opportunities, add Anomaly
Study integration, perform Phase 12/13 hardening, deploy, package, release, or
publish.

## Authority boundary

`OpportunityService` is pure and compiled directly into the independent test
assembly. The per-game `ResearchOpportunityManager` owns exactly one instance.
The service owns:

- active immutable specifications;
- mutable progress indexed by stable `rr1` `OpportunityKey` values;
- active and previously generated project identities;
- category budgets and category totals;
- deterministic reconciliation, aliases, diagnostics, and orphan retention;
- versioned save snapshots; and
- separate ordinary and special/prototype progress collections.

`OpportunitySpecificationPipeline` runs the immutable Def index, Phase 5 rule
registry, and Phase 6 selector. It then creates legacy projections for existing
execution and UI. Category budgets and maximum progress retain the legacy
allocation formulas. Static selection completes before a current-game faction
is projected. A specification that cannot currently be projected remains in the
service with state and allocation metadata; availability cannot remove it from
the static candidate set.

`ResearchOpportunity` retains its public/runtime shape. Once bound to an
authoritative key, its progress reads and applications delegate to the service.
The category availability calculation likewise reads the service's matched plus
safe orphan aggregate instead of summing mutable legacy objects.

The service accepts arbitrary supplied `OpportunitySpec` values and knows
nothing about execution handlers. That preserves the future seam for registered
activity handlers and curated opportunities without implementing the Phase 9
registry or Phase 13 content in this phase.

## Save schema

The manager writes one `opportunityState` root at schema version 1. Its records
store project, type, relation, requirement kind, canonical subject, category,
alternates, maximum/current progress, and the stable key as strings rather than
loaded `Def` references. Definitions supplied by removed mods therefore remain
recoverable data.

The schema deliberately does not save generated specifications. It marks that
static specification regeneration is required after load; progress is reconciled
after that deterministic pass. No ordinary load path force-resets or regenerates
progress. A successful legacy migration rewrites only the new root on the next
save and never writes `_allGeneratedOpportunities`,
`_allProjectsWithGeneratedOpportunities`, `currentProject`, or
`_categoryStores` again.

A schema newer than version 1 is retained as a read-only snapshot. The older
service neither applies progress nor permits a save that would overwrite the
future root. Invalid old schema numbers and malformed progress are salvaged
conservatively: valid records continue, bad numeric credit becomes a diagnosed
zero-credit orphan, and the original semantic identifiers remain saved.

## Legacy migration and reconciliation

Old deep-saved opportunity objects are read only when `opportunityState` is
absent. During `LoadingVars`, the raw XML strings are captured before cross-ref
resolution can discard a missing Def. Each object is converted to an explicit
`LegacyOpportunityMigrationDto` with no runtime object or loaded-Def reference.
The DTO list is one-way input and is discarded after reconciliation.

Reconciliation is stable under reversed source/specification order:

1. exact `OpportunityKey` wins;
2. project, type, relation, and requirement kind must agree after explicit alias
   canonicalization;
3. canonical-subject equality wins over alternate-family membership;
4. a remaining tie uses stable-key order;
5. duplicate records mapping to one key sum progress without truncating it to a
   newly smaller maximum; and
6. unmatched records retain their full semantic fields and safe category credit
   as orphans.

Legacy current-game faction references are normalized to the Phase 5 static
player/non-player semantic families. A removed project that cannot be
regenerated is explicitly moved to orphan state. A malformed record with no
usable project identity is also retained instead of being dropped. If the mod or
Def later returns, the next load can reconcile that saved orphan normally.

## Prototype and special state

Progress for prototype and other special opportunities uses a distinct
`specialState` migration/save collection. It is not folded into ordinary
progress or used to redesign prototype execution. Existing prototype thing
references and terrain grids remain owned and serialized independently by
`PrototypeKeeper` and `PrototypeTerrainGrid`; Phase 8 does not cancel, replace,
or reinterpret them. The Phase 0 fixtures contain no active prototype object,
so live prototype-object migration remains the already documented Phase 11/14
evidence gap.

## Independent headless evidence

Seventeen Phase 8 test cases cover:

- new-schema round trips and stable keys;
- both Phase 0 old-save fixtures and all 195 legacy records;
- the partial `VFE_CoffeeTable` ancestor progress;
- exact category totals and saved budgets;
- settings-driven budget/maximum changes without progress reset;
- project switching followed by save/load;
- removed mods, missing projects/Defs, unparseable legacy state, and safe orphan
  credit;
- explicit project/type/subject rename aliases;
- strict relation and requirement matching;
- input-order-independent reconciliation and snapshots;
- duplicate semantic state without truncation;
- malformed and future schema versions;
- idempotent migrate-save-load-reconcile behavior;
- separate special/prototype state; and
- acceptance of future curated or handler-backed specifications without any
  handler knowledge in the service.

Together with Phases 2-7, the independent Release suite passes 99 tests.

## Verification evidence

Final repository evidence is recorded at the committed Phase 8 revision:

- `scripts/test.py Release`: 99 passed, 0 failed;
- `scripts/build.py Debug`: passed; DLL size 665,088 bytes, SHA-256
  `DE84FEBB6C23E24211D820527F0CE3899CBD49791B6E25D1B50676FE812EE04F`;
- `scripts/build.py Release`: passed; DLL size 611,328 bytes, SHA-256
  `469162010F2A4E27D07D7F4A27019E47590CE79EF29A43ABE2A4CB62A051FD7E`;
- `scripts/check.py`: 129 XML files parsed, tests and both builds passed;
- shared `modctl inspect` and `validate`: clean RimWorld 1.6 repository, zero
  errors and warnings;
- package ID remains `PeteTimesSix.ResearchReinvented`;
- assembly identity remains `ResearchReinvented, Version=1.0.0.0`, file version
  `1.6.0.0`, product version `1.6`; and
- source audits confirm no handler registry, Fishing/curated mapping, Anomaly
  Study path, Phase 9 execution migration, Phase 10 UI migration, Phase 11
  prototype replacement, deployment, release, or publishing operation.

No RimWorld process was running during Phase 8 work. No deployment command was
run, and the original checkout and Workshop copy were not modified.

## Exit criteria

- One per-game service is authoritative for specifications and mutable state.
- Old fixtures migrate without exceptions or silent research loss.
- Saving after successful migration writes only schema version 1.
- Ordinary load regeneration occurs only because schema version 1 deliberately
  regenerates deterministic static specifications.
- Runtime execution/UI and prototype internals remain on their existing paths.
- Phase 9 may begin only as a separate change set.
