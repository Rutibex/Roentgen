#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

REQUIRED = [
    "src/Roentgen.Contracts/CombatContracts.cs",
    "src/Roentgen.Entities/CombatState.cs",
    "src/Roentgen.Combat/CombatDefinitions.cs",
    "src/Roentgen.Combat/CombatMath.cs",
    "src/Roentgen.Combat/CombatQueryWorld.cs",
    "src/Roentgen.Combat/DamageResolver.cs",
    "src/Roentgen.Combat/CombatEncounterRuntime.cs",
    "src/Roentgen.Combat/CombatCommands.cs",
    "src/Roentgen.Combat/CombatEvents.cs",
    "src/Roentgen.Combat/CombatSimulationSystem.cs",
    "src/Roentgen.AI/BasicEnemyCombatAi.cs",
    "src/Roentgen.App/Phase10CombatController.cs",
    "src/Roentgen.App/Phase10InputAdapter.cs",
    "tests/Roentgen.Combat.Tests/Roentgen.Combat.Tests.csproj",
    "tools/Roentgen.CombatLab/Roentgen.CombatLab.csproj",
    "schemas/combat-state.schema.json",
    "schemas/combat-actor-state.schema.json",
    "schemas/weapon-definition.schema.json",
    "schemas/armor-profile.schema.json",
    "samples/combat/phase10-encounter.json",
]


def main() -> int:
    failures: list[str] = []
    for item in REQUIRED:
        if not (ROOT / item).is_file():
            failures.append(f"missing {item}")

    checks = {
        "src/Roentgen.Combat/CombatEncounterRuntime.cs": [
            "PendingCombatAction", "ResolveHitscan", "SweepMelee", "UpdateProjectiles", "Explode", "ApplyOngoingStatuses", "PersistTo", "RestoreFrom"
        ],
        "src/Roentgen.Combat/DamageResolver.cs": [
            "ArmorStopped", "ConditionPermille", "BodyPartFunctionalState.Crippled", "CharacterActionKind.Dying"
        ],
        "src/Roentgen.Combat/CombatSimulationSystem.cs": [
            "CombatAttackCommandHandler", "CombatEntitySynchronizationSystem", "CombatPersistenceSystem"
        ],
        "src/Roentgen.AI/BasicEnemyCombatAi.cs": [
            "DetectionRangeMillimeters", "QueueReload", "ActorRetreated", "legImpairment"
        ],
        "src/Roentgen.App/RoentgenGame.cs": [
            "Phase 10 Real-time Combat", "Phase10CombatController", "BuildDebugLines"
        ],
    }
    for relative, terms in checks.items():
        text = (ROOT / relative).read_text(encoding="utf-8")
        for term in terms:
            if term not in text:
                failures.append(f"{relative} missing contract marker {term!r}")

    combat_text = "\n".join(path.read_text(encoding="utf-8") for path in (ROOT / "src/Roentgen.Combat").rglob("*.cs"))
    for forbidden in ("Microsoft.Xna.Framework", "System.Random", "Random.Shared", "Guid.NewGuid", "DateTime.Now"):
        if forbidden in combat_text:
            failures.append(f"Roentgen.Combat contains forbidden dependency/API {forbidden}")

    projects = json.loads((ROOT / "eng/projects.json").read_text(encoding="utf-8"))
    all_projects = {item["name"]: item for group in ("runtimeProjects", "testProjects", "toolProjects") for item in projects[group]}
    for name in ("Roentgen.Combat", "Roentgen.Combat.Tests", "Roentgen.CombatLab"):
        if name not in all_projects:
            failures.append(f"eng/projects.json missing {name}")
    combat_project = ET.parse(ROOT / "src/Roentgen.Combat/Roentgen.Combat.csproj").getroot()
    package_names = [node.attrib.get("Include", "") for node in combat_project.iter("PackageReference")]
    if any("MonoGame" in name for name in package_names):
        failures.append("Roentgen.Combat must remain headless and cannot reference MonoGame")

    if re.search(r"State\.Tick\s*==\s*action\.ImpactTick", (ROOT / "src/Roentgen.Combat/CombatEncounterRuntime.cs").read_text(encoding="utf-8")):
        failures.append("action resolution must tolerate restored or skipped ticks; use >= with ImpactResolved")

    if failures:
        print("Combat contract verification FAILED:")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("Combat contract verification PASSED.")
    print(f"  Required artifacts: {len(REQUIRED)}")
    print("  Headless combat boundary: enforced")
    print("  Unified real-time/Focus resolver markers: present")
    return 0


if __name__ == "__main__":
    sys.exit(main())
