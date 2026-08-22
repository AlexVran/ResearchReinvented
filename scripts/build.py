from __future__ import annotations

import argparse
import sys

from common import PROJECT, ROOT, artifact_dll, run_quiet


def main() -> int:
    parser = argparse.ArgumentParser(description="Restore and build without deploying.")
    parser.add_argument("configuration", nargs="?", choices=("Debug", "Release"), default="Release")
    args = parser.parse_args()

    run_quiet([sys.executable, str(ROOT / "scripts" / "sync_metadata.py")])
    lock_file = PROJECT.parent / "packages.lock.json"
    if not lock_file.is_file():
        print("missing Source/packages.lock.json; run dotnet restore --use-lock-file first", file=sys.stderr)
        return 1
    run_quiet(["dotnet", "restore", str(PROJECT), "--locked-mode", "--nologo", "--verbosity", "quiet"])
    run_quiet(
        [
            "dotnet",
            "build",
            str(PROJECT),
            "--configuration",
            args.configuration,
            "--no-restore",
            "--nologo",
            "--verbosity",
            "quiet",
        ]
    )

    output = artifact_dll(args.configuration)
    if not output.is_file():
        print(f"build did not create {output}", file=sys.stderr)
        return 1
    forbidden = {"0harmony.dll", "rimbridgeserver.sdk.dll", "rimbridgeserver.annotations.dll"}
    leaked = [path.name for path in output.parent.glob("*.dll") if path.name.lower() in forbidden]
    if leaked:
        print(f"forbidden runtime dependencies in build output: {', '.join(leaked)}", file=sys.stderr)
        return 1
    print(f"build ok: {args.configuration} -> {output.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"build failed: {error}", file=sys.stderr)
        raise SystemExit(1)
