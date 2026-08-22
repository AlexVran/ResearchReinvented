from __future__ import annotations

import argparse
import sys

from common import ROOT, TEST_PROJECT, run_quiet


def main() -> int:
    parser = argparse.ArgumentParser(description="Run the independent characterization test project.")
    parser.add_argument("configuration", nargs="?", choices=("Debug", "Release"), default="Release")
    args = parser.parse_args()

    lock_file = TEST_PROJECT.parent / "packages.lock.json"
    if not lock_file.is_file():
        print("missing Source/Tests/packages.lock.json; run dotnet restore --use-lock-file first", file=sys.stderr)
        return 1

    run_quiet(["dotnet", "restore", str(TEST_PROJECT), "--locked-mode", "--nologo", "--verbosity", "quiet"])
    run_quiet(
        [
            "dotnet",
            "test",
            str(TEST_PROJECT),
            "--configuration",
            args.configuration,
            "--no-restore",
            "--nologo",
            "--verbosity",
            "quiet",
            "--logger",
            "console;verbosity=normal",
        ]
    )
    print(f"tests ok: {args.configuration} -> {TEST_PROJECT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"tests failed: {error}", file=sys.stderr)
        raise SystemExit(1)
