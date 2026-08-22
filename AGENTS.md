# Research Reinvented rewrite instructions

This checkout is the isolated rewrite. Do not modify or deploy from the original
`ResearchReinvented` checkout while working here.

## Current contract

- RimWorld 1.6 is the only supported game version.
- `Directory.Build.props` is the version owner.
- Keep `PeteTimesSix.ResearchReinvented`, public Def names, and assembly identity
  stable unless a migration phase explicitly changes them.
- Ordinary builds write only to `artifacts/`; they never deploy.
- Local deploys must be explicit, guarded, and must omit
  `About/PublishedFileId.txt`.
- Public packages may preserve the existing Workshop ID but must exclude source,
  symbols, SDKs, private Harmony, local fixtures, and development metadata.
- Do not edit gameplay code during Phase 1.

## Deferred work

- Phase 2 owns the independently runnable characterization-test project and
  global-service seams.
- The Argonic Core prototype completion failure is a required Phase 11 fix.
- Optional Anomaly integration should use its Study workflow to study items tied
  to a research project; Anomaly must never become a hard dependency.

Follow the shared `steam-mods` development, deployment, and release rules.
