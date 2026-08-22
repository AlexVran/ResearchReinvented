from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from common import ROOT, mod_version


ABOUT = ROOT / "About" / "About.xml"
MANIFEST = ROOT / "About" / "Manifest.xml"
STEAM_MODS = ROOT / ".steam-mods.json"
LOAD_FOLDERS = ROOT / "LoadFolders.xml"
DESCRIPTION = ROOT / "ModDescription.md"


def normalize_description(value: str) -> str:
    return "\n".join(line.strip() for line in value.strip().splitlines())


def expected_state() -> dict[str, object]:
    version = mod_version()
    return {
        "version": version,
        "supported": ["1.6"],
        "description": normalize_description(DESCRIPTION.read_text(encoding="utf-8")),
    }


def errors() -> list[str]:
    expected = expected_state()
    problems: list[str] = []

    about = ET.parse(ABOUT).getroot()
    if about.findtext("modVersion") != expected["version"]:
        problems.append("About/About.xml modVersion is stale")
    supported = [node.text for node in about.findall("./supportedVersions/li")]
    if supported != expected["supported"]:
        problems.append(f"About/About.xml supportedVersions is {supported!r}, expected ['1.6']")
    if normalize_description(about.findtext("description") or "") != expected["description"]:
        problems.append("About/About.xml description differs from ModDescription.md")

    manifest = ET.parse(MANIFEST).getroot()
    if manifest.findtext("version") != expected["version"]:
        problems.append("About/Manifest.xml version is stale")

    steam_mods = json.loads(STEAM_MODS.read_text(encoding="utf-8"))
    if steam_mods.get("mod", {}).get("version") != expected["version"]:
        problems.append(".steam-mods.json version is stale")
    if steam_mods.get("mod", {}).get("rimworldVersion") != "1.6":
        problems.append(".steam-mods.json must target only RimWorld 1.6")

    load_root = ET.parse(LOAD_FOLDERS).getroot()
    if [node.tag for node in list(load_root)] != ["v1.6"]:
        problems.append("LoadFolders.xml must contain only the v1.6 route")
    for legacy in ("v1.3", "v1.4", "v1.5"):
        if (ROOT / legacy).exists() or (ROOT / "ModSpecific" / legacy).exists():
            problems.append(f"legacy payload still exists: {legacy}")
    return problems


def write_state() -> None:
    expected = expected_state()
    about_tree = ET.parse(ABOUT)
    about = about_tree.getroot()
    about.find("modVersion").text = str(expected["version"])
    supported = about.find("supportedVersions")
    supported.clear()
    ET.SubElement(supported, "li").text = "1.6"
    about.find("description").text = "\n" + DESCRIPTION.read_text(encoding="utf-8").strip() + "\n\t"
    ET.indent(about_tree, space="\t")
    about_tree.write(ABOUT, encoding="utf-8", xml_declaration=True)

    manifest_tree = ET.parse(MANIFEST)
    manifest_tree.getroot().find("version").text = str(expected["version"])
    ET.indent(manifest_tree, space="\t")
    manifest_tree.write(MANIFEST, encoding="utf-8", xml_declaration=True)

    steam_mods = json.loads(STEAM_MODS.read_text(encoding="utf-8"))
    steam_mods["mod"]["version"] = expected["version"]
    steam_mods["mod"]["rimworldVersion"] = "1.6"
    STEAM_MODS.write_text(json.dumps(steam_mods, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Synchronize or check public mod metadata.")
    parser.add_argument("--write", action="store_true", help="write metadata from version owners")
    args = parser.parse_args()
    if args.write:
        write_state()
    problems = errors()
    if problems:
        print("metadata check failed:", file=sys.stderr)
        for problem in problems:
            print(f"- {problem}", file=sys.stderr)
        return 1
    print(f"metadata ok: version {mod_version()}, RimWorld 1.6 only")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
