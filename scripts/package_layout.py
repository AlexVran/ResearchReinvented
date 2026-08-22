from __future__ import annotations

import shutil
from pathlib import Path

from common import ASSEMBLY_NAME, ROOT, artifact_dll, iter_files


PUBLIC_DIRECTORIES = (
    ROOT / "About",
    ROOT / "ModSpecific" / "v1.6",
    ROOT / "Textures",
    ROOT / "v1.6",
)
PUBLIC_FILES = (ROOT / "LICENSE", ROOT / "LoadFolders.xml", ROOT / "README.md")
FORBIDDEN_NAMES = {
    "0harmony.dll",
    "rimbridgeserver.sdk.dll",
    "rimbridgeserver.annotations.dll",
    ".ds_store",
}
FORBIDDEN_SUFFIXES = {".pdb", ".mdb", ".cs", ".csproj", ".sln"}


def should_skip(relative: Path, *, include_published_id: bool) -> bool:
    lower = relative.as_posix().lower()
    if lower == "v1.6/assemblies/researchreinvented.dll":
        return True
    if not include_published_id and lower == "about/publishedfileid.txt":
        return True
    return relative.name.startswith("._")


def stage_payload(destination: Path, *, include_published_id: bool) -> list[Path]:
    dll = artifact_dll("Release")
    if not dll.is_file():
        raise FileNotFoundError(f"missing Release assembly: {dll}")
    if destination.exists():
        shutil.rmtree(destination)
    destination.mkdir(parents=True)

    copied: list[Path] = []
    for source in iter_files((*PUBLIC_DIRECTORIES, *PUBLIC_FILES)):
        relative = source.relative_to(ROOT)
        if should_skip(relative, include_published_id=include_published_id):
            continue
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        copied.append(relative)

    assembly_relative = Path("v1.6") / "Assemblies" / ASSEMBLY_NAME
    assembly_target = destination / assembly_relative
    assembly_target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dll, assembly_target)
    copied.append(assembly_relative)
    audit_payload(destination, include_published_id=include_published_id)
    return sorted(copied)


def audit_payload(payload: Path, *, include_published_id: bool) -> None:
    files = [path for path in payload.rglob("*") if path.is_file()]
    if not files:
        raise RuntimeError("staged payload is empty")
    if not (payload / "v1.6" / "Assemblies" / ASSEMBLY_NAME).is_file():
        raise RuntimeError("staged payload has no current Release assembly")
    if not (payload / "About" / "About.xml").is_file():
        raise RuntimeError("staged payload has no About/About.xml")
    published = payload / "About" / "PublishedFileId.txt"
    if include_published_id and not published.is_file():
        raise RuntimeError("public package lost About/PublishedFileId.txt")
    if not include_published_id and published.exists():
        raise RuntimeError("local deploy contains About/PublishedFileId.txt")

    forbidden: list[str] = []
    for path in files:
        relative = path.relative_to(payload)
        if path.name.lower() in FORBIDDEN_NAMES or path.suffix.lower() in FORBIDDEN_SUFFIXES:
            forbidden.append(relative.as_posix())
        if relative.parts and relative.parts[0].lower() in {
            "source",
            "tests",
            "tools",
            "scripts",
            "artifacts",
            "docs",
            ".runs",
        }:
            forbidden.append(relative.as_posix())
        if relative.parts and relative.parts[0].lower() in {"v1.3", "v1.4", "v1.5"}:
            forbidden.append(relative.as_posix())
    if forbidden:
        raise RuntimeError("forbidden public payload files: " + ", ".join(sorted(set(forbidden))))
