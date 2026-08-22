# Research Reinvented modernization plan

This document is the resumable implementation plan for modernizing Research
Reinvented. The recommended strategy is a staged replacement: rewrite the
domain core and opportunity generator, adapt the working RimWorld-facing code,
and replace prototyping last.

The plan deliberately keeps every phase independently reviewable. Do not begin
the next phase until the current phase's exit criteria are met.

## Intended outcome

The completed mod should:

- generate relevant, deterministic research opportunities from vanilla and
  modded `Def`s;
- remain responsive with a large mod list and avoid repeated global scans;
- preserve existing XML identifiers, translations, settings, and package
  identity where practical;
- load existing saves through an explicit migration path;
- isolate fragile Harmony integrations so one failed feature does not disable
  the entire research system;
- make generation rules testable without running a complete RimWorld game;
- provide diagnostics explaining why an opportunity was generated or rejected.

This is not initially a rebalance. Preserve current category budgets and
research-point behavior until the new generator and state model are proven.

## Default decisions

Unless deliberately changed during Phase 0, use these decisions:

- RimWorld 1.6 is the active development target.
- RimWorld 1.3-1.5 payloads are removed from the rewrite; the original checkout
  remains the historical source for them.
- The package ID and public XML `defName`s remain stable.
- Existing saves are supported through a versioned migration layer.
- Opportunity generation uses static loaded-Def facts. Current-map feasibility
  is a separate dynamic query.
- Explicit mod metadata wins over inference.
- Prototyping can be disabled independently if its Harmony integration fails.
- Repository modernization and gameplay changes are separate commits.

Dependency versions must be resolved again when implementation starts. At the
time this plan was written, the shared resolver reported:

- .NET SDK `10.0.302`
- `Krafs.Rimworld.Ref` `1.6.4871`
- `Lib.Harmony.Ref` `2.4.2`
- `Microsoft.NETFramework.ReferenceAssemblies.net472` `1.0.3`
- `Microsoft.NET.Test.Sdk` `18.9.0`
- xUnit `2.9.3`

These are a dated observation, not permanent pins.

## Working rules for every phase

At the beginning of a later session:

1. Read this document and the repository's current `AGENTS.md`, if present.
2. Run the shared doctor, inspect, validate, and dependency resolver commands.
3. Inspect `git status`, the current branch/upstream, active RimWorld processes,
   open issues, and repository-native scripts.
4. Create or use a `codex/` feature branch unless another branch was requested.
5. State the phase's behavioral invariant and plausible regression.
6. Change only the selected phase.
7. Run its focused tests and the quiet full build.
8. Update `PENDING_RELEASE_NOTES.md` only for completed player-visible changes.
9. Stop at the phase boundary and report changed files, evidence, and remaining
   risks.

Never combine dependency modernization, generator replacement, save migration,
and prototype Harmony work in a single unreviewable change.

## Progress checklist

- [x] Phase 0: confirm scope and capture the behavioral baseline
- [x] Phase 1: modernize the repository and build without changing gameplay
- [x] Phase 2: add characterization fixtures and test seams
- [x] Phase 3: introduce the new domain contracts
- [x] Phase 4: build the immutable Def index and evidence graph
- [x] Phase 5: implement opportunity rules
- [x] Phase 6: implement normalization, scoring, selection, and diagnostics
- [ ] Phase 7: run the new generator in shadow mode
- [ ] Phase 8: introduce the opportunity service and versioned save state
- [ ] Phase 9: migrate analysis and clinical execution
- [ ] Phase 10: migrate UI, settings, and remaining lookups
- [ ] Phase 11: replace or contain prototyping
- [ ] Phase 12: add compatibility, malformed-Def, and performance coverage
- [ ] Phase 13: perform live validation and prepare release readiness

## Phase 0: scope and behavioral baseline

### Goal

Record what must remain compatible before changing architecture.

### Work

1. Confirm the default decisions above, especially active RimWorld versions and
   save compatibility.
2. Inventory the repository layout, assembly loading, Harmony patches,
   settings, save data, compatibility modules, and current deployment path.
3. Triage open GitHub issues and classify each as generator, execution,
   prototyping, UI, compatibility, or unrelated.
4. Select representative research projects covering:
   - generic unlocked buildings and items;
   - production recipes with broad ingredient filters;
   - drugs and medicine;
   - surgery recipes;
   - plants, terrain, fuel, factions, and techprints;
   - projects contributed by at least two content mods.
