# Phase 6 normalization, scoring, selection, and diagnostics

Status: complete on branch `codex/rewrite-phase-6`.

Phase 6 adds a pure, inactive selection pass over Phase 5 candidates. It does
not call the legacy generator, switch player-visible generation, migrate saved
state, change execution or UI, replace prototypes, repair the Argonic Core
prototype issue, add Anomaly Study integration, deploy, package, release, or
publish. `MasterFactory` remains the only authoritative player-facing
generator.

No item is added to `PENDING_RELEASE_NOTES.md`: the new infrastructure is not
on a runtime path and therefore changes no player-visible behavior.

## Pure selection boundary

`Source/Domain/Selection/OpportunitySelection.cs` is compiled into the
independent headless test assembly as well as production. It accepts only the
immutable `ResearchDefIndex`, Phase 5 `OpportunityRuleResult`, and an immutable
selection policy. It has no dependency on maps, pawns, factions, UI, Harmony,
loaded runtime `Def`s, availability checks, or legacy progress objects.

The policy supplies explicit type/relation-to-category assignments, category
and rule limits, diversity and diagnostic caps, and a copy of the current
category budget snapshot. The selector never allocates, changes, or recomputes
project/category research points; its result copies the provided project and
category budgets exactly. Runtime category lookup and the existing allocation
formulas remain owned by legacy code until later phases.

## Normalization and deterministic selection

- Equivalent ThingDef, TerrainDef, and RecipeDef requirement subjects are
  canonicalized to the sorted representative of their immutable Phase 4
  equivalent group before an `OpportunityKey` is created again. Existing and
  group alternates are retained as sorted alternate subjects.
- Candidates with the resulting same semantic key merge their sorted, distinct
  generation reasons and rule identifiers. The winning immutable specification
  preserves the greatest explicit importance and the conservative rare/freebie
  flags.
- Scores explicitly combine evidence strength, the Phase 4 evidence confidence
  retained by Phase 5 candidates, direct/ancestor/descendant relation, static
  requirement feasibility, and bounded explicit importance. This is static
  relevance ranking only; it does not claim a patient, faction, item, map, or
  recipe is currently available in a game.
- Candidates are ordered by descending score and stable semantic key. Category
  and rule limits are enforced before the selected result is published. A
  provenance-family diversity cap groups recipe, ingredient, construction,
  fuel, and plant candidates by their shared evidence source so one broad
  recipe or alternate family cannot dominate the set.
- The result owns a culture-invariant canonical semantic snapshot covering
  budgets, selected scores and rule ids, merged reasons, rejections, and
  suppressed counts for byte-for-byte comparisons.
- Diagnostics retain Phase 5 rejections and add semantic-merge, category,
  rule, diversity, and malformed-candidate explanations. They are sorted and
  capped, with suppressed diagnostic and per-candidate reason counts, so broad
  sources cannot create unbounded output. Explicit metadata reasons sort first
  within the bounded merged reasons so affected selections remain explainable.

## Headless tests

Ten Phase 6 tests add coverage for:

- equivalent subjects and recipes canonicalized before semantic-key deduplication;
- duplicate-key reason/rule merging and explicit metadata provenance;
- each evidence/confidence/relation/feasibility/importance score component,
  provenance-family diversity, category limits, and rule limits;
- byte-for-byte stable output and bounded diagnostics under reversed input;
- byte-stable end-to-end rule-generation/selection output with no duplicate keys;
- bounded merged generation reasons that retain explicit metadata first;
- malformed null candidates and invalid policy data, plus a 512-member broad
  filter fixture that remains bounded and never creates a filter subject;
- exact preservation of supplied project and category point budgets without
  reallocation.

The independent suite has 71 passing tests. Phase 7 may consume this pure
selector only through a non-player-facing shadow comparison; it must not make
the new candidates authoritative.

## Verification evidence

Pre-edit checks ran from clean Phase 5 commit
`bb75ffc67ae8e20ab3dcb25c56538f207bf91fea`; shared `modctl inspect` and
`validate` reported a 1.6 clean repository with zero errors and warnings.
The dependency resolver selected SDK `10.0.302`, `Krafs.Rimworld.Ref`
`1.6.4871`, `Lib.Harmony.Ref` `2.4.2`,
`Microsoft.NETFramework.ReferenceAssemblies.net472` `1.0.3`,
`Microsoft.NET.Test.Sdk` `18.9.0`, xUnit `2.9.3`, and runner `4.0.0`.
No RimWorld process was running. Repository-wide verification and final build
results:

- `scripts/test.py Release`: 71 passed, 0 failed, independently runnable;
- `scripts/build.py Debug`: passed; DLL size 583,680 bytes, SHA-256
  `01E52D4CB90815AFE77425FB0EE6116483464C3D7F5F71BED61FB4F2E0291B97`;
- `scripts/build.py Release`: passed; DLL size 533,504 bytes, SHA-256
  `6CC6FF722724DF152D55D8D8E96AC8B40286A2BCE45C450B6229BF71FB8ACD30`;
- `scripts/check.py`: 129 XML files parsed, all tests passed, and quiet Debug
  and Release production builds passed;
- final shared `modctl validate`: zero errors and zero warnings;
- assembly identity remains `ResearchReinvented, Version=1.0.0.0`, file
  version `1.6.0.0`, product version `1.6`, and package identity
  `PeteTimesSix.ResearchReinvented`;
- source search found no production call to the Phase 5 rule registry or the
  Phase 6 selector, so the legacy generator remains authoritative.

No deployment command was run and no RimWorld installation was modified.
