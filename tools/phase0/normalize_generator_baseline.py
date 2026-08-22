#!/usr/bin/env python3
"""Normalize a runtime collector report into a deterministic generator fixture."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path, help="Raw collector report JSON.")
    parser.add_argument("output", type=Path, help="Normalized fixture JSON.")
    parser.add_argument("--profile", required=True, help="Stable profile name.")
    parser.add_argument(
        "--projects",
        help="Comma-separated project defNames to retain; defaults to every captured project.",
    )
    return parser.parse_args()


def stable_digest(value: Any) -> str:
    payload = json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def normalize_requirement(requirement: dict[str, Any]) -> dict[str, Any]:
    identity = requirement.get("canonicalIdentity")
    if requirement.get("kind") == "ROComp_RequiresFaction" and identity:
        identity = identity.split(":", 1)[0]

    return {
        "kind": requirement.get("kind"),
        "canonicalIdentity": identity,
        "alternates": sorted(requirement.get("alternates") or []),
    }


def normalize_opportunity(opportunity: dict[str, Any]) -> dict[str, Any]:
    return {
        "project": opportunity.get("project"),
        "type": opportunity.get("type"),
        "relation": opportunity.get("relation"),
        "category": opportunity.get("category"),
        "maximumProgress": opportunity.get("maximumProgress"),
        "currentProgress": opportunity.get("currentProgress"),
        "importance": opportunity.get("importance"),
        "rare": opportunity.get("rare"),
        "freebie": opportunity.get("freebie"),
        "generationSource": opportunity.get("generationSource"),
        "requirement": normalize_requirement(opportunity["requirement"]),
    }


def normalize_project(project: dict[str, Any]) -> dict[str, Any]:
    opportunities = [normalize_opportunity(value) for value in project["opportunities"]]
    opportunities.sort(
        key=lambda value: (
            value["type"] or "",
            value["relation"] or "",
            value["requirement"]["kind"] or "",
            value["requirement"]["canonicalIdentity"] or "",
            tuple(value["requirement"]["alternates"]),
        )
    )
    categories = sorted(project["categoryStores"], key=lambda value: value["category"])
    return {
        "defName": project["defName"],
        "modPackageId": project.get("modPackageId"),
        "baseCost": project.get("baseCost"),
        "opportunityCount": len(opportunities),
        "opportunityDigest": stable_digest(opportunities),
        "opportunities": opportunities,
        "categoryStores": categories,
    }


def main() -> None:
    args = parse_args()
    report = json.loads(args.input.read_text(encoding="utf-8"))
    if report.get("error"):
        raise SystemExit(f"collector report contains an error: {report['error']}")

    selected = None
    if args.projects:
        selected = {value.strip() for value in args.projects.split(",") if value.strip()}

    projects = [
        normalize_project(project)
        for project in report["projects"]
        if selected is None or project["defName"] in selected
    ]
    projects.sort(key=lambda value: value["defName"])

    found = {project["defName"] for project in projects}
    missing_selected = sorted((selected or set()) - found)
    if missing_selected:
        raise SystemExit(f"selected projects were not captured: {', '.join(missing_selected)}")

    fixture = {
        "schema": "research-reinvented-legacy-generator-fixture-v1",
        "profile": args.profile,
        "gameVersion": report["gameVersion"],
        "loadedDefs": sorted(report["loadedDefs"], key=lambda value: value["defType"]),
        "packageIds": sorted(mod["packageId"] for mod in report["mods"]),
        "projects": projects,
    }
    fixture["projectDigest"] = stable_digest(projects)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(fixture, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
