# Phase 4 immutable Def index and evidence graph

Status: complete on branch `codex/rewrite-phase-4`.

Phase 4 adds a session-immutable view of loaded definition facts and their
provenance. It does not generate new opportunities, select candidates, migrate
saves, change execution or UI, replace prototypes, repair Argonic Core, or add
Anomaly Study integration. The legacy `MasterFactory` and legacy saved
`ResearchOpportunity` objects remain authoritative and player-visible.

No item is added to `PENDING_RELEASE_NOTES.md` because the index is inactive
rewrite infrastructure and does not change player behavior.

## Loaded-Def boundary

`LoadedDefSnapshotSource` is created once at the end of post-Def startup, after
the existing rarity, research-kit, and alternate preparation steps. It reads
each relevant loaded-Def collection exactly once:

- projects;
- recipes;
- things;
- terrains;
- explicit special opportunities;
- explicit and generated alternate definitions.

Unlock and recipe-user relationships are derived from those captured arrays.
Construction-stuff candidates are also derived from the captured ThingDefs, so
the adapter does not call `ResearchProjectDef.UnlockedDefs`,
`RecipeDef.AllRecipeUsers`, or `GenStuff.AllowedStuffsFor` in a per-project
loop. The snapshot surface has no map, pawn, faction, UI, Harmony, or current-
game service. No tick path calls the index builder.

`ResearchDefIndexSession.Initialize` is idempotent for the session. It publishes
the completed index only after construction succeeds and reports each cyclic
prerequisite component once. Malformed-data warnings are deduplicated by the
pure builder and runtime logging is capped at 50 details plus one suppressed-
count summary.

## Immutable index

The pure types under `Source/Domain/DefIndex/` are compiled directly into the
independent headless test project. Snapshot constructors copy nested inputs;
the builder snapshots each top-level source enumeration once; and every
published collection is read-only.

The index retains:

- projects, direct and hidden prerequisite edges, transitive ancestors, and
  transitive descendants;
- unlocks, `requiredAnalyzed`, and techprints;
- recipes, research prerequisites, products and counts, recipe users, and
  ingredient requirements;
- ThingDef and TerrainDef research prerequisites and construction costs;
- plant harvest products and refuelable fuel filters;
- complete explicit special-opportunity metadata and subjects;
- stable transitive equivalent/similar alternate groups with all source Defs
  and explicit/inferred provenance.

Every project evidence edge records project, subject, direct/ancestor/
descendant relation, semantic role, source kind, source Def, and confidence.
Hidden prerequisite edges have their own source kind. Ancestor and descendant
outcomes retain the original Def that supplied the evidence.

Prerequisite cycles use iterative strongly connected component traversal. A
self-reference and a multi-project loop each produce one bounded component;
reachability queries use visited sets and never recurse.

## Compact filters

Fixed ingredients and construction materials retain their exact Def identity
and count. Broad ingredient, stuff, and fuel filters become one evidence node
backed by a bitset over the sorted loaded ThingDef catalog. A stable SHA-256
membership key identifies the filter. The index can test membership or lazily
enumerate matching definitions later without creating one evidence edge or
opportunity per allowed ThingDef.

The focused broad-filter fixture permits 512 ThingDefs. Its indexed filter is
one ingredient requirement, one evidence edge, and eight 64-bit words.

## Headless tests

Eight Phase 4 tests cover:

- direct, hidden, ancestor, recipe, product, user, unlock, required-analysis,
  cost, plant, fuel, techprint, and explicit-special provenance;
- a two-project prerequisite cycle and a self-cycle, each reported once;
- the 512-member compact broad filter and membership lookup;
- null definitions, null references, missing projects, invalid fixed
  requirements, and malformed alternate endpoints;
- byte-for-byte canonical output under reversed top-level and nested source
  enumeration;
- transitive alternate grouping across explicit and inferred sources;
- exactly one enumeration of each loaded-Def snapshot collection and the
  absence of map/pawn services from the source contract;
- detachment from mutable source lists and read-only published collections.

The existing 38 characterization/domain tests continue to pass, including all
579 captured legacy opportunities. The Phase 4 total is 46 tests.

## Verification evidence

Final repository-native evidence:

- `scripts/test.ps1 Release`: 46 passed, 0 failed, independently runnable;
- `scripts/check.ps1`: 129 XML files parsed, all tests passed, and quiet Debug
  and Release production builds passed;
- Debug DLL: 530,432 bytes, SHA-256
  `843AACF0EB33F74C3E5CA585E69D87CDEA00E63F0D62B931B485B25E4AFE5462`;
- Release DLL: 482,816 bytes, SHA-256
  `4DD53CCBC0820BAA7B00AFECA1BD47FA727403DEC45E40A18385B43372AB7B2D`;
- assembly identity: `ResearchReinvented, Version=1.0.0.0`, file version
  `1.6.0.0`, product version `1.6`;
- package identity: `PeteTimesSix.ResearchReinvented`;
- `scripts/package.ps1 --no-build`: produced a 181-entry public ZIP whose
  packaged DLL hash equals the verified Release DLL;
- package SHA-256:
  `E335AE8019F75227C9A6A40F6EE27A9CDF16BC2F8FE7701213C9B6170ACEB6C0`;
- the package contained zero source, test, symbol, private Harmony, or
  RimBridge SDK/annotations entries and passed shared `modctl audit-package`.

No deploy command is run and no RimWorld installation is modified.

## Exit criteria

- A project query returns deterministic evidence with full role/source Def
  provenance and explicit direct/hidden prerequisite edges.
- Index construction performs one startup read of each assigned loaded-Def
  collection and no map or pawn scan.
- Broad filters remain compact and cycles are finite and deduplicated.
- The legacy generator remains the only player-facing generator.

Phase 5 opportunity rules may begin only as a separate change set.
