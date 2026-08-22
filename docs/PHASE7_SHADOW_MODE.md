# Phase 7 shadow-mode comparison

Status: complete on branch `codex/rewrite-phase-7`.

Phase 7 runs the Phase 5 rules and Phase 6 selector beside the authoritative
legacy generator when the selected project changes. The new result is observed
and compared only. It does not create opportunities, jobs, UI entries,
progress, category stores, or saved state, and it cannot change or reject the
legacy result.

This phase does not start the Phase 8 opportunity service or save migration,
migrate execution or UI, replace prototypes, repair the Argonic Core prototype
interaction, add Anomaly Study execution, package, release, or publish. No item
is added to `PENDING_RELEASE_NOTES.md` because default player-visible behavior
is unchanged.

## Runtime boundary

`ResearchShadowComparisonSession` is initialized once after the immutable Def
index. Its toggle defaults to false, is exposed only as a development debug
action, and is deliberately not serialized. When enabled, the manager invokes
the observer only while handling a selected-project change or an explicit
debug action. There is no per-tick generation or global Def scan.

The observer:

- adapts the already-generated legacy opportunities without mutating them;
- generates a detached static candidate universe from the immutable Def index;
- normalizes and selects candidates with the existing legacy project and
  category budget snapshots copied exactly into the Phase 6 policy;
- canonicalizes equivalent and similar subject families, recipe families, and
  current-game faction instances only for semantic comparison;
- distinguishes a legacy opportunity omitted by bounded selection from one the
  new rules never generated;
- reports duplicate keys, legacy-only, not-selected, new-only, metadata, and
  project/category budget differences in stable order;
- classifies only reviewed differences and leaves every other difference
  `Unexplained`;
- catches all observer failures and returns control to the authoritative legacy
  path; and
- writes at most 64 combined difference, rejection, and conversion details,
  followed by one suppressed-count summary.

Cold immutable-index construction and warm per-project rule generation plus
selection are measured separately. Static generation remains separate from
current-game availability: maps, pawns, factions, patients, and present items
do not decide which candidates exist.

## Reviewed comparison policy

The shared, headless `ShadowComparisonPolicies.Phase7` policy records the
reviewed differences used by the runtime observer. It approves:

- semantic-key duplicates removed by Phase 6;
- candidates omitted by deterministic limits and diversity;
- dynamic player/non-player faction importance deferred to availability;
- explicit special-opportunity metadata retained by the new rules;
- bounded descendant reverse-engineering and relevant ancestor evidence;
- direct unlocks retained from the immutable evidence graph;
- legacy broad ingredient, fuel, and medicine filter expansions omitted when
  they have no explicit subject evidence; and
- three exact omissions found during live review: direct autobong analysis for
  `MicroelectronicsBasics`, and the two Research Reinvented field-kit recipes
  directly gated by `MultiAnalyzer`.

The exact autobong approval has a negative test proving that an unrelated
direct analysis remains unexplained. Approvals can also be scoped by project,
type, subject, relation, and generation-reason provenance, preventing a broad
classification from concealing future regressions.

## Findings fixed during shadow runs

The comparisons found and corrected Phase 4-6 defects before those components
became authoritative:

- Mono dictionary path compression invalidated live key enumeration; alternate
  groups now snapshot keys before grouping.
- Static candidates incorrectly carried a legacy freebie override. Static
  candidates now retain the captured legacy false default, and factionless
  knowledge retains legacy importance `0.5`.
- Context and plant rules were following inherited second-order evidence.
  They now use direct evidence and equivalent alternates, keeping broad families
  bounded.
- plant `sowResearchPrerequisites` were absent from the loaded index and are now
  included with ordinary Thing prerequisites.
- an optional null singular recipe prerequisite was reported as malformed; it
  is now omitted while valid plural prerequisites remain indexed.
- similar-family comparison previously treated equivalent semantic tasks as
  different selection keys. A comparison-only canonicalizer now aligns them
  without changing Phase 6 selection semantics.

These corrections affect only the inactive new pipeline. The legacy generator
remains the sole player-facing authority.

## Live evidence

