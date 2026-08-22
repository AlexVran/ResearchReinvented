# Phase 0 behavioral and compatibility baseline

Status: complete at source commit `77aa3d4ee02a3c80838f1134b7512b506454a465`.

This document owns the Phase 0 decisions and evidence required by
`REWRITE_PLAN.md`. Phase 0 changes only baseline tooling, fixtures, and
documentation. No gameplay source, XML definition, texture, assembly, package
identity, or installed Workshop item is changed.

Development takes place in a sibling Git worktree on
`codex/rewrite-phase-0`. The original checkout remains on `main`; its
user-owned rewrite plan is read-only input to this worktree.

## Compatibility decisions

- RimWorld 1.6 is the active rewrite target. The captured runtime is
  `1.6.4871 rev591`.
- Existing 1.3, 1.4, and 1.5 payloads remain frozen and keep loading their
  current assemblies.
- `PeteTimesSix.ResearchReinvented` and existing public XML `defName` values
  remain stable. The rewrite is a replacement build, not a second mod intended
  to run alongside the original.
- Existing saves are supported through an explicit, versioned migration path.
- Generator decisions use immutable facts from loaded `Def`s. Whether a
  subject is currently reachable or present on a map is a separate dynamic
  query.
- Explicit compatibility metadata wins over inferred relationships.
- Prototyping is an independently containable subsystem. A failed prototype
  integration must not disable ordinary research opportunities.
- Repository/build modernization, textures, generator replacement, save
  migration, execution changes, and prototype changes remain separate review
  slices.
- Anomaly is optional. Its Study interaction may be reused for ordinary items
  related to the current research project, but the core research loop must
  retain a non-Anomaly analysis path.

## Repository and runtime inventory

### Payload and loading

- The authoritative mod payload is nested under `ResearchReinvented/`.
- `ResearchReinvented/LoadFolders.xml` loads the payload root plus the active
  version folder. Combat Extended and Vanilla Expanded Framework Architect
  receive version-specific compatibility folders in 1.4 through 1.6.
- The 1.6 assembly is `ResearchReinvented/v1.6/Assemblies/ResearchReinvented.dll`.
- The mod constructor applies all Harmony patches. Post-definition startup then
  scans loaded databases and prepares alternates and research-kit data.

### Build and deploy

- The current project targets `net472`, uses C# 11, embeds debug symbols, and
  references `Krafs.Rimworld.Ref 1.6.4503-beta`, `Lib.Harmony 2.3.6`, and the
  .NET Framework reference assemblies package.
- A normal Windows build invokes `ResearchReinvented/Source/Install.bat`. That
  script waits, copies the payload into RimWorld's local `Mods` directory, and
  creates a ZIP. Phase 1 must separate build, deploy, and package operations.
- The legacy Release project builds when the install target is disabled with
  `-p:OS=Unix`: zero errors and one existing `CS0169` warning for
  `Dialog_AlternateMapper.viewHeight`.
- The isolated build DLL SHA-256 is
  `36BD9B0A649B031DEE865E64A11C16F0FA863D65E4FA5E8FBDEE2E6DC6E85334`.
  It differs from the checked-in DLL
  (`329D27DD64F18555182BC8730050FACB8304E2F92E2D788DB1905BD3BF128F07`)
  and the currently installed Workshop DLL
  (`A3292C14B384D244F13AB96CB4899F48A54AA92794F9E1DB68FE2E50EC692749`).
  Embedded debug data and differing build inputs mean hash inequality alone is
  not evidence of a gameplay difference.

### Saved state and mutable runtime ownership

- `ResearchOpportunityManager` is a `GameComponent` with a static `Instance`.
  It deep-saves generated opportunities and category stores and saves project
  references directly.
- Legacy opportunity state includes the project, type, relation, requirement,
  maximum/current progress, importance, load ID, and `isForcedRare` when true.
- `isForcedFreebie` is not serialized. Runtime rare/freebie status may also be
  inferred from loaded `DefModExtension` data. Migration must preserve the
  observed result rather than assuming the absent field is false.
- `PrototypeKeeper` saves prototype object references. Changing research can
  remove prototype blueprints, frames, unfinished things, bills, and products.
- Several `WorkGiver` implementations retain static lookup caches. These are a
  later execution/performance concern, not Phase 0 behavior to change.

### Generator architecture

- `ResearchOpportunityPrefabs` delegates to `MasterFactory` and related
  factories.
- Generation performs repeated global `DefDatabase` scans and constructs
  mutable legacy opportunity objects immediately.
- Opportunity requirements are polymorphic runtime objects and saved directly.
  Phase 3 will introduce semantic keys and immutable specifications while a
  compatibility adapter still creates legacy objects.

## Generator fixtures

`tools/phase0/legacy-generator-collector` runs inside a disposable quick-test
game and calls the legacy generator itself. Raw reports and logs remain under
ignored `.runs/`. `tools/phase0/normalize_generator_baseline.py` removes only
non-semantic timing, localized display text, and generated faction load IDs.

