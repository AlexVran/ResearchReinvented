from __future__ import annotations

import os
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Iterable, Sequence


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "Source" / "ResearchReinvented.csproj"
ASSEMBLY_NAME = "ResearchReinvented.dll"


class CommandError(RuntimeError):
    pass


def mod_version() -> str:
    root = ET.parse(ROOT / "Directory.Build.props").getroot()
    value = root.findtext(".//ModVersion")
    if not value:
        raise RuntimeError("Directory.Build.props has no ModVersion")
    return value.strip()


def artifact_dll(configuration: str = "Release") -> Path:
    return ROOT / "artifacts" / "bin" / configuration / ASSEMBLY_NAME


def run_quiet(command: Sequence[str], *, cwd: Path = ROOT) -> None:
    result = subprocess.run(
        command,
        cwd=cwd,
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )
    if result.returncode:
        if result.stdout:
            print(result.stdout.rstrip(), file=sys.stderr)
        raise CommandError(f"command failed ({result.returncode}): {' '.join(command)}")


def iter_files(paths: Iterable[Path]) -> Iterable[Path]:
    for path in paths:
        if path.is_file():
            yield path
        elif path.is_dir():
            yield from (candidate for candidate in path.rglob("*") if candidate.is_file())


def rimworld_processes() -> list[str]:
    if os.name == "nt":
        result = subprocess.run(
            ["tasklist", "/FO", "CSV", "/NH"],
            check=False,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
        )
        lowered = result.stdout.lower()
        return [name for name in ("RimWorldWin64.exe", "RimWorld.exe") if name.lower() in lowered]

    result = subprocess.run(
        ["pgrep", "-fl", "RimWorld"],
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
    )
    return [line for line in result.stdout.splitlines() if line.strip()]