5. Capture the generated opportunity list for every fixture, including type,
   subject, relation, category, maximum progress, rare/freebie flags, and source
   project.
6. Create old-save fixtures containing partial opportunity progress, category
   settings, an active research project, and active prototypes where possible.
7. Record generation time, allocations if measurable, and log output for a
   normal list and a heavy list.

Store machine-generated baseline reports under ignored `.runs/`. Commit only
small durable fixtures and documentation.

### Exit criteria

- The compatibility decisions are written down.
- Representative generator outputs and save fixtures are reproducible.
- Known issues are mapped to later phases.
- No gameplay code has changed.

### Resume prompt

> Work on Phase 0 of `REWRITE_PLAN.md`. Confirm the scope, capture reproducible
> generator and save baselines, and stop when the Phase 0 exit criteria are met.

## Phase 1: repository and build modernization

### Goal

Create a reliable development shell without changing player behavior.

### Work

1. Decide whether to flatten the nested mod payload or teach the repository
   wrappers its authoritative payload path. Prefer the modern shared contract,
   but preserve the installed package shape.
2. Add the standard version owner, pinned SDK, `.editorconfig`, local
   `AGENTS.md`, standard manifest, and pending release notes.
3. Replace the build-triggered `Install.bat` workflow with quiet build and
   guarded deploy wrappers. A normal build must not deploy implicitly.
4. Move to stable compatible `Krafs.Rimworld.Ref`, reference-only Harmony, and
   explicit .NET Framework reference assemblies.
5. Add release staging/package auditing that excludes private Harmony, symbols,
   development SDKs, and local metadata.
6. Correct repository metadata and artwork only as a separate, reviewed slice.

### Tests

- Restore and quiet Debug/Release builds.
- Compare the old and new DLL identity and installed payload layout.
- Verify that a normal build performs no deployment.
- Run the shared repository validator and verify that only 1.6 is shipped.

### Exit criteria

- The project builds reproducibly from a fresh checkout.
- Build, deploy, and package are separate guarded operations.
- No intended gameplay behavior changed.

### Resume prompt

> Work only on Phase 1 of `REWRITE_PLAN.md`. Modernize the repository and build
> shell without changing gameplay, verify the Phase 1 criteria, and stop.

## Phase 2: characterization tests and test seams

### Goal

Protect current behavior before replacing it.

### Work

1. Add an independently runnable test project.
2. Extract narrow adapters around global RimWorld services such as
   `DefDatabase`, `Find.ResearchManager`, factions, maps, and settings.
3. Add snapshot-style characterization tests for the Phase 0 projects.
4. Add tests for existing progress allocation, category budgets, alternate
   subjects, rarity/freebie behavior, and stable load IDs.
5. Add save round-trip tests for serializable state where a headless seam is
   feasible.
6. Add regression tests for already identified defects before fixing them.

Do not attempt to make every Harmony patch unit-testable. Characterize pure
logic first and reserve live tests for game-owned behavior.

### Exit criteria

- Tests can enumerate old-generator output for fixed fixtures.
- Failures show meaningful semantic differences rather than object addresses.
- The full production build remains unchanged.

### Resume prompt

> Implement Phase 2 of `REWRITE_PLAN.md`: add characterization fixtures and
> test seams around current behavior. Do not replace the generator yet.

## Phase 3: new domain contracts

### Goal

Represent research relationships without losing provenance.

### Work

Introduce pure or mostly pure models equivalent to:

```csharp
public sealed record SubjectEvidence(
    ResearchProjectDef Project,
    Def Subject,
    ResearchRelation Relation,
    SubjectRole Role,
    EvidenceSource Source,
    Def SourceDef,
    float Confidence);

public sealed record OpportunitySpec(
    OpportunityKey Key,
    ResearchProjectDef Project,
    ResearchOpportunityTypeDef Type,
    ResearchRelation Relation,
    RequirementSpec Requirement,
    float Importance,
    bool Rare,
    bool Freebie,
    IReadOnlyList<GenerationReason> Reasons);
```

1. Define stable semantic `OpportunityKey` composition from project, type,
   relation, requirement kind, and canonical subject identity.
2. Separate immutable `OpportunitySpec` from mutable saved progress.
3. Model requirements as values rather than polymorphic saved behavior where
   possible.
4. Add generation-reason and rejection-reason types for diagnostics.
5. Keep adapters capable of constructing legacy `ResearchOpportunity` objects
   temporarily.

### Exit criteria

