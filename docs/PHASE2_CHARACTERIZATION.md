# Phase 2 characterization tests and test seams

Status: complete on branch `codex/rewrite-phase-2`.

Phase 2 protects legacy Research Reinvented behavior before generator or state
replacement. It adds an independently runnable headless test project and narrow
adapters around RimWorld globals without changing the player-facing rules.

## Scope boundary

This phase does not replace the generator, introduce new domain contracts,
migrate saves, change prototype behavior, repair the Argonic Core interaction,
or add Anomaly Study execution. Those requirements remain assigned to their
later phases in `REWRITE_PLAN.md`.

No player-visible release note is added because the default runtime path keeps
the legacy behavior and this phase is development infrastructure only.

## Runtime seams

`IResearchRuntimeServices` is the narrow boundary for legacy code that needs:

- `DefDatabase` enumeration, preserving the distinction between `AllDefs` and
  `AllDefsListForReading`;
- the current `ResearchManager` and selected project;
- the player faction and the exact legacy faction query;
- loaded maps;
- Research Reinvented settings;
- current-game `GameComponent` lookup.

`LiveResearchRuntimeServices` delegates to the same RimWorld globals used
before Phase 2. `ResearchRuntimeServices.OverrideForTests` uses the tested
`ScopedServiceOverride` helper to provide bounded, disposable, nestable
overrides without making the adapter part of the public assembly API.
The generator factories, alternates preparation, post-Def startup, opportunity
manager, category/settings lookups, unique-ID component access, and two analysis
map caches now enter those globals through the seam.

`LegacyOpportunitySemantics` extracts the existing formulas for category
budgets, per-type opportunity allocation, rare/freebie flags, the one-point
minimum, and stable load-ID formatting. The production paths call this helper,
and the test project links the same source file. Arithmetic order and Boolean
semantics are unchanged.

## Independent tests

`Source/Tests/ResearchReinvented.Tests.csproj` targets `net10.0`, restores from
its own lock file, writes only under `artifacts/tests/`, and runs without Unity,
RimWorld, or the production assembly. `scripts/test.ps1` and `scripts/test.sh`
are the supported entry points. The repository-wide check runs the Release test
suite before its ordinary Debug and Release production builds.

The 30 tests cover:

- canonical snapshot digests for 13 minimal-profile projects and the focused
  `Ferny_ButcherTable` compatibility project;
- all 579 captured opportunities, including type, relation, category,
  requirement kind, canonical subject, alternates, progress, importance,
  rarity/freebie result, and legacy generation source;
- exact category budgets and the existing allocation formulas, including the
  zero-cost one-point floor and rare-opportunity behavior;
- the `TableButcher` / `AncientTableButcher` alternate relationship;
- legacy forced-flag truth tables and the absence/default serialization
  semantics recorded in Phase 0;
- 195 opportunities from the two real legacy-save snapshots, including
  contiguous load IDs `89-177` and `178-283`;
- the partial ancestor `VFE_CoffeeTable` progress
  (`10.9743195 / 16.666666`);
- a headless semantic round trip of the captured manager, category, opportunity,
  requirement, settings-context, and prototype-reference state;
- executable ownership contracts for issues 19, 20, 22, 24, and 33, the
  Argonic Core prototype failure, and optional Anomaly Study integration;
- default, nested, restored, and null-rejected service override behavior.

The save round trip is deliberately a semantic JSON snapshot round trip, not a
claim that RimWorld's `Scribe` runtime was booted headlessly. Neither Phase 0
save contains active prototypes, so prototype save/load evidence remains a
Phase 11 gap rather than an invented passing fixture.

## Verification evidence

Pre-edit evidence:

- worktree HEAD was `7e0ba23d7cc2cc12458c1da3633f7423eb1c7e19` on
  `codex/rewrite-phase-1`, with a clean tracked worktree;
- the Phase 2 branch was created as `codex/rewrite-phase-2` from that commit;
- shared `modctl doctor`, `inspect`, and `validate` ran before editing;
- dependency resolution selected SDK `10.0.302`, `Krafs.Rimworld.Ref`
  `1.6.4871`, `Lib.Harmony.Ref` `2.4.2`,
  `Microsoft.NETFramework.ReferenceAssemblies.net472` `1.0.3`,
  `Microsoft.NET.Test.Sdk` `18.9.0`, xUnit `2.9.3`, and runner `4.0.0`;
- no RimWorld process was running.

Final commands and results:

- `scripts/test.ps1 Release`: 30 passed, 0 failed, independently runnable;
- `scripts/check.ps1`: 129 XML files parsed, tests passed, and quiet Debug and
  Release production builds passed;
- Debug DLL: 398,848 bytes,
  SHA-256 `F27D40BB32D9C13AF04E981686A37C5749260696E8404BA518FF739A6285230A`;
- Release DLL: 360,448 bytes,
  SHA-256 `F3C7649A2F57E4B96C7FD73EDA5EBFF624D87F6EF1911903B9D832915D883FD9`;
- assembly identity: `ResearchReinvented, Version=1.0.0.0`, file version
  `1.6.0.0`, product version `1.6`;
- package identity: `PeteTimesSix.ResearchReinvented`;
- `scripts/package.ps1 --no-build`: produced a 181-entry public ZIP whose
  packaged DLL hash equals the verified Release DLL;
- package SHA-256:
  `DFB64D14B422C40AA2355C8EEF76F5025B119389F92C1AC1CBCCE2E67E9EF5D2`;
- public package retained `About/PublishedFileId.txt`, contained zero forbidden
  source/test/symbol/Harmony/SDK entries, and passed shared
  `modctl audit-package`;
- final shared `modctl validate`: zero errors and zero warnings.

No deploy command was run, and no live RimWorld installation was modified.

## Exit criteria

- The independent tests enumerate and validate the captured old-generator
  output for every fixed Phase 0 project.
- Snapshot failures identify semantic project/opportunity fields, never runtime
  object addresses.
- The legacy formulas are exercised by the same source compiled into production.
- The default service adapter uses the original RimWorld global calls.
- Production Debug/Release builds, identity, 1.6-only metadata, and package
  boundaries remain intact.

Phase 3 may introduce new domain contracts only as a separate change set.
