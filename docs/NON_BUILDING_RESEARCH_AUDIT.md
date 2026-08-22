# Active-mod research projects without building unlocks

Audit date: 2026-08-22

This audit preserves the loaded-definition evidence requested before execution
and UI migration. It is a compatibility input, not a player-visible change and
not evidence that the Phase 7 shadow generator is authoritative.

The complete, path-free list is
[`audits/active-nonbuilding-research-2026-08-22.csv`](audits/active-nonbuilding-research-2026-08-22.csv).

## Result

RimWorld resolved 1,113 `ResearchProjectDef` records from the active profile.
Of those, 518 unlock no resolved building `ThingDef`:

| Coverage class | Projects | Rewrite implication |
| --- | ---: | --- |
| `ThematicMetadata` | 467 | At least one standard recipe, terrain, non-building thing, analysis requirement, or techprint relationship is available to the Phase 4/5 index and rules. |
| `CustomResearchModOnly` | 3 | Only a custom `ResearchMod` advertises the effect; the rewrite can still create generic opportunities, but needs an adapter or explicit metadata for thematic tasks. |
| `GenericFallbackOnly` | 48 | No captured standard unlock metadata exists. Phase 5 still supplies theory/schematic and static social candidate families, but no subject-specific task can be inferred safely. |

The following evidence counts overlap because one project can expose more than
one relationship:

| Evidence on projects with no building unlock | Projects |
| --- | ---: |
| Any recipe | 452 |
| Surgery recipe | 26 |
| Non-building `ThingDef` | 447 |
| Terrain | 5 |
| Techprint | 23 |
| Required analyzed thing | 2 |
| Custom `ResearchMod` | 3 |

This answers the two motivating cases:

- Odyssey's `Fishing` project unlocks no building and exposes none of the
  captured standard relationships. It is compatible with generic rewrite
  opportunities, but currently has no fish-specific inferred opportunity.
- All 26 no-building projects with surgery recipes expose their recipes to the
  loaded index. Phase 5 has an explicit, conservatively gated surgery-prototype
  rule, so surgery research has thematic evidence rather than depending on a
  building unlock.

## Generic-fallback review set

These are the 48 projects that should receive focused fixtures or explicit
metadata before the rewrite becomes authoritative. The CSV contains labels,
costs, prerequisites, and all captured relationship columns.

| Source package | Count | Project defNames |
| --- | ---: | --- |
| `ferny.generallymoreresearch` | 14 | `Ferny_Bioshred`; `Ferny_HealingPulse`; `Ferny_ImmunityPulse`; `Ferny_InhumanizingChant`; `Ferny_InvokeEclipse`; `Ferny_InvokeVoidAlly`; `Ferny_PsychicAwakening`; `Ferny_RegenerativeChant`; `Ferny_ShardProduction`; `Ferny_SkipMigration`; `Ferny_VoidCleanse`; `Ferny_VoidTouchTransfer`; `Ferny_WitheringChant`; `Ferny_XenogeneticRituals` |
| `Ludeon.RimWorld.Anomaly` | 13 | `BloodRain`; `Brainwipe`; `Chronophagy`; `DeathRefusal`; `NeurosisPulse`; `Philophagy`; `PleasurePulse`; `Psychophagy`; `SkipAbductionPlayer`; `SummonAnimals`; `SummonFleshbeastsPlayer`; `SummonPitGate`; `SummonShamblers` |
| `Bamba.AllBambaArmors` | 3 | `ABT_Research_IND_CataphractArmor`; `ABT_Research_IND_MarineArmor`; `ABT_Research_IND_ReconArmor` |
| `ferny.progressionfurniture` | 2 | `Ferny_InteractiveTables`; `Ferny_OutdoorSolarLamp` |
| `hlx.UltratechAlteredCarbon` | 2 | `AC_PsytrainerProduction`; `AC_SkilltrainerProduction` |
| Unattributed loaded def | 1 | `Ferny_PrisonArchitecture` |
| `Dubwise.DubsBadHygiene` | 1 | `Irrigation` |
| `Ludeon.RimWorld.Biotech` | 1 | `Archogenetics` |
| `Ludeon.RimWorld.Ideology` | 1 | `Bioregeneration` |
| `Ludeon.RimWorld.Odyssey` | 1 | `Fishing` |
| `M3.Continued.JangoDsoul.StarWars.PIICAS` | 1 | `JDSPhaseIIArmorLeadership` |
| `Matsay.ForsakenFaction` | 1 | `FOF_Plants` |
| `OskarPotocki.VFE.Classical` | 1 | `VFEC_RoadBuilding` |
| `OskarPotocki.VFE.Pirates` | 1 | `VFEP_WarcasketRemoval` |
| `ReSplice.XOTR.Core` | 1 | `RS_DarkArchiteHarvesting` |
| `VanillaExpanded.VPEFlowers` | 1 | `VPEF_Horticulture` |
| `ferny.progressiondrugs` | 1 | `Ferny_NecroaVaccine` |
| `ferny.progressionwarrants` | 1 | `WarrantNetwork` |
| `sarg.alphacrafts` | 1 | `AC_ArtisanalFermentation` |