- Domain types have no dependency on maps, pawns, UI, or Harmony.
- Equal semantic opportunities produce equal stable keys.
- Existing execution still uses the legacy model through adapters.

### Resume prompt

> Implement Phase 3 of `REWRITE_PLAN.md`. Add the immutable opportunity,
> evidence, requirement, and key contracts while leaving runtime behavior on
> the legacy generator.

## Phase 4: immutable Def index and evidence graph

### Goal

Scan loaded Defs once and retain why each subject relates to each project.

### Work

1. Build indexes for projects, direct and hidden prerequisites, recipes,
   products, recipe users, unlocks, construction materials, plants, fuels,
   `requiredAnalyzed`, and explicit special opportunities.
2. Detect prerequisite cycles iteratively or with bounded graph traversal and
   report each cycle once.
3. Preserve ingredient filters as compact evidence. Do not eagerly expand every
   broad `ThingFilter` into hundreds of tasks.
4. Annotate every edge with role and source, for example direct unlock, direct
   recipe product, ingredient, facility, cost material, fuel, or ancestor
   outcome.
5. Normalize explicit and inferred alternates into stable equivalence groups.
6. Construct the index after all Defs and patches are loaded, then treat it as
   immutable for the session.

### Tests

- Cyclic, self-referential, null, hidden, and unusual modded Def fixtures.
- Broad ingredient filters remain compact.
- Index output is deterministic regardless of source enumeration order.
- No map or pawn scanning occurs during index construction.

### Exit criteria

- A project query returns evidence with complete provenance.
- No repeated global `DefDatabase` scans occur during ordinary ticks.
- The legacy generator remains the active player-facing generator.

### Resume prompt

> Implement Phase 4 of `REWRITE_PLAN.md`: build the immutable Def index and
> provenance-preserving evidence graph. Keep the legacy generator active.

## Phase 5: opportunity rules

### Goal

Convert evidence into independently testable opportunity candidates.

### Work

1. Define `IOpportunityRule` and a registry with deterministic ordering.
2. Implement general analysis rules.
3. Implement specific rules for medicine analysis, drug analysis, drug trials,
   surgery prototypes, production prototypes, plants, terrain, fuels, social
   opportunities, theory, schematics, books, and explicit specials.
4. Make rules conservative when evidence is weak:
   - generic `Analyse` favors explicit required analysis, direct unlocks, and
     direct recipe products;
   - `AnalyseMedicine` requires a meaningful medicine outcome or clinical use;
   - `TrialDrug` requires a directly relevant ingestible drug, not an incidental
     ingredient-filter member;
   - `PrototypeSurgery` requires a directly gated, meaningful surgery whose
     other static prerequisites can be met.
5. Keep static generation separate from current-game availability. For example,
   a surgery opportunity may exist while reporting that no eligible patient is
   currently available.
6. Add a `DefModExtension` allowing content mods to force, suppress, replace, or
   reweight tasks and subjects.
7. Keep the current handcrafted special-opportunity XML format working through
   an adapter.

### Exit criteria

- Each rule has positive, negative, and malformed-input tests.
- Explicit metadata overrides inference.
- Unknown content falls back to generic analysis or theory without exceptions.

### Resume prompt

> Implement Phase 5 of `REWRITE_PLAN.md`. Add tested opportunity rules,
> especially Analyse, AnalyseMedicine, TrialDrug, and PrototypeSurgery. Do not
> switch player-facing generation yet.

## Phase 6: normalization, scoring, and selection

### Goal

Produce a small, relevant, deterministic opportunity set.

### Work

1. Score candidates using evidence strength, relation, feasibility, explicit
   importance, and confidence.
2. Canonicalize equivalent subjects and recipes before deduplication.
3. Deduplicate by semantic `OpportunityKey` while merging generation reasons.
4. Add configurable per-category and per-rule limits.
5. Select for diversity so one broad recipe does not dominate the entire list.
6. Sort deterministically using explicit scores and stable Def identifiers.
7. Preserve current category point budgets after selection.
8. Add bounded diagnostics answering:
   - why was this task generated?
   - why was another candidate rejected?
   - which explicit override affected it?

### Exit criteria

- Repeated generation produces byte-for-byte equivalent semantic snapshots.
- Broad filters and alternate families cannot create unbounded task lists.
- No duplicate semantic keys exist.
- Balance tests show unchanged total category/project budgets unless an
  intentional difference is documented.

### Resume prompt

> Implement Phase 6 of `REWRITE_PLAN.md`: normalize, score, deduplicate, limit,
> and explain new-generator candidates while preserving current point budgets.