All accepted runs used RimWorld `1.6.4871 rev591`, an isolated `-quicktest`
profile, the guarded repository deployment, and an opt-in collector. RimWorld
was checked before every deployment-related operation. The collector preserved
its Phase 0 behavior unless `RR_SHADOW_COMPARE=1` was set, and was hardened to
wait for the legacy unique-ID component instead of racing game-component
initialization.

| Profile | Projects compared | Cold index | Warm generation range | Result |
| --- | ---: | ---: | ---: | --- |
| Core only | 7 | 196.509 ms | 1.044-24.085 ms | All differences resolved; the two exact `MultiAnalyzer` recipe improvements were confirmed by the later DLC run. |
| Core plus all DLC | 13 | 411.198 ms | 0.611-124.137 ms | Ten projects were already clean; seven reviewed direct-unlock differences were classified. A current-policy follow-up on `CarpetMaking`, `MicroelectronicsBasics`, and `MultiAnalyzer` reported zero unexplained differences. |
| Harmony, all DLC, ReplaceLib, Progression: Kitchen/Production, Argonic Core | 3 | 421.211 ms | 3.765-26.986 ms | `Ferny_ButcherTable`, `Electricity`, and `ComplexClothing` reported zero unexplained differences. `Ferny_Workbench` was explicitly absent because its Medieval Overhaul integration was not active. |

The final all-DLC follow-up loaded all nine requested packages and reported:

- `CarpetMaking`: legacy 24, selected 15, matched 11, differences 8,
  unexplained 0;
- `MicroelectronicsBasics`: legacy 60, selected 27, matched 24,
  differences 32, unexplained 0; and
- `MultiAnalyzer`: legacy 34, selected 21, matched 12, differences 23,
  unexplained 0.

No accepted log contains a project or category budget mismatch or a legacy
conversion failure. The smaller selected sets remove duplicate faction tasks,
incidental broad-filter subjects, and one-family domination while retaining
direct evidence and reviewed omissions, demonstrating greater relevance for
the selected fixtures without changing point budgets.

The requested 1,406-entry heavy profile was attempted. The direct isolated
process mounted only Core and the five DLCs because Workshop enumeration was
unavailable outside the interactive Steam session; it omitted both the rewrite
and collector and produced no comparison output. That process was stopped and
the reduced run was rejected as heavy evidence. The two real Phase 0 saves from
a 1,373-mod environment remain durable heavy-state fixtures. A genuine
interactive heavy shadow timing run remains an explicit Phase 12/14 live
validation blocker and is not represented as complete here.

The focused content run also reproduced the already documented Argonic Core
construction-transpiler error. It remains assigned to Phase 11 and was not
changed in Phase 7.

## Headless and repository verification

The 11 Phase 7 tests cover exact semantic matches, legacy/new-only approvals,
metadata mismatches, duplicate and bounded stable output, category/project
budget comparisons, generated-but-not-selected candidates, malformed generated
data, runtime-faction canonicalization, project/reason scoping, similar-family
canonicalization, and the exact autobong approval. Together with Phases 2-6,
the independent Release suite passes 82 tests.

Final verification:

- `scripts/test.py Release`: 82 passed, 0 failed;
- `scripts/build.py Debug`: passed; DLL size 614,400 bytes, SHA-256
  `6F2189D937EC23B288285557BEC115032C4DB057DE11C0B8D635CE974CF24201`;
- `scripts/build.py Release`: passed; DLL size 562,688 bytes, SHA-256
  `32ED55D10D9B2B1B09C53177AB41BEF77A14AE59250D08FE26F2AEE523B682BA`;
- `scripts/check.py`: 129 XML files parsed, characterization tests and quiet
  Debug/Release builds passed, and metadata remains RimWorld 1.6 only;
- final shared `modctl inspect` reported a clean 1.6 repository, and
  `modctl validate` reported zero errors and zero warnings;
- assembly identity remains
  `ResearchReinvented, Version=1.0.0.0`, file version `1.6.0.0`, product
  version `1.6`, and package ID `PeteTimesSix.ResearchReinvented`; and
- source audits found no new Scribe fields, opportunity-service authority,
  execution/UI migration, or saved shadow toggle.

The temporary collector and Harmony junctions and the guarded local rewrite
deployment were removed after RimWorld exited. Workshop item `2868392160`
remains installed and untouched. No deployment remains active, and no release
or publishing operation was performed.
