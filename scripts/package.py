from __future__ import annotations

import argparse
import shutil
import sys
import zipfile
from pathlib import Path

from common import ROOT, mod_version, run_quiet
from package_layout import stage_payload


ZIP_TIMESTAMP = (1980, 1, 1, 0, 0, 0)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build and create a deterministic public mod ZIP.")
    parser.add_argument("--no-build", action="store_true", help="reuse the existing Release artifact")
    parser.add_argument("--output", type=Path, help="ZIP path (default: dist/ResearchReinvented-<version>.zip)")
    args = parser.parse_args()
    if not args.no_build:
        run_quiet([sys.executable, str(ROOT / "scripts" / "build.py"), "Release"])

    stage_root = ROOT / ".runs" / "phase1" / "package-stage"
    payload = stage_root / "ResearchReinvented"
    stage_payload(payload, include_published_id=True)

    output = (args.output or ROOT / "dist" / f"ResearchReinvented-{mod_version()}.zip").resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(output.suffix + ".tmp")
    if temporary.exists():
        temporary.unlink()
    with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for source in sorted(path for path in payload.rglob("*") if path.is_file()):
            relative = Path("ResearchReinvented") / source.relative_to(payload)
            info = zipfile.ZipInfo(relative.as_posix(), ZIP_TIMESTAMP)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, source.read_bytes(), compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
    temporary.replace(output)
    shutil.rmtree(stage_root)
    print(f"package ok: {output.relative_to(ROOT) if output.is_relative_to(ROOT) else output}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"package failed: {error}", file=sys.stderr)
        raise SystemExit(1)