## Phase 7: shadow-mode comparison

### Goal

Exercise the new generator without changing gameplay.

### Work

1. Run old and new generators when the selected project changes.
2. Compare semantic results and write bounded development diagnostics.
3. Classify differences as intended improvement, missing compatibility, or bug.
4. Run comparisons over vanilla, DLC, selected content mods, and a heavy mod
   list.
5. Add approved differences to tests; fix unexplained differences.
6. Measure cold index construction and warm per-project generation separately.

Shadow mode must never duplicate jobs, progress, UI entries, or saved state.

### Exit criteria

- All unexplained differences are resolved or recorded as explicit blockers.
- Generation has no per-tick global scans.
- Diagnostics remain bounded and can be disabled in normal play.
- The new results are demonstrably more relevant for the selected fixtures.

### Resume prompt

> Implement Phase 7 of `REWRITE_PLAN.md`. Run the new generator in non-player-
> facing shadow mode, compare it with legacy output, and stop after resolving or
> documenting every material difference.

## Phase 8: opportunity service and save migration

### Goal

Make the new specifications authoritative while preserving old saves.

### Work

1. Introduce one per-game `IOpportunityService` owning active specifications,
   mutable progress state, queries, regeneration, and progress application.
2. Add a versioned save root and store progress by `OpportunityKey`.
3. Load legacy opportunity objects into migration DTOs.
4. Reconcile legacy state to new specifications by project, type, relation, and
   canonical subject.
5. Define and test the unmatched-state policy. Prefer crediting safe aggregate
   progress or retaining an orphan record over silently corrupting a save.
6. Preserve prototypes and special state through a separate migration path.
7. Remove forced regeneration from ordinary load paths unless the saved schema
   requires it.

### Tests

- New-save round trip.
- Every Phase 0 old-save fixture.
- Missing mods and renamed/missing Defs.
- Project switch followed by save/load.
- Partially completed opportunities and settings changes.

### Exit criteria

- The new service is authoritative for opportunity state.
- Old fixtures load without exceptions or silent research loss.
- Saving again writes only the new schema after successful migration.

### Resume prompt

> Implement Phase 8 of `REWRITE_PLAN.md`: introduce the central opportunity
> service and versioned save migration, prove old-save fixtures, and stop before
> migrating prototyping internals.

## Phase 9: analysis and clinical execution

### Goal

Route existing player actions through the new service.

### Work

1. Migrate theory and bench/field analysis queries.
2. Replace WorkGiver static opportunity caches with service queries and
   explicitly invalidated per-map indexes.
3. Migrate medicine tending/surgery events.
4. Migrate ingestion and drug-trial events.
5. Migrate books, tooling, faction lectures, interrogation, and other chunk or
   continuous progress handlers.
6. Centralize progress modifiers, storyteller speed, XP, motes, caps, and
   project completion in one progress application path.
7. Add handler-level feature flags and startup self-checks where Harmony targets
   are optional or version-sensitive.

### Exit criteria

- Each action advances exactly one intended semantic opportunity.
- No legacy static opportunity cache remains on migrated paths.
- Changing projects invalidates jobs and queries safely.
- Save/load during or after a job behaves correctly.

### Resume prompt

> Implement Phase 9 of `REWRITE_PLAN.md`: migrate analysis and event-driven
> clinical/research handlers to the new opportunity service, with focused and
> live evidence for each migrated action.

## Phase 10: UI and settings

### Goal

Make all presentation and configuration consume the new read model.

### Work

1. Create UI-facing immutable view models rather than exposing saved state.
2. Migrate the main research window, opportunity rows, category totals, icons,
   rarity, availability, and tooltips.
3. Display generation reasons and dynamic unavailability reasons in developer
   mode.
4. Migrate presets and sparse overrides.
5. Reconcile opportunities by stable key when settings change rather than
   blindly discarding progress.
6. Preserve translation keys where the player-facing wording is unchanged.

### Exit criteria

- Compact and expanded UI modes match the new service state.
- Settings changes have a defined, tested progress policy.
- No UI code scans Def databases or maps to regenerate opportunities.

### Resume prompt

> Implement Phase 10 of `REWRITE_PLAN.md`: move UI and settings onto immutable
> view models from the new service and verify progress reconciliation.

## Phase 11: prototyping containment or replacement

### Goal

Make prototyping optional, testable, and resilient to mod conflicts.

### Work

