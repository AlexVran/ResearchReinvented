# Phase 9 analysis and clinical execution

Status: complete on branch `codex/rewrite-phase-9`.

Phase 9 routes existing ordinary research actions through a registered activity
handler and the Phase 8 per-game opportunity service. Stable `OpportunityKey`
values cross the handler boundary; mutable progress remains owned by
`OpportunityService`. Legacy `ResearchOpportunity` projections remain the
RimWorld-facing adapter for current jobs, requirements, UI, and prototypes.

This phase does not migrate UI or settings, replace prototype selection or
prototype state, repair Argonic Core, add Fishing or other curated
opportunities, add Anomaly Study integration, perform Phase 12 hardening,
deploy, package, release, or publish.

## Handler registry

`OpportunityActivityRegistry` is a pure, independently tested registry. A
handler selects stable keys from immutable specifications for one project. New
handlers can register without adding a service method or editing a central
switch. The built-in stable IDs cover:

- theory;
- bench, in-place, and terrain analysis;
- social research;
- tending and surgery medicine as separately checked handlers;
- self-ingestion and observed ingestion;
- books; and
- tooling.

Duplicate IDs are rejected. Missing handlers return a bounded unavailable
result. Disabled handlers retain their reason. Exceptions in startup checks or
queries quarantine only that handler and produce a diagnostic; specifications,
saved progress, generic handlers, and other registered activities remain
available. Version-sensitive private targets used by books, ingestion,
surgery, and tooling have handler-level startup self-checks.

The registry depends only on `IOpportunityService.SpecificationsFor`. The
service remains unaware of handler IDs, loaded maps, Harmony targets, job
drivers, or curated activities. A synthetic optional handler test proves that a
new activity can select and advance a stable key without changing the service.

## RimWorld execution adapter

Each `ResearchOpportunityManager` owns one `ResearchExecutionService`. It maps
registered stable keys to the current legacy projections at the RimWorld
boundary and exposes deterministic queries for the active or an explicitly
named project. Book queries can generate specifications for a non-active
project without changing the active project or its UI cache.

The migrated paths are:

- ordinary research-bench theory work;
- hauled bench analysis, in-place field analysis, and terrain analysis;
- tending and medicine consumed during surgery;
- ingestion and observed drug trials;
- books;
- recipe-bench tooling;
- faction lecture dialogs and remote lecture jobs;
- brainstorm interactions; and
- prisoner teaching and interrogation work queries.

Every migrated lookup is ordered by stable key and returns one intended match
per handler/subject query. Continuous jobs retain their current-project failure
condition. After save/load they resolve the same semantic key again from their
job target. Event handlers query at the moment of the event and therefore do
not retain stale projection references.

Prototype selection deliberately continues through the legacy manager query
methods. Its progress calls now pass through the centralized progress adapter,
but Phase 9 does not reinterpret prototype transitions or saved artifacts.

## Per-map indexes and invalidation

The former static WorkGiver opportunity dictionaries and single-opportunity
caches are removed. Analysis subject lookup uses a revisioned index owned by
the per-game execution service and partitioned by `Map`, stable handler ID, and
semantic Def identity. The index stores static subject matches, while dynamic
availability is checked on every query.

Specification reconciliation, project generation, project completion, reset,
and settings-driven cache clearing explicitly invalidate the index. Bench and
map-thing inventories rebuild when either their tick changes or the execution
revision changes. Missing dictionary keys return an empty result instead of
throwing, addressing the failure shape reported in upstream issue #33.

## Central progress path

`ResearchExecutionService` now owns the common application sequence for
continuous and chunk progress:

1. research-capability check;
2. Intellectual XP;
3. chunk or tick amount;
4. storyteller research speed;
5. category speed;
6. researcher tech-level cost factor;
7. fast-research debug factor;
8. remaining-opportunity cap;
9. authoritative service application;
10. pawn research record and progress mote;
11. vanilla project progress; and
12. project completion.

`OpportunityProgressMath` independently tests the modifiers, remaining cap,
fast-research multiplier, mote cap, and invalid-factor rejection. Legacy
`ResearchOpportunity.ResearchTickPerformed`, `ResearchChunkPerformed`, and
`FinishImmediately` remain compatibility wrappers over this one path, so
deferred prototype calls cannot create a second progress implementation.

## Independent headless evidence

Eleven Phase 9 tests cover:

- a synthetic optional handler advancing service state without service changes;
- missing, disabled, failed, and startup-disabled handlers;
- deterministic filtering and duplicate-key suppression;
- unique stable IDs for every built-in migrated handler;
- exactly one stable opportunity advanced by one handler application;
- explicit per-owner/per-map index invalidation and isolation;
- project switching without returning a prior project's key;
- save/load during a continuous activity and stable-key resumption;
- duplicate handler registration rejection; and
- centralized modifier, cap, mote, and invalid-input math.

Together with Phases 2-8, the independent Release suite passes 110 tests.

## Verification evidence

Final pre-documentation binary evidence for this Phase 9 change set:

- `dotnet test ... Release`: 110 passed, 0 failed;
- `scripts/build.py Debug`: passed; DLL size 685,568 bytes, SHA-256
  `A5D5AFAA366BAC2677F1FBA0232BA37487E2F7A53DC0D98887068A8B8F24FE29`;
- `scripts/build.py Release`: passed; DLL size 630,272 bytes, SHA-256
  `3BAB685528976D9FAA4DD4893ED1ADE4CB6C4E9F0B11168582A1000D9C53EABC`;
- package ID remains `PeteTimesSix.ResearchReinvented`;
- assembly identity remains `ResearchReinvented, Version=1.0.0.0`, file version
  `1.6.0.0`, product version `1.6`; and
- source audits find no migrated WorkGiver static opportunity cache and no
  ordinary execution use of legacy manager filters. Remaining legacy filters
  are confined to `PrototypeUtilities`, owned by Phase 11;
- `scripts/check.py`: 129 XML files parsed, tests and both builds passed; and
- shared `modctl inspect` and `validate`: clean RimWorld 1.6 repository shape,
  zero errors and warnings. (`inspect` correctly reports the uncommitted Phase
  9 worktree as dirty before the commit.)

No RimWorld process was running during implementation. No deployment command
was run. The original checkout and Workshop copy were not modified. Live-game
scenario execution remains a Phase 14 release-readiness activity because this
phase was not authorized to deploy and the shared doctor reports no local GABS
or RimBridgeServer checkout.

## Exit criteria

- Every migrated action resolves stable keys through a registered handler.
- A synthetic optional activity can report progress without a new service API
  or central-switch edit.
- Missing, disabled, and failed handlers are bounded and retain generic state.
- No legacy static opportunity cache remains on migrated WorkGiver paths.
- Project changes explicitly invalidate map indexes; continuous jobs retain
  their project-change failure conditions.
- Save/load resumes the same semantic opportunity in independent tests.
- Phase 10 may begin only as a separate change set.