The durable minimal fixture contains 13 projects and 552 opportunities:

| Project | Source | Opportunities | Primary coverage |
| --- | --- | ---: | --- |
| `Electricity` | Core | 65 | Buildings, fuel, factions, schematics, construction prototypes |
| `ComplexClothing` | Core | 97 | Broad recipe ingredients and production prototypes |
| `DrugProduction` | Core | 25 | Drugs and material analysis |
| `MedicineProduction` | Core | 29 | Medicine, facilities, recipes, tooling |
| `Prosthetics` | Core | 29 | Recipe and production-facility relationships |
| `Bionics` | Core | 40 | Advanced production and recipe relationships |
| `TreeSowing` | Core | 55 | Plants and harvest products |
| `CarpetMaking` | Core | 24 | Terrain analysis and terrain prototypes |
| `BiofuelRefining` | Core | 36 | Fuel/material relationships |
| `MicroelectronicsBasics` | Core | 60 | Recipes, facilities, factions, schematics |
| `MultiAnalyzer` | Core | 34 | Advanced building/facility relationships |
| `BioferriteShaping` | Anomaly | 37 | Anomaly items and terrain relationships |
| `BlissLobotomy` | Anomaly | 21 | Surgery, pawn, medicine, and clinical relationships |

The focused compatibility fixture adds `Ferny_ButcherTable` from Progression:
Kitchen with 27 generated opportunities. A real heavy save supplies the second
content-mod project, `Ferny_Workbench`, which exists only when Progression:
Production's Medieval Overhaul integration is active.

Every captured generator opportunity preserves:

- type and source project;
- canonical subject and alternates;
- direct/ancestor relation;
- category and maximum/current progress;
- importance, rare, and freebie flags;
- legacy generation-source explanation.

The normalizer rejects reports containing collector errors or missing selected
projects. Regenerating all durable fixtures produced identical SHA-256 hashes.

## Save fixtures

`tools/phase0/extract_legacy_save.py` streams the large XML save rather than
loading it into memory as one tree. It records only Research Reinvented state,
relevant mod identifiers, source hashes, and the active settings snapshot.

| Fixture | Active project | Opportunities | Partial progress | Prototype references |
| --- | --- | ---: | ---: | ---: |
| `legacy-save-ferny-butcher-table-partial.json` | `Ferny_ButcherTable` | 106 | 1 | 0 |
| `legacy-save-ferny-workbench.json` | `Ferny_Workbench` | 89 | 0 | 0 |

The partial entry is ancestor `Analyse` progress for `VFE_CoffeeTable`:
`10.9743195 / 16.666666`, in `ForwardEngineering`.

Both saves preserve the active project, generated-project set, opportunity
load IDs, category stores, manager change ticker, and configured category
overrides. No inspected save had an active prototype. Prototype save migration
therefore remains an explicit Phase 11 live-fixture gap.

## Timing and allocation observations

Measurements use the first legacy generation call for each selected project
inside one quick-test game. Managed byte deltas use `GC.GetTotalMemory(false)`
and are directional observations, not benchmark-grade allocation counts.

| Profile | Loaded mods | Projects | Opportunities | Summed generation | Slowest project | Approx. managed delta |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Minimal DLC | 9 | 13 | 552 | 23.167 ms | 14.944 ms | 327,680 bytes |
| Argonic/content compatibility | 13 | 14 | 569 | 24.776 ms | 15.804 ms | 331,776 bytes |

The slowest entry in both profiles is the first project, `Electricity`, so the
number includes cold-start/JIT work. Later projects are mostly below 3 ms.

The user's full active list cannot be mounted by a direct isolated executable
launch because Workshop enumeration requires the interactive Steam user
session. The baseline therefore combines an exact 13-mod compatibility run
with two real saves created under 1,373 active mods. A full heavy-list timing
run remains Phase 12 evidence; it must not be represented as completed by a
profile that silently omitted Workshop mods.

## Argonic Core incompatibility

Reproduction profile:

1. Load Harmony, Core/DLCs, ReplaceLib, Progression: Kitchen, Progression:
   Production, Argonic Core `1.6.1`, legacy Research Reinvented, and the Phase 0
   collector.
2. Load Argonic Core before Research Reinvented.
3. Start a quick-test game.

The resulting log contains:

```text
RR: Frame_CompleteConstruction_Patches - TranspilerSpawn - failed to apply patch (instructions not found, stage 1)
```

Root cause:

- Argonic Core transpiles `Frame.CompleteConstruction`. Immediately after the
  `GenSpawn.Spawn` call it inserts material-transfer instructions and skips the
  original following instruction.
- Research Reinvented's
  `HarmonyPatches/Prototypes/Frame_CompleteConstruction_Patches.cs` searches
  for an exact instruction sequence ending in `GenSpawn.Spawn` followed by
  `Pop`.
- Its `HarmonyBefore` list does not name Argonic Core's Harmony owner,
  `Argon.CoreLib.MaterialReplacement`.