1. Inventory every prototype state transition: designator or bill visibility,
   blueprint, frame, unfinished item, completed product, terrain, surgery,
   success/failure, cancellation, project switch, and save/load.
2. Move prototype tracking behind an `IPrototypeService` with stable keys.
3. Keep per-map state map-owned and serializable; eliminate unrelated global
   static state.
4. Prefer narrow prefixes/postfixes and stable public APIs over transpilers.
5. Where a transpiler is unavoidable, use semantic `CodeMatcher` patterns,
   verify the match count at startup, and disable only that prototype feature
   when the target shape is unknown.
6. Remove hard-coded local-variable slots and duplicate patches.
7. Rebuild experimental surgery discovery from the new surgery rule and dynamic
   patient feasibility query.
8. Verify cancellation removes or converts all prototype artifacts according
   to an explicit policy.

### Exit criteria

- The core research system works with prototyping disabled.
- A failed prototype patch degrades gracefully with one actionable warning.
- Every prototype transition and save/load boundary has automated or live
  scenario evidence.

### Resume prompt

> Implement Phase 11 of `REWRITE_PLAN.md`. Isolate and replace prototyping
> behind its own service, avoid brittle transpilers where possible, and prove
> each state transition before proceeding.

## Phase 12: heavy-mod-list and compatibility hardening

### Goal

Prove that unusual content cannot explode generation cost or break the mod.

### Work

1. Generate synthetic databases with tens of thousands of `ThingDef`s and
   recipes, thousands of projects, broad filters, null optional fields, cyclic
   prerequisites, hidden recipes, virtual products, and missing Def references.
2. Establish measured performance budgets from the test machine for:
   - one-time index construction;
   - warm generation for one project;
   - project switching;
   - per-tick overhead while nothing changes;
   - allocations and log volume.
3. Add real integration fixtures for representative large framework/content
   mods.
4. Audit Harmony owners and patch conflicts for every runtime hook.
5. Verify alternate grouping across mods and allow explicit cross-mod metadata.
6. Ensure warnings are deduplicated and identify the responsible Def and mod.
7. Confirm that removed mods and missing Defs do not make saves unloadable.

### Exit criteria

- Opportunity count is bounded independently of broad-filter cardinality.
- Ordinary ticks perform no generator work when state is unchanged.
- Malformed content produces bounded diagnostics rather than exceptions.
- The selected heavy list completes startup, project switching, and save/load.

### Resume prompt

> Implement Phase 12 of `REWRITE_PLAN.md`: add synthetic and real heavy-mod-list
> coverage, establish performance budgets, and fix only evidence-backed
> compatibility problems.

## Phase 13: live validation and release readiness

### Goal

Prove real player-visible behavior through its complete game transitions.

### Work

1. Build and package through repository-native wrappers and audit the payload.
2. Deploy only through the guarded deploy workflow.
3. Start with the minimal validation profile: Harmony, Core, explicitly required
   DLCs, RimBridgeServer when available, and Research Reinvented.
4. Exercise theory, bench analysis, field analysis, medicine analysis, a drug
   trial, recipe prototype, construction prototype, and prototype surgery for
   their real durations.
5. Test project switching during active jobs and prototypes.
6. Save and reload before, during, and after representative opportunities.
7. Repeat critical scenarios with the chosen heavy mod list.
8. Inspect bounded logs, opportunity state, research progress, job state, and
   deployed hashes.
9. Update player-facing release notes for completed behavioral changes.
10. Treat release and publishing as a separate explicitly requested workflow.

### Exit criteria

- Minimal and heavy profiles both prove the intended behavior and opposite
  regression cases.
- Old and new saves load and continue correctly.
- Installed binaries match the verified build.
- The package contains no private/development-only assemblies or symbols.
- Remaining limitations are documented before any release request.

### Resume prompt

> Perform Phase 13 of `REWRITE_PLAN.md`. Build, guarded-deploy, and prove the
> complete research workflows in minimal and heavy profiles. Do not publish or
> release unless I explicitly request it.

## Suggested first future request

Copy this into a new session:

> Open `C:\Users\alexv\RiderProjects\ResearchReinvented\REWRITE_PLAN.md` and
> begin Phase 0. Follow the repository's RimWorld development instructions,
> preserve unrelated changes, complete only Phase 0, and stop with the evidence
> and decisions needed before Phase 1.

For later phases, replace `Phase 0` with the next unchecked phase. Before doing
so, verify that all earlier exit criteria still hold in the current branch.
