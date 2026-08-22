# Phase 10 UI and settings

Status: complete on branch `codex/rewrite-phase-10`.

Phase 10 makes the Phase 8 opportunity service the read authority for the main
research window and gives settings changes a deterministic, progress-preserving
reconciliation policy. Execution and prototype behavior remain on their Phase 9
and legacy Phase 11 boundaries respectively.

This phase does not replace prototype selection or state, repair Argonic Core,
add Fishing or other curated opportunities, add Anomaly Study integration,
perform heavy-list hardening, deploy, package, release, or publish.

## Immutable presentation boundary

`IOpportunityService.ReadModelFor` returns a detached
`OpportunityProjectReadModel`. Its category and opportunity collections are
read-only snapshots containing:

- stable `OpportunityKey`, project, type, category, relation, and requirement;
- current and maximum progress;
- authoritative category progress and current category budget;
- rarity, freebie, importance, and preserved-state kind; and
- immutable generation reasons.

Creating a later snapshot is the only way for UI to observe subsequent service
mutations. The read model never exposes `OpportunityProgressState`, saved DTOs,
or the service's mutable dictionaries. Orphan category credit is included in the
category total without inventing an executable opportunity row.

The RimWorld presentation adapter resolves loaded category/type Defs through a
one-time immutable catalog. It copies current labels, icons, cycled alternate
subjects, tooltips, handler icons, and faction info into a per-draw immutable
view model. Legacy opportunity projections are used only at that adapter edge
for current runtime display assets; the UI never reads their progress or saved
state.

Both compact and expanded row renderers consume the same
`OpportunityPresentationViewModel`. Category headers use the service snapshot's
progress and budget rather than `ResearchOpportunityCategoryTotalsStore`.
Collapse state is keyed by stable category identity, not mutable Def-backed
opportunity collections.

Developer-mode tooltips add the stable key, relation, importance, rarity,
freebie flag, state kind, generation provenance, current dynamic category
availability reason, missing-projection state, and any failed/disabled activity
handler diagnostics. Normal player tooltips and existing translation keys are
preserved.

## Settings and reconciliation policy

Category preset resolution is now a pure, headless-tested operation:

1. resolve the active preset;
2. apply only non-null user overrides;
3. keep counted weight no greater than the resolved category weight; and
4. save only values that differ from the active preset.

Returning a value to its preset default removes the override. On a preset
switch, explicit overrides remain explicit while unchanged values adopt the new
preset. Existing serialized nullable fields and Def names remain unchanged.

Confirming a settings change calls `ReconcileSettingsChange` instead of
`ResetAllProgress`. The manager regenerates previously generated projects in
stable Def-name order, regenerates the selected project last, and delegates
state reconciliation to `OpportunityService.SetSpecifications`:

- matching stable keys keep their complete progress when maximums or budgets
  change;
- removed keys become credited orphans and remain in the versioned save;
- a key that becomes available again reclaims its retained progress;
- current project identity and execution indexes remain synchronized; and
- prior category budgets are replaced rather than left as stale UI totals.

The settings tabs use cached, deterministically ordered preset/category
catalogs. Their content is inside a scroll view with per-tab minimum content
height, addressing the failure shape in upstream issue #24 without adding any
opportunity regeneration, map scan, or Def-database scan to the research-window
draw path. English confirmation text now describes the actual preservation
policy while retaining the existing translation keys.

## Independent headless evidence

Seven new Phase 10 tests cover:

- detached and deterministic project/category/opportunity read models;
- exact service progress, maximums, budgets, rarity, and generation reasons;
- orphan category credit without a fabricated opportunity row;
- sparse preset overrides;
- preset switching with explicit overrides and inherited values;
- counted-weight clamping and removal of default-equal overrides; and
- settings reconciliation through removal and reintroduction of a stable key,
  including changed maximums and category budgets.

Together with Phases 2-9, the Release suite passes 117 tests. Existing Phase 8
tests continue to cover new-save round trips, every Phase 0 old-save fixture,
missing/renamed Defs and removed mods, partial progress, exact category totals,
project switching plus save/load, deterministic reconciliation, duplicate
semantic state, malformed/future schemas, idempotent migration, and safe
unmatched/special/prototype state.

## Verification evidence

- `dotnet test ... Release`: 117 passed, 0 failed;
- `scripts/check.py`: 129 XML files parsed, tests passed, Debug and Release
  builds passed;
- Debug DLL: 714,240 bytes, SHA-256
  `93C05B8AACBBF54062AB7344EB0642C6A3C59CC4A354FBBDBC2BA61C479AA3EA`;
- Release DLL: 657,408 bytes, SHA-256
  `03BE720B7DBA7D16903395D46D2D44548D0699DCE6E5E77CC54B506AE7FFCCEE`;
- package ID remains `PeteTimesSix.ResearchReinvented`;
- assembly identity remains `ResearchReinvented, Version=1.0.0.0`, file version
  `1.6.0.0`, product version `1.6`;
- source audits find no legacy opportunity collection, `DefDatabase` access,
  map scan, forced regeneration, or progress reset in the main research-window
  presentation path; and
- shared `modctl inspect` and `validate`: RimWorld 1.6 repository shape, zero
  errors and warnings. (`inspect` correctly reports the pre-commit worktree as
  dirty.)

No RimWorld process was running. No deployment command was run. The original
checkout retains its pre-existing untracked `REWRITE_PLAN.md`, and neither it
nor Workshop item `2868392160` was modified. Pixel-level settings scrolling and
live UI interaction remain Phase 14 evidence because this phase was not
authorized to deploy and the shared doctor reports no local GABS or
RimBridgeServer checkout.

## Exit criteria

- Compact and expanded UI modes consume the same service-backed immutable view
  model.
- Settings changes have a documented and independently tested stable-key
  progress policy.
- Main UI presentation does not scan Def databases or maps and cannot trigger
  opportunity regeneration.
- Phase 11 may begin only as a separate change set.
