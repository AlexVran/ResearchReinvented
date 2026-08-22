# Phase 11 prototype containment

Status: complete on branch `codex/rewrite-phase-11`.

Phase 11 contains prototype lifecycle behind a per-game service, moves actual
artifact references to map-owned state, replaces the Argonic-sensitive
construction completion transpiler, and makes project-switch cancellation
operate only on artifacts explicitly tracked for the previous project.

Opportunity generation, state, execution, and the main UI retain their Phase
8-10 authorities. This phase does not perform heavy-list compatibility work,
add curated opportunities or Fishing, integrate Anomaly Study, replace the
general execution registry, deploy, release, or publish.

## State and save boundary

`IPrototypeService` is the pure per-game semantic authority. Each artifact uses
an `rrp1` key containing its research project, map identity, artifact kind, and
stable RimWorld artifact identity. Records optionally retain the authoritative
`OpportunityKey` and subject Def name without retaining a loaded Def or runtime
object.

Prototype schema v1 writes `prototypeState` through explicit save DTOs. Records
are deterministically ordered, duplicate keys are merged by terminal-state
precedence, malformed records are retained as non-active orphans, and a future
schema is preserved read-only. A future prototype schema disables prototype
mutation only; it does not disable the ordinary opportunity service.

Legacy `_prototypes` references remain a read-only migration input. On load they
are moved into their map component and reconciled into semantic records once a
current project is available. New saves write semantic schema v1 plus the
map-owned references and no longer write the legacy game-wide collection.

`PrototypeTerrainGrid` remains the compatible map component and now owns its
map's prototype Thing references as well as terrain/foundation grids. The game
component no longer holds a `Dictionary<Map, ...>` or a global Thing set.

## Transition inventory

| Boundary | Phase 11 behavior | Evidence |
| --- | --- | --- |
| Designator visibility | A narrow postfix exposes a build designator only when the service-backed prototype availability query permits it. | Runtime contract test and 1.6 reference build |
| Bill visibility | Production rows use the per-game availability index; experimental surgery starts from authoritative `PrototypeSurgery` specifications. | Runtime contract test and 1.6 reference build |
| Blueprint placement | `GenConstruct.PlaceBlueprintForBuild` postfixes register the returned blueprint by stable key. | Lifecycle test and 1.6 reference build |
| Blueprint to frame | Prefix captures the blueprint key and postfix transitions that exact record to the returned frame. | Lifecycle test and runtime contract test |
| Unfinished item | Recipe toil initialization registers the unfinished item against the recipe opportunity. | Lifecycle test and 1.6 reference build |
| Completed product | Recipe completion transitions an unfinished item when present, otherwise registers the product once; construction completion locates the spawned product after all original/modded work and transitions the captured frame. | Lifecycle/idempotence tests and runtime contract test |
| Terrain/foundation | Completion marks the map-owned cell and writes a completed semantic record; normal replacement clears stale prototype flags. | Lifecycle/save round-trip tests and 1.6 reference build |
| Surgery | Bill creation registers a surgery bill; successful iteration completes it and awards the existing prototype opportunity once. Candidate recipes come from current authoritative specs, then `AvailableReport`, missing ingredients, body parts, and `AvailableOnNow` are queried for the current patient. | Lifecycle and surgery-discovery contract tests |
| Success/failure | Success produces a completed terminal record; failure captures work before destruction, writes a failed terminal record, and grants the existing partial-work credit. | Lifecycle, terminal-state, and save/load tests |
| Cancellation | Thing destruction and bill deletion convert tracked records; completion cannot be downgraded by later cleanup deletion. | Cancellation and terminal-precedence tests |
| Project switch | With cancellation enabled, only active stable keys owned by the previous project are destroyed/deleted. Completed products and terrain remain; ordinary bills are never selected. With the existing preservation setting enabled, no artifacts are cancelled. | Project-switch test and source contract |
| Save/load | Schema v1 round-trips active and terminal state deterministically; malformed, duplicate, old, and future data follow explicit conservative policies. | Round-trip, duplicate, malformed/future, and idempotence tests |

## Argonic Core regression

The old `Frame.CompleteConstruction` integration depended on a precise
`GenSpawn.Spawn`/`Pop` instruction sequence, hard-coded local slots, and two
process-wide static handoff fields. Argonic Core changes the IL directly after
spawn, so the old matcher omitted prototype completion and research credit.

