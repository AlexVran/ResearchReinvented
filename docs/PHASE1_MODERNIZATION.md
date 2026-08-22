# Phase 1 repository and build modernization

Status: complete on branch `codex/rewrite-phase-1`.

Phase 1 modernizes the isolated rewrite's development shell and artwork without
changing gameplay code. The original checkout remains untouched.

## Decisions

- The payload is flattened to the repository root while preserving its installed
  relative paths.
- Per the owner's Phase 1 decision, the rewrite supports RimWorld 1.6 only.
  The 1.3, 1.4, and 1.5 payloads and load routes are removed from this worktree.
- `Directory.Build.props` owns version `1.6`; metadata synchronization checks
  `About.xml`, `Manifest.xml`, `.steam-mods.json`, and `LoadFolders.xml`.
- Ordinary Debug and Release builds write only under ignored `artifacts/` and
  never deploy.
- Public packaging retains the existing `PublishedFileId.txt`. Explicit local
  deployment omits it.
- The independent test project remains Phase 2 work. Creating a ceremonial
  empty test project in Phase 1 would not protect behavior.

## Compatibility invariants

- Package ID: `PeteTimesSix.ResearchReinvented` (unchanged).
- Assembly name: `ResearchReinvented` (unchanged).
- Assembly version: `1.0.0.0` (unchanged for binary compatibility).
- File version: `1.6.0.0`.
- A SHA-256 comparison of all 157 gameplay C# files against the original
  checkout reported zero content differences. Only build/assembly metadata was
  modernized.
- The Argonic Core prototype failure remains captured for the Phase 11 fix.
- Optional Anomaly Study-based item research remains a later execution feature;
  Anomaly is not a hard dependency.

## Verification evidence

- Fresh locked restore succeeded with SDK `10.0.302` and
  `Source/packages.lock.json`.
- Quiet Debug and Release builds succeeded. Outputs were
  `395264` and `357376` bytes respectively during verification.
- `scripts/check.py` parsed 129 XML files and built both configurations.
- A normal build left the real RimWorld `Mods/ResearchReinvented` target absent.
- Public package creation was byte-for-byte deterministic across consecutive
  runs at the verified commit; the handoff records that commit's output hash.
- Shared `modctl audit-package` returned `ok`.
- Shared `modctl validate` returned zero errors. Its sole warning is the
  intentionally deferred Phase 2 independent test project.
- Disposable guarded deployment staged 180 files under an ignored fake `Mods`
  root, omitted `PublishedFileId.txt`, retained the preview and icon, replaced
  an existing target cleanly, and left no stage, backup, or lock artifacts.
- A directory not literally named `Mods` was rejected.
- The final art dimensions and exact texture provenance are recorded in
  `docs/ASSETS.md`.

## Remaining risks

Phase 1 does not claim runtime gameplay proof because it deliberately changes
no gameplay behavior and does not deploy to the game. Phase 2 must introduce
independently runnable characterization tests before architecture replacement.
Later phases still own save migration, the Argonic Core prototype repair,
optional Anomaly Study integration, heavy-list compatibility, and full live
duration/state-transition validation.
