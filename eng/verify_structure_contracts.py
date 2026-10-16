#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
from pathlib import Path

import jsonschema

ROOT = Path(__file__).resolve().parents[1]
ERRORS: list[str] = []


def require(condition: bool, message: str) -> None:
    if not condition:
        ERRORS.append(message)


def source(path: str) -> str:
    file = ROOT / path
    require(file.is_file(), f"Missing {path}.")
    return file.read_text(encoding="utf-8") if file.is_file() else ""


def main() -> int:
    projects = json.loads(source("eng/projects.json"))
    require(projects.get("schemaVersion") >= 8, "eng/projects.json must use schemaVersion 8 or later.")
    names = {item["name"] for group in ("runtimeProjects", "testProjects", "toolProjects") for item in projects.get(group, [])}
    for name in ("Roentgen.World", "Roentgen.Navigation", "Roentgen.Physics", "Roentgen.Rendering", "Roentgen.Structures.Tests", "Roentgen.StructureLab"):
        require(name in names, f"{name} is not declared in eng/projects.json.")
    require("Roentgen.StructureLab" in projects.get("executableProjects", []), "StructureLab must be executable.")

    for project in ("src/Roentgen.World/Roentgen.World.csproj", "src/Roentgen.Navigation/Roentgen.Navigation.csproj", "src/Roentgen.Physics/Roentgen.Physics.csproj"):
        require("MonoGame" not in source(project), f"{project} must remain MonoGame-free.")

    authoritative_folders = (ROOT / "src/Roentgen.World/Structures", ROOT / "src/Roentgen.Navigation", ROOT / "src/Roentgen.Physics")
    for folder in authoritative_folders:
        for path in folder.glob("*.cs"):
            require("Microsoft.Xna.Framework" not in path.read_text(encoding="utf-8"), f"{path.relative_to(ROOT)} leaks MonoGame into authoritative structure code.")

    required = (
        "src/Roentgen.World/Structures/BuildingGrammar.cs",
        "src/Roentgen.World/Structures/SettlementPlanCompiler.cs",
        "src/Roentgen.World/Structures/StructureBlueprintCompiler.cs",
        "src/Roentgen.World/Structures/StructurePlans.cs",
        "src/Roentgen.World/Structures/StructureValidation.cs",
        "src/Roentgen.World/Structures/StructureRepairEngine.cs",
        "src/Roentgen.World/Structures/StructureStreamingCoordinator.cs",
        "src/Roentgen.World/Structures/MicroRegionStructureCampaign.cs",
        "src/Roentgen.Navigation/StructureNavigationEmitter.cs",
        "src/Roentgen.Navigation/AutomatedStructureTraversalValidator.cs",
        "src/Roentgen.Physics/StructureCollision.cs",
        "src/Roentgen.Physics/StructureCollisionRegistry.cs",
        "src/Roentgen.Rendering/StructureRenderingBridge.cs",
        "src/Roentgen.Rendering/StructureGpuRegistry.cs",
        "src/Roentgen.App/Phase7WorldScene.cs",
        "tools/Roentgen.StructureLab/Program.cs",
        "tests/Roentgen.Structures.Tests/StructureGenerationTests.cs",
    )
    for item in required:
        source(item)

    compiler = source("src/Roentgen.World/Structures/StructureBlueprintCompiler.cs")
    for token in ("AssignRequirements", "ArchitecturalGeometryCompiler.Compile", "BuildDamage", "BuildReservations", "new StructureValidator", "new StructureRepairEngine"):
        require(token in compiler, f"Structure compiler lacks {token}.")
    require(compiler.index("AssignRequirements") < compiler.index("ArchitecturalGeometryCompiler.Compile"), "Compiler does not establish room topology before geometry.")

    catalog = source("src/Roentgen.World/Structures/StructureGrammarCatalog.cs")
    for grammar in ("prospect-clinic", "small-residence", "boarding-house", "salvage-workshop", "warehouse", "market-shelter", "guard-post", "blackwater-intake", "meridian-relay", "generic-shelter"):
        require(grammar in catalog, f"Initial grammar catalog lacks {grammar}.")

    validator = source("src/Roentgen.World/Structures/StructureValidation.cs")
    for code in ("STR-ID-001", "STR-ROOM-003", "STR-NAV-001", "STR-NAV-003", "STR-RES-002", "STR-GEO-002", "STR-STREAM-002"):
        require(code in validator, f"Structure validator lacks invariant {code}.")
    repair = source("src/Roentgen.World/Structures/StructureRepairEngine.cs")
    for token in ("ApplyLowestCostRepair", "AddExteriorDoor", "ReconnectRoom", "RelocateReservation", "ApplySafeFallback", "RepairHistory"):
        require(token in repair, f"Repair engine lacks {token}.")

    navigation = source("src/Roentgen.Navigation/StructureNavigationEmitter.cs")
    for token in ("NavigationCell", "NavigationPortal", "NavigationLink", "CoverPoint", "RestrictedArea", "FlankingLink"):
        require(token in navigation or token in source("src/Roentgen.Navigation/InteriorNavigation.cs"), f"Navigation emission lacks {token}.")
    collision = source("src/Roentgen.Physics/StructureCollision.cs") + source("src/Roentgen.Physics/StructureCollisionRegistry.cs")
    require("StructureCollisionCompiler" in collision and "SweepCapsule" in collision and "Raycast" in collision, "Static structure collision contracts are incomplete.")
    streaming = source("src/Roentgen.World/Structures/StructureStreamingCoordinator.cs")
    for token in ("Channel.CreateBounded", "Epoch", "MaximumResidentCpuBytes", "StaleResults", "CancelledRequests"):
        require(token in streaming, f"Interior streaming lacks {token}.")
    rendering = source("src/Roentgen.Rendering/StructureRenderingBridge.cs")
    require("GroupBy" in rendering and "StructureCellLayer" in rendering and "StructureMaterialKind" in rendering, "Structure render batching is incomplete.")
    app = source("src/Roentgen.App/RoentgenGame.cs")
    require("Phase9WorldScene" in app or "Phase8WorldScene" in app or "Phase7WorldScene" in app, "Application does not launch the Phase 7 scene.")

    schema_paths = (
        "schemas/settlement-physical-program.schema.json",
        "schemas/settlement-plan.schema.json",
        "schemas/building-grammar.schema.json",
        "schemas/structure-instance.schema.json",
        "schemas/room-graph.schema.json",
        "schemas/damage-history.schema.json",
        "schemas/structure-navigation.schema.json",
        "schemas/structure-validation-report.schema.json",
        "schemas/structure-repair-history.schema.json",
    )
    for schema_path in schema_paths:
        jsonschema.Draft202012Validator.check_schema(json.loads(source(schema_path)))
    grammar_schema = json.loads(source("schemas/building-grammar.schema.json"))
    grammar_sample = json.loads(source("samples/structures/prospect-clinic.grammar.json"))
    jsonschema.Draft202012Validator(grammar_schema).validate(grammar_sample)
    profile = json.loads(source("samples/structures/phase7-structure-profile.json"))
    require(profile.get("validationStructureTarget") == 10000, "Phase 7 profile must retain the 10,000-structure exit target.")
    require(profile.get("regressionSites") == ["Prospect Clinic", "Blackwater Intake", "Meridian Relay"], "Permanent regression sites changed unexpectedly.")

    if ERRORS:
        print("Phase 7 structure contract verification FAILED:")
        for item in ERRORS:
            print(f"  - {item}")
        return 1
    print("Phase 7 structures, interiors, navigation, collision, streaming, schemas, tests, and StructureLab contracts verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