- When Argonic composes first, Research Reinvented cannot find the sequence and
  omits `PreSpawn`/`PostSpawn`. Prototype completion therefore lacks the path
  that marks the result and grants research progress, matching the reported
  “build completes but no research” behavior.

Phase 11 replacement contract:

- Do not depend on local indices or an exact `Spawn`/`Pop` IL shape.
- A prefix captures whether the frame is a prototype plus its project, map,
  position, target definition, work amount, and worker.
- A postfix locates the completed product after other mods have transferred
  state, applies the prototype outcome, and credits the matching opportunity
  exactly once.
- A normal non-prototype construction credits nothing.
- Argonic's selected material replacements remain attached to the finished
  product.
- Failure of this integration disables only prototype construction credit and
  emits a targeted diagnostic; ordinary research remains operational.

This is deliberately characterized in Phase 0 and fixed in Phase 11, where the
prototype subsystem is replaced or contained.

## Optional Anomaly Study route

The requested behavior is to study ordinary items related to the active
research project using Anomaly's Study interaction when Anomaly is enabled.
It is not a request to turn those items into anomaly entities or award anomaly
knowledge.

Required behavior:

- eligible ordinary things are derived from the same canonical requirement and
  alternate-subject data as the selected Research Reinvented opportunity;
- the player can assign Study-style work to those items when Anomaly is active;
- completed Study work contributes only to the matching Research Reinvented
  opportunity/category and uses its existing progress budget;
- studying an unrelated item contributes no progress;
- the interaction must not grant anomaly knowledge unless the target already
  has an independent vanilla Anomaly reason to do so;
- without Anomaly, the existing/generalized analysis work path provides the
  same Research Reinvented progress opportunity;
- save data stores semantic research progress, not an Anomaly runtime object.

Anomaly exposes `DarkStudy`, `StudyInteract`, `WorkGiver_DarkStudyInteract`,
`CompProperties_StudyUnlocks`, `StudyEfficiency`, and `EntityStudyRate` defs.
The exact game-code seam must be inspected with DecompilerServer before the
execution phase; Phase 0 does not guess at private 1.6 APIs or add a hard DLC
reference.

## Retexture provenance

Workshop item `3279243445` (Research Reinvented Retextured) is the authoritative
source for the rewrite's loose PNG sprites. It contains a one-for-one
replacement for all 21 current PNGs, each at 512 x 512:

| Texture group | PNG count |
| --- | ---: |
| Tool motes (`Adv`, `Basic`, `Mid`, `None`) | 4 |
| Hi-tech research kit | 4 |
| Multi-analyzer research kit | 4 |
| Remote research kit | 4 |
| Simple research kit | 5 |

Phase 1's separate artwork slice will copy only the matching texture PNGs. It
must not copy the retexture mod's `About` data, preview, `PublishedFileId.txt`,
or DDS files into the rewrite.

## Open-issue triage

| Issue | Classification | Planned phase |
| --- | --- | --- |
| [#33: `KeyNotFoundException` for `MeleeWeapon_Axe`](https://github.com/PeteTimesSix/ResearchReinvented/issues/33) | Execution/cache invalidation | 9 |
| [#24: settings cannot scroll](https://github.com/PeteTimesSix/ResearchReinvented/issues/24) | UI/settings | 10 |
| [#22: missing XML parent](https://github.com/PeteTimesSix/ResearchReinvented/issues/22) | Compatibility/malformed XML | 12 |
| [#20: bills sometimes vanish on research switch](https://github.com/PeteTimesSix/ResearchReinvented/issues/20) | Prototyping lifecycle | 11 |
| [#19: Combat Extended patch failure](https://github.com/PeteTimesSix/ResearchReinvented/issues/19) | Harmony isolation/compatibility | 11-12 |

The Argonic Core prototype failure is additionally tracked by this baseline
even though it does not currently have a repository issue.

## Reproduction commands

Normalize raw collector reports:

```powershell
uv run python tools/phase0/normalize_generator_baseline.py `
  .runs/phase0/runtime/minimal-generator.json `
  tests/fixtures/phase0/legacy-generator-minimal.json `
  --profile minimal
```

Extract a save fixture:

```powershell
uv run python tools/phase0/extract_legacy_save.py `
  $save `
  tests/fixtures/phase0/legacy-save.json `
  --payload-root ResearchReinvented `
  --settings $settings
```

Compile the legacy baseline without deploying:

```powershell
dotnet build ResearchReinvented/Source/ResearchReinvented.csproj `
  -c Release `
  -p:OS=Unix `
  -p:OutputPath=.runs/phase0/legacy-build/
```

## Phase 0 exit check

- Compatibility decisions are explicit.
- Representative Core, DLC, Anomaly, and content-mod generator outputs are
  committed as reproducible semantic fixtures.
- Real old-save state with partial progress and settings is committed.
- Argonic Core's prototype failure is reproduced and its replacement contract
  is explicit.
- Open issues are assigned to later phases.
- No gameplay code or assets changed.

Phase 1 may begin only as a separate change set.