The replacement is a normal prefix/postfix pair. The prefix captures the frame
key, project-owned prototype decision, map, cell, target definition, and work in
Harmony's invocation-local `__state`. The postfix runs after the complete
original/modded method, locates the finished target at the captured cell,
transitions exactly one frame record to the completed product, applies prototype
degradation, and awards the existing opportunity. It neither inspects local
slots nor assumes any instruction after `GenSpawn.Spawn`, so Argonic's material
transfer remains part of the original method and attached to the product.

If a changed game/mod shape leaves no matching product, only construction
prototype completion/credit is disabled for that game and one actionable
warning is emitted. Ordinary research remains available. The remaining
construction failure-chance transpiler matches the semantic stat call, obtains
the frame from the generated closure rather than a local slot, and similarly
degrades only that optional modifier.

## Cancellation policy and issue #20

The legacy switch path derived a broad Def set from opportunities belonging to
other projects and deleted every matching blueprint, frame, unfinished item,
and bill on every map. That could select ordinary player bills, matching the
failure described by upstream issue #20.

Phase 11 cancellation asks `IPrototypeService` for active records owned by the
previous project and resolves only those exact stable keys. It never searches
all generated opportunity Defs. The policy is:

- active blueprint, frame, unfinished-item, and prototype-bill records may be
  cancelled when the player has enabled prototype cancellation;
- completed products, completed terrain/foundation, failed records, and prior
  cancellations are retained;
- ordinary untracked bills and artifacts are never removed; and
- the existing `disablePrototypeBillCancellation` setting remains authoritative
  and preserves all artifacts when enabled.

## Independent headless evidence

Eighteen Phase 11 tests cover stable keys and map separation; blueprint,
frame, unfinished-item, product, terrain, foundation, ordinary bill, and surgery
transitions; successful, failed, cancelled, and completed terminal states;
project switching; new-save round trips; deterministic duplicate handling;
malformed records and schema versions; future-schema read-only preservation;
idempotent save/load; one-warning feature isolation; ordinary research with
prototyping disabled; the Argonic-resistant prefix/postfix contract; map-owned
serialization; exact-key cancellation; authoritative surgery discovery; and
the absence of fixed local slots in remaining prototype transpilers.

Together with Phases 2-10, the Release suite passes 135 tests.

## Verification evidence

- `dotnet test ... Release`: 135 passed, 0 failed;
- `scripts/check.py`: XML, tests, and Debug/Release builds passed;
- Debug DLL: 724,480 bytes, SHA-256
  `0AA963AF595445302B204B646E0BEF075A851E246C988DC171064CB40A084207`;
- Release DLL: 668,160 bytes, SHA-256
  `6638CFAC82069D2B87A4081C365F89C188AB311F4681AD2129D686AA79F1CC43`;
- package ID remains `PeteTimesSix.ResearchReinvented`;
- assembly identity remains `ResearchReinvented, Version=1.0.0.0`, file version
  `1.6.0.0`, product version `1.6`;
- source audits find no `Frame.CompleteConstruction` transpiler, spawn/`Pop`
  matcher, shared completion handoff, broad `defsToCancel` scan, process-wide
  prototype opportunity cache, or hard-coded local slot in the prototype
  integration; and
- shared `modctl inspect` and `validate` report zero errors and warnings.

The installed Krafs 1.6.4871 reference assembly proves all changed RimWorld and
Harmony-facing code compiles. The required doctor found no local
DecompilerServer, GABS, or RimBridgeServer installation, so no decompiler or
live-game claim is made. No RimWorld process was running and no deployment
command was run. Live Argonic material-retention, visual labels, and gameplay
save/load remain Phase 14 validation evidence.

## Exit criteria

- Ordinary research remains independently operational when prototyping or an
  individual prototype feature is disabled.
- Prototype lifecycle and save state are behind a versioned per-game service,
  while references and terrain flags are map-owned.
- Construction completion no longer depends on Argonic-sensitive IL shape or
  hard-coded locals and fails feature-locally with one warning.
- Every Phase 11 transition has independent headless evidence and a compiled
  RimWorld adapter.
- Phase 12 may begin only as a separate change set.
