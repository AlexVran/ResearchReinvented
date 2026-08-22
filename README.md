
# Research Reinvented    [![Badge License]][License]   [![Badge Mod]][RimWorld]

*Adds a more interesting research system.*

<br>

## Dependencies

- **[Harmony]**

<br>


<!----------------------------------------------------------------------------->

[RimWorld]: https://store.steampowered.com/app/294100/RimWorld/
[Harmony]: https://github.com/pardeike/HarmonyRimWorld

[License]: LICENSE


<!---------------------------------{ Badges }---------------------------------->

[Badge License]: https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge
[Badge Mod]: https://img.shields.io/badge/Mod-RimWorld-cecece?style=for-the-badge

## Development

The rewrite targets RimWorld 1.6 only. A normal build never deploys the mod.

- `scripts/build-quiet.ps1` restores and builds without touching a game install.
- `scripts/check.ps1` runs the repository checks.
- `scripts/deploy.ps1` performs an explicit guarded local deployment.
- `scripts/package.ps1` creates the audited public ZIP.
