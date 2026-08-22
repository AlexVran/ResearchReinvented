#!/usr/bin/env python3
"""Extract a stable Research Reinvented migration fixture from a RimWorld save."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


RR_MANAGER = "PeteTimesSix.ResearchReinvented.Managers.ResearchOpportunityManager"
PROTOTYPE_KEEPER = "PeteTimesSix.ResearchReinvented.Managers.PrototypeKeeper"
RR_PREFIX = "PeteTimesSix.ResearchReinvented."


def text(element: ET.Element | None, default: str | None = None) -> str | None:
    if element is None or element.text is None:
        return default
    value = element.text.strip()
    return value if value else default


def scalar(value: str | None):
    if value is None:
        return None
    if value in {"True", "False"}:
        return value == "True"
    try:
        return int(value)
    except ValueError:
        pass
    try:
        return float(value)
    except ValueError:
        return value


def xml_value(element: ET.Element):
    children = list(element)
    if not children:
        return scalar(text(element))
    if all(child.tag == "li" for child in children):
        return [xml_value(child) for child in children]
    result = {}
    for child in children:
        value = xml_value(child)
        if child.tag in result:
            existing = result[child.tag]
            if not isinstance(existing, list):
                existing = [existing]
            existing.append(value)
            result[child.tag] = existing
        else:
            result[child.tag] = value
    return result


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def category_map(payload_root: Path) -> dict[str, dict[str, str]]:
    result: dict[str, dict[str, str]] = {}
    opportunity_defs = payload_root / "v1.6" / "Defs" / "Opportunities"
    for path in sorted(opportunity_defs.glob("*.xml")):
        root = ET.parse(path).getroot()
        for node in root.iter():
            if not node.tag.endswith("ResearchOpportunityTypeDef"):
                continue
            def_name = text(node.find("defName"))
            if not def_name:
                continue
            categories = {}
            for relation in ("Direct", "Ancestor", "Descendant"):
                category = text(node.find(f"category_{relation}"))
                if category:
                    categories[relation] = category
            if categories:
                result[def_name] = categories
    return result


def opportunity_snapshot(node: ET.Element, categories: dict[str, dict[str, str]]) -> dict:
    relation = text(node.find("relation"), "Direct")
    opportunity_type = text(node.find("def"))
    type_categories = categories.get(opportunity_type or "", {})
    category = type_categories.get(relation or "Direct") or type_categories.get("Direct")
    requirement = node.find("requirement")
    requirement_class = requirement.attrib.get("Class", "") if requirement is not None else ""
    forced_rare_node = node.find("isForcedRare")
    forced_freebie_node = node.find("isForcedFreebie")
    return {
        "project": text(node.find("project")),
        "type": opportunity_type,
        "relation": relation,
        "category": category,
        "requirement": {
            "kind": requirement_class.rsplit(".", 1)[-1] if requirement_class else None,
            "facts": xml_value(requirement) if requirement is not None else None,
        },
        "maximumProgress": scalar(text(node.find("maximumProgress"), "0")),
        "currentProgress": scalar(text(node.find("currentProgress"), "0")),
        "importance": scalar(text(node.find("importance"), "0")),
        "legacyLoadId": scalar(text(node.find("loadID"), "-1")),
        "forcedRarePersisted": (
            scalar(text(forced_rare_node, "False")) if forced_rare_node is not None else None
        ),
        "forcedFreebiePersisted": (
            scalar(text(forced_freebie_node, "False")) if forced_freebie_node is not None else None
        ),
    }


def extract_save(save_path: Path, payload_root: Path) -> dict:
    categories = category_map(payload_root)
    manager = None
    prototype_keeper = None
    current_research_project = None
    mod_ids: list[str] = []

    for _, element in ET.iterparse(save_path, events=("end",)):
        if element.tag == "modIds" and not mod_ids:
            mod_ids = [value for child in element.findall("li") if (value := text(child))]
        elif element.tag == "currentProj" and current_research_project is None:
            current_research_project = text(element)
        elif element.tag == "li":
            class_name = element.attrib.get("Class")
            if class_name == RR_MANAGER:
                manager = element
            elif class_name == PROTOTYPE_KEEPER:
                prototype_keeper = element

    if manager is None:
        raise ValueError(f"{save_path} has no {RR_MANAGER} component")

    opportunities_parent = manager.find("_allGeneratedOpportunities")
    opportunities = []
    if opportunities_parent is not None:
        opportunities = [
            opportunity_snapshot(node, categories) for node in opportunities_parent.findall("li")
        ]

    generated_parent = manager.find("_allProjectsWithGeneratedOpportunities")
    generated_projects = []
    if generated_parent is not None:
        generated_projects = [
            value for node in generated_parent.findall("li") if (value := text(node))
        ]

    stores_parent = manager.find("_categoryStores")
    category_stores = []
    if stores_parent is not None:
        for node in stores_parent.findall("li"):
            category_stores.append(
                {
                    "project": text(node.find("project")),
                    "category": text(node.find("category")),
                    "researchPoints": scalar(
                        text(node.find("allResearchPoints"), text(node.find("researchPoints"), "0"))
                    ),
                }
            )

    prototype_ids = []
    if prototype_keeper is not None:
        prototypes = prototype_keeper.find("_prototypes")
        if prototypes is not None:
            prototype_ids = [value for node in prototypes.findall("li") if (value := text(node))]

    semantic_mod_digest = hashlib.sha256("\n".join(mod_ids).encode("utf-8")).hexdigest()
    return {
        "schema": "research-reinvented-legacy-save-baseline-v1",
        "source": {
            "saveSha256": sha256(save_path),
            "saveBytes": save_path.stat().st_size,
            "activeModCount": len(mod_ids),
            "activeModIdsSha256": semantic_mod_digest,
            "relevantMods": [
                mod_id
                for mod_id in mod_ids
                if "researchreinvented" in mod_id.lower()
                or mod_id.lower() in {"brrainz.harmony", "ludeon.rimworld"}
            ],
        },
        "state": {
            "researchManagerProject": current_research_project,
            "opportunityManagerProject": text(manager.find("currentProject")),
            "changeTicker": scalar(text(manager.find("changeTicker"), "-1")),
            "generatedProjects": generated_projects,
            "opportunities": opportunities,
            "categoryStores": category_stores,
            "prototypeReferenceIds": prototype_ids,
        },
        "serializationNotes": {
            "relationDefaultsToDirectWhenAbsent": True,
            "forcedRareIsOnlyWrittenWhenTrue": True,
            "forcedFreebieIsNotSerializedByLegacyCode": True,
            "runtimeRareAndFreebieMayAlsoComeFromLoadedDefModExtensions": True,
        },
    }


def extract_settings(settings_path: Path) -> dict:
    root = ET.parse(settings_path).getroot()
    settings = root.find("ModSettings")
    if settings is None:
        raise ValueError(f"{settings_path} has no ModSettings element")
    return {
        "settingsSha256": sha256(settings_path),
        "settings": xml_value(settings),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("save", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument(
        "--payload-root",
        type=Path,
        default=Path(__file__).resolve().parents[2] / "ResearchReinvented",
    )
    parser.add_argument("--settings", type=Path)
    args = parser.parse_args()

    result = extract_save(args.save.resolve(), args.payload_root.resolve())
    if args.settings:
        result["settings"] = extract_settings(args.settings.resolve())

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