The three `CustomResearchModOnly` projects are `PowerManagement` from Colony
Manager Redux and `FluffyBreakdowns_ComponentLifetimeOne` /
`FluffyBreakdowns_ComponentLifetimeTwo` from Fluffy Breakdowns. Arbitrary
custom `ResearchMod` behavior is deliberately not inferred by the pure rules.

## Method and reproducibility

- Source profile: 1,419 active package IDs in a copy of the user's actual
  `ModsConfig.xml`; profile version `1.6.4871 rev590`.
- Runtime: RimWorld `1.6.4871 rev591`, launched through Steam so Workshop and
  local mods resolved exactly as they do for the active installation.
- Profile SHA-256 before and after the audit:
  `8CCB7E60019C9DA913B7970E3E7D2CD51E0C0F2BC0822C1D136EA5285D6AD1FD`.
  The live profile was never edited.
- A temporary, isolated collector was appended only to the copied profile. It
  exported after the initial long-event queue completed and before Quicktest
  could generate a map, then closed RimWorld.
- Raw loaded-def capture: 1,113 rows, 538,525 bytes, SHA-256
  `CD52F19D7DDB12D2946F513CA939E78DE63923C96A600198766E249C079B7CE9`.
  It remains under ignored `.runs/research-audit/` for local diagnosis.
- Tracked normalized dataset: 518 rows, SHA-256
  `37337FACC5FA8EE73DB302EC5A05B7A30853D6E51EE1E31D06E2C918951D688E`.
  Rows retain the collector's ordinal package/defName order.

A building unlock means a resolved `ThingDef` with category `Building` linked
through `researchPrerequisites` or the project's runtime unlocked-def view.
The collector separately captured linked recipes, surgery status, terrains,
non-building things, `requiredAnalyzed`, techprints, custom `researchMods`, and
prerequisites. Empty package fields are preserved rather than guessed.

## Limits and next use

This is a snapshot of definitions that survived the active stack's XML patches
and cross-reference resolution. It cannot discover arbitrary effects implemented
only by C# patches, custom components, quest logic, or another mod-specific
contract. A mod update or active-list change requires a new audit.

Before final release readiness, use the dataset to add at least the following
focused compatibility fixtures. Phase 13 owns final curated opportunities after
the core migration and hardening phases are complete:

1. Odyssey `Fishing` as a valid project with generic fallback only.
2. A surgery-only project with its recipe and patient/medicine availability
   kept outside static generation.
3. A custom-`ResearchMod` project that remains bounded and explainable.
4. An Anomaly knowledge project, without treating the deferred Anomaly Study
   integration as already implemented.

No Phase 8 save/runtime migration, Anomaly Study integration, deployment, or
player-visible generator switch is part of this audit.
