# Phase 3 domain contracts

Status: complete on branch `codex/rewrite-phase-3`.

Phase 3 introduces immutable semantic opportunity values and a temporary
legacy bridge. The legacy generator, `ResearchOpportunity` save objects,
progress allocation, jobs, UI, and prototyping remain authoritative and
player-visible behavior is unchanged.

## Scope boundary

This phase does not build the Def index or evidence graph, implement new
opportunity rules, normalize or select candidates, enable shadow generation,
migrate saves, replace prototypes, repair the Argonic Core prototype failure,
or add Anomaly Study execution. Those requirements remain assigned to later
phases in `REWRITE_PLAN.md`.

No item is added to `PENDING_RELEASE_NOTES.md` because the change is internal
rewrite infrastructure and is not active in gameplay.

## Pure domain model

The files under `Source/Domain/` compile both into the production assembly and
directly into the independent `net10.0` test project. They reference no
RimWorld, Verse, Unity, Harmony, map, pawn, or UI type.

- `DefIdentity` stores a normalized definition type and `defName` without
  retaining a loaded `Def`. Its canonical value uses NFC Unicode normalization
  and a local UTF-8 percent encoder so formatting does not vary by runtime or
  culture.
- `OpportunityKey` uses the versioned `rr1` format and exactly five semantic
  components: project, opportunity type, research relation, requirement kind,
  and canonical subject. Importance, rare/freebie flags, reasons, progress,
  alternate lists, and object identity do not affect the key.
- `RequirementSpec` represents the seven active legacy requirement forms as
  immutable values: none, thing, terrain, recipe, faction, factionless pawn,
  and schematic/project. Alternate mode is retained for legacy construction;
  alternate identities are copied, deduplicated, and sorted.
- `SubjectEvidence` retains project, subject, relation, role, source Def,
  evidence source, and bounded confidence without consulting game state.
- `GenerationReason` and `RejectionReason` retain structured provenance and
  diagnostic details for later rule and selection phases.
- `OpportunitySpec` is immutable and compares by semantic key. Its reasons are
  snapshotted on construction.
- `OpportunityProgressState` is a separate mutable value keyed by
  `OpportunityKey`. It is deliberately not serializable or registered with the
  game; Phase 8 still owns the saved-state schema and migration policy.

The existing `ResearchRelation` type moved to the pure domain source folder but
keeps the same public namespace and enum values.

## Temporary legacy bridge

`LegacyOpportunityAdapter` converts an existing valid
`ResearchOpportunity` into an `OpportunitySpec` with a legacy provenance
reason. It can also resolve a specification's project, opportunity type, and
requirement through `IResearchRuntimeServices` and construct the corresponding
legacy `ResearchOpportunity`. Missing loaded Defs produce a structured
`RejectionReason` rather than a partially initialized opportunity.

The adapter covers every active legacy requirement comp. Read-only primary-Def
accessors were added to the terrain and recipe comps so conversion does not use
reflection. Existing factories still construct legacy opportunities directly;
no active generator, execution, allocation, or serialization call site was
switched in this phase.

## Independent tests

`DomainContractsTests` adds eight focused tests to the Phase 2 project. The 38
total tests cover:

- equality and exact formatting of stable semantic keys;
- sensitivity to every declared key component and intentional independence
  from alternates, scores, flags, reasons, and progress;
- Unicode normalization and deterministic delimiter escaping;
- immutable, sorted, deduplicated alternate subjects;
- independent mutable progress and immutable specifications;
- rejection of a supplied key that disagrees with its semantic fields;
- value equality and provenance retention for evidence and diagnostic reasons;
- conversion of all 579 Phase 0 generator opportunities across all seven
  legacy requirement kinds, including the canonical
  `TableButcher` / `AncientTableButcher` alternate fixture.

The existing 30 characterization and deferred-regression tests continue to
pass. The independent project still has no RimWorld or production-assembly
reference.

## Verification evidence

Pre-edit evidence:

- Phase 3 started from clean commit
  `ae258621617eb8c9dcc5843dbc16e98751c8100b` and created
  `codex/rewrite-phase-3`;
- shared `modctl doctor`, `inspect`, and `validate` ran before editing, with
  validation reporting zero errors and zero warnings;
- dependency resolution selected SDK `10.0.302`, `Krafs.Rimworld.Ref`
  `1.6.4871`, `Lib.Harmony.Ref` `2.4.2`,
  `Microsoft.NETFramework.ReferenceAssemblies.net472` `1.0.3`,
  `Microsoft.NET.Test.Sdk` `18.9.0`, xUnit `2.9.3`, and runner `4.0.0`;
- no RimWorld process was running.

Final commands and results:

- `scripts/test.ps1 Release`: 38 passed, 0 failed, independently runnable;
- `scripts/check.ps1`: 129 XML files parsed, tests passed, and quiet Debug and
  Release production builds passed;
- Debug DLL: 420,864 bytes, SHA-256
  `15685AE427B4D86ECDB2438EE2E1D24983AADFD30BD146C6AD4C508C72FB887B`;
- Release DLL: 379,904 bytes, SHA-256
  `7394B83BB2A9197CEFECAD131607A8CE043CAB628305F68E06F11972C00CCEFF`;
- assembly identity: `ResearchReinvented, Version=1.0.0.0`, file version
  `1.6.0.0`, product version `1.6`;
- package identity remains `PeteTimesSix.ResearchReinvented`;
- `scripts/package.ps1 --no-build`: produced a 181-entry public ZIP whose
  packaged DLL hash equals the verified Release DLL;
- package SHA-256:
  `78C2EBC9CF6D28EAD17615C4E74F403A6236C9EC1015FFB6A26F6118922BAB87`;
- the public package retained `About/PublishedFileId.txt`, contained zero
  forbidden source/test/symbol/Harmony/SDK entries, and passed shared
  `modctl audit-package`.

No deploy command was run, and no live RimWorld installation was modified.

## Exit criteria

- The domain contracts compile and run independently without maps, pawns, UI,
  Harmony, or any other game-global dependency.
- Equal semantic opportunities produce equal, stable `rr1` keys, with every
  declared identity component covered by tests.
- Every current legacy requirement can cross the adapter boundary in both
  directions when its loaded Def still exists.
- Runtime execution and saved progress remain on the legacy model.

Phase 4 may build the immutable Def index and provenance graph as a separate
change set while keeping the legacy generator active.
