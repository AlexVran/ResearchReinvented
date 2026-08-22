# Phase 5 opportunity rules

Status: complete on branch `codex/rewrite-phase-5`.

Phase 5 converts immutable Phase 4 evidence into independently testable
opportunity candidates. It does not normalize, score, deduplicate, limit, or
select candidates; run shadow comparisons; migrate saves; change execution or
UI; replace prototypes; repair the Argonic Core prototype; or add Anomaly Study
integration. `MasterFactory` remains the only player-facing generator.

No item is added to `PENDING_RELEASE_NOTES.md` because the rules are inactive
rewrite infrastructure and player-visible behavior is unchanged.

## Pure rule boundary

`IOpportunityRule`, `OpportunityRuleContext`, `OpportunityCandidate`,
`OpportunityRuleResult`, and `OpportunityRuleRegistry` live under
`Source/Domain/Rules/` and compile directly into the independent headless test
assembly. The default registry sorts rules by numeric order and stable rule id.
Candidate output is sorted by rule order and semantic `OpportunityKey`; semantic
deduplication remains assigned to Phase 6.

The registry contains bounded rules for:

- theory, schematic/book study, and techprints;
- static social opportunity forms without querying current factions or pawns;
- direct ThingDef analysis, including medicine, drugs, ingestible drug trials,
  pawns/corpses, plants, food, and generic fallback analysis;
- recipe ingredients, production facilities, construction materials, and fuel;
- harvested products and terrain analysis;
- surgery, production, ThingDef construction, and terrain prototypes;
- the existing `SpecialResearchOpportunityDef` XML format.

Every candidate retains a structured generation reason naming its evidence
source and source Def. Rejections remain separate structured values. Runtime
availability is deliberately absent: synthetic faction requirement identities
represent static social opportunity forms, while actual faction, patient,
medicine, map, and pawn availability remains a later dynamic query.

## Conservative inference

Generic and specialized direct analysis considers only explicit
`requiredAnalyzed`, direct unlock, and recipe-product evidence. Unknown indexed
ThingDefs therefore fall back to `Analyse`, while a project with no analyzable
content still has theory and schematic candidates.

Medicine analysis requires either strong direct medicine evidence or a fixed
medicine ingredient in a surgery. Drug trials require a strongly related ThingDef
that is both a drug and ingestible. Ingredient and fuel evidence can produce
their narrower analysis forms, but never a drug trial. A compact `ThingFilter`
evidence subject is never expanded or treated as one of its allowed ThingDefs.

Surgery prototypes require a directly gated, meaningful, non-blacklisted
surgery whose remaining research prerequisites are the project or its known
ancestors. Production and construction prototypes apply equivalent static
gating checks. These checks do not inspect maps, patients, bills, pawns, or
current research availability.

## Immutable semantic traits

The one-time loaded-Def adapter now captures only the static traits needed by
the rules. Thing snapshots retain medicine, drug, ingestible, raw-food, plant,
pawn/corpse, haulability, buildability, instant-build, flammability, and corpse
relationships. Recipe snapshots retain surgery, meaningful-output, and
blacklist traits. Terrain snapshots retain soil and player-buildable traits.
The pure index publishes those immutable values and includes them in its
canonical snapshot. No live `Def` reaches the rule layer.

## Explicit author metadata

`ResearchOpportunityOverrides` is an optional `DefModExtension` containing
ordered snapshot entries with `Force`, `Suppress`, `Replace`, or `Reweight`
actions. An entry may match project, opportunity type, subject, and relation;
replacement and reweight actions may change the task, subject, or importance.
Extensions attached to projects default their project, while extensions on
ThingDefs, RecipeDefs, or TerrainDefs default their subject.

The runtime adapter captures extension entries into the immutable index. The
registry applies them after inference, records `ExplicitMetadata` reasons, and
returns diagnostics for incomplete force/replacement entries or invalid
weights. Explicit suppressions remove inferred candidates. Handcrafted special
XML is adapted separately and preserves explicit type, relation flags and
override, alternate mode, importance, rarity, and freebie state.

## Headless tests

Fifteen Phase 5 tests cover:

- stable registry and candidate ordering;
- theory, schematic/book, techprint, and static social candidates;
- positive and negative medicine and drug analysis, including direct
  ingestible drug trials;
- flesh/non-flesh pawn and corpse-analysis type mapping;
- clinical medicine from fixed surgery ingredients and rejection of incidental
  broad-filter drug membership;
- ingredients, facilities, costs, harvested products, plants, and typed fuels;
- soil, buildable floor, and generic terrain analysis;
- positive surgery, production, construction, and terrain prototypes, plus
  unrelated prerequisites and blacklisted recipes;
- explicit-special type, relation, alternates, importance, rarity, and freebie
  preservation;
- force, suppress, replace, and reweight metadata precedence;
- null, malformed, disabled-special, missing-project, cyclic, and unknown Def
  inputs without exceptions;
- byte-for-byte candidate enumeration under reversed loaded-Def order.

The 46 earlier characterization/domain/index tests continue to pass. The Phase
5 total is 61 independently runnable tests.

## Verification evidence

Final repository-native evidence:

- `scripts/test.ps1 Release`: 61 passed, 0 failed, independently runnable;
- `scripts/check.ps1`: 129 XML files parsed, all tests passed, and quiet Debug
  and Release production builds passed;
- Debug DLL: 560,640 bytes, SHA-256
  `AD089C04385F29CC0635392148499ACB3954AE51AC1EDFC6E14F253A0B76E2E2`;
- Release DLL: 510,976 bytes, SHA-256
  `15C9822AAE9405B418B43B1C7223C32172CFB6F02B65305F550FAA1F9234FC9D`;
- assembly identity: `ResearchReinvented, Version=1.0.0.0`, file version
  `1.6.0.0`, product version `1.6`;
- package identity remains `PeteTimesSix.ResearchReinvented`;
- `scripts/package.ps1 --no-build` produced a 181-entry public ZIP with zero
  source, test, symbol, private Harmony, or RimBridge SDK/annotations entries;
- package SHA-256:
  `238FD8E8BE418DB1CC833A2E4E99BBD40C0E18EA75C11E798BEA565FEED8529A`;
- shared `modctl validate` reported zero errors and warnings, and shared
  `modctl audit-package` passed;
- no rule-registry call exists outside the headless tests, so the legacy
  generator remains authoritative;
- no deploy command ran and no RimWorld installation was modified.

## Exit criteria

- Deterministically ordered pure rules produce candidates and structured
  reasons for all Phase 5 opportunity families.
- Conservative rules reject weak drug/medicine/surgery evidence and never
  expand broad filters.
- Explicit extension metadata overrides inference, and the existing special
  XML contract remains supported through an adapter.
- Malformed, cyclic, null, broad-filter, unknown, and reordered inputs are
  bounded and tested without world-state services.
- The legacy generator remains the only player-facing generator.

Phase 6 normalization, scoring, selection, limits, deduplication, and bounded
diagnostic presentation may begin only as a separate change set.
