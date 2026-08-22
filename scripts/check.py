from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET

from common import ROOT, run_quiet


def main() -> int:
    parser = argparse.ArgumentParser(description="Run repository metadata, fixture, test, and build checks.")
    parser.add_argument("--skip-build", action="store_true")
    parser.add_argument("--skip-tests", action="store_true")
    args = parser.parse_args()

    run_quiet([sys.executable, str(ROOT / "scripts" / "sync_metadata.py")])
    parsed = 0
    for path in sorted(ROOT.rglob("*.xml")):
        if any(part in {".runs", "artifacts"} for part in path.parts):
            continue
        try:
            ET.parse(path)
        except ET.ParseError as error:
            raise RuntimeError(f"malformed XML: {path.relative_to(ROOT)}: {error}") from error
        parsed += 1
    if not args.skip_tests:
        run_quiet([sys.executable, str(ROOT / "scripts" / "test.py"), "Release"])
    if not args.skip_build:
        run_quiet([sys.executable, str(ROOT / "scripts" / "build.py"), "Debug"])
        run_quiet([sys.executable, str(ROOT / "scripts" / "build.py"), "Release"])
    print(f"check ok: {parsed} XML files, characterization tests, RimWorld 1.6-only metadata")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"check failed: {error}", file=sys.stderr)
        raise SystemExit(1)
