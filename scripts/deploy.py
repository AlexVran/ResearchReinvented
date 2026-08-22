from __future__ import annotations

import argparse
import os
import shutil
import sys
import uuid
from pathlib import Path

from common import ROOT, rimworld_processes, run_quiet
from package_layout import stage_payload


TARGET_NAME = "ResearchReinvented"


def is_link_like(path: Path) -> bool:
    return path.is_symlink() or bool(getattr(path, "is_junction", lambda: False)())


def validate_mods_root(value: Path) -> Path:
    if not value.exists() or not value.is_dir():
        raise ValueError(f"Mods directory does not exist: {value}")
    if is_link_like(value):
        raise ValueError(f"refusing linked Mods directory: {value}")
    resolved = value.resolve(strict=True)
    if resolved.name.lower() != "mods":
        raise ValueError(f"expected a directory named Mods, got: {resolved}")
    if resolved in {Path(resolved.anchor), Path.home().resolve()}:
        raise ValueError(f"unsafe Mods directory: {resolved}")
    return resolved


def main() -> int:
    parser = argparse.ArgumentParser(description="Guarded local RimWorld mod deployment.")
    parser.add_argument("--mods-dir", type=Path, required=True, help="existing RimWorld Mods directory")
    parser.add_argument("--no-build", action="store_true", help="reuse the existing Release artifact")
    args = parser.parse_args()

    running = rimworld_processes()
    if running:
        raise RuntimeError("refusing deployment while RimWorld is running: " + ", ".join(running))
    mods_root = validate_mods_root(args.mods_dir)
    target = mods_root / TARGET_NAME
    if target.parent != mods_root or target.resolve(strict=False).parent != mods_root:
        raise RuntimeError(f"unsafe deployment target: {target}")
    if target.exists() and is_link_like(target):
        raise RuntimeError(f"refusing linked deployment target: {target}")
    if not args.no_build:
        run_quiet([sys.executable, str(ROOT / "scripts" / "build.py"), "Release"])

    token = uuid.uuid4().hex
    stage = mods_root / f".{TARGET_NAME}.stage-{token}"
    backup = mods_root / f".{TARGET_NAME}.backup-{token}"
    lock = mods_root / f".{TARGET_NAME}.deploy.lock"
    lock_handle = None
    moved_existing = False
    try:
        lock_handle = open(lock, "x", encoding="utf-8")
        lock_handle.write(f"pid={os.getpid()}\n")
        lock_handle.flush()
        stage_payload(stage, include_published_id=False)
        if target.exists():
            target.rename(backup)
            moved_existing = True
        stage.rename(target)
        if backup.exists():
            shutil.rmtree(backup)
        print(f"deploy ok: {target}")
        return 0
    except FileExistsError as error:
        raise RuntimeError(f"another deployment holds {lock}") from error
    except Exception:
        if not target.exists() and moved_existing and backup.exists():
            backup.rename(target)
        raise
    finally:
        if stage.exists():
            shutil.rmtree(stage)
        if lock_handle is not None:
            lock_handle.close()
            lock.unlink(missing_ok=True)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"deploy failed: {error}", file=sys.stderr)
        raise SystemExit(1)
