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
    names = {item["name"] for group in ("runtimeProjects", "testProjects", "toolProjects") for item in projects.get(group, [])}
    require(projects.get("schemaVersion") >= 6, "eng/projects.json must use schemaVersion 6 or later.")
    for name in ("Roentgen.World", "Roentgen.Physics", "Roentgen.World.Tests", "Roentgen.Physics.Tests", "Roentgen.WorldLab"):
        require(name in names, f"{name} is not declared in eng/projects.json.")
    require("Roentgen.WorldLab" in projects.get("executableProjects", []), "WorldLab must be executable.")

    world_project = source("src/Roentgen.World/Roentgen.World.csproj")
    physics_project = source("src/Roentgen.Physics/Roentgen.Physics.csproj")
    require("MonoGame" not in world_project, "Roentgen.World must remain MonoGame-free.")
    require("MonoGame" not in physics_project, "Roentgen.Physics must remain MonoGame-free.")
    for folder in ("src/Roentgen.World", "src/Roentgen.Physics"):
        for path in (ROOT / folder).glob("*.cs"):
            require("Microsoft.Xna.Framework" not in path.read_text(encoding="utf-8"), f"{path.relative_to(ROOT)} leaks MonoGame into authoritative world code.")

    required_sources = (
        "src/Roentgen.World/WorldCoordinates.cs",
        "src/Roentgen.World/WorldGenerationProfile.cs",
        "src/Roentgen.World/RegionalTopologyGenerator.cs",
        "src/Roentgen.World/RoadGraphGenerator.cs",
        "src/Roentgen.World/TerrainChunkCompiler.cs",
        "src/Roentgen.World/TerrainStreamingCoordinator.cs",
        "src/Roentgen.Physics/TerrainCollisionRegistry.cs",
        "src/Roentgen.Rendering/TerrainRenderingBridge.cs",
        "src/Roentgen.Rendering/TerrainChunkRenderRegistry.cs",
        "src/Roentgen.App/Phase7WorldScene.cs",
        "tools/Roentgen.WorldLab/Program.cs",
    )
    for item in required_sources:
        source(item)

    coordinates = source("src/Roentgen.World/WorldCoordinates.cs")
    require("FloorDivide" in coordinates and "PositiveModulo" in coordinates, "Negative world coordinate helpers are incomplete.")
    topology = source("src/Roentgen.World/RegionalTopologyGenerator.cs")
    for domain in ("world.topology", "height", "moisture", "contamination", "industrial"):
        require(domain in topology, f"Topology generator does not expose domain {domain}.")
    require("RoadGraphGenerator.FindPath" in topology, "Topology does not route roads at regional scale.")
    require("LandmarkReservation" in topology, "Topology does not reserve Phase 7 parcels.")

    chunk = source("src/Roentgen.World/TerrainChunkCompiler.cs")
    require("profile.LodSampleCounts" in chunk, "Terrain compiler does not build configured LODs.")
    require("worldX" in chunk and "worldZ" in chunk, "Terrain compiler is not sampling absolute world coordinates.")
    require("CancellationToken" in chunk and "ThrowIfCancellationRequested" in chunk, "Terrain compiler does not support bounded cancellation.")
    height_evaluator = source("src/Roentgen.World/TerrainHeightEvaluator.cs")
    require("Int128" in height_evaluator, "Road projection is not protected from squared-distance overflow.")
    streaming = source("src/Roentgen.World/TerrainStreamingCoordinator.cs")
    for token in ("PriorityQueue", "SemaphoreSlim", "MaximumPendingGenerationRequests", "MaximumCpuResidentBytes", "Cancelled", "AwaitingUpload", "GenerationTicket"):
        require(token in streaming, f"Streaming coordinator lacks {token}.")

    terrain_rendering = source("src/Roentgen.Rendering/TerrainRenderingBridge.cs")
    require("AddSkirt" in terrain_rendering and "includeSkirts" in terrain_rendering, "Terrain rendering does not close mixed-LOD boundaries with skirts.")
    upload = source("src/Roentgen.Rendering/RenderResourceUploadQueue.cs")
    for token in ("_maximumPendingOperations", "_maximumPendingBytes", "MaximumMilliseconds", "IsCurrent", "RenderUploadPriority"):
        require(token in upload, f"Bounded render upload queue lacks {token}.")
    app = source("src/Roentgen.App/RoentgenGame.cs")
    require("Phase9WorldScene" in app or "Phase8WorldScene" in app or "Phase7WorldScene" in app, "Application does not launch the current micro-region scene.")

    schemas = {
        "schemas/world-generation-profile.schema.json": "samples/world/phase6-world-profile.json",
    }
    for schema_path, sample_path in schemas.items():
        schema = json.loads(source(schema_path))
        sample = json.loads(source(sample_path))
        jsonschema.Draft202012Validator(schema).validate(sample)
    for schema_path in ("schemas/regional-topology.schema.json", "schemas/terrain-chunk-manifest.schema.json", "schemas/terrain-streaming-telemetry.schema.json"):
        jsonschema.Draft202012Validator.check_schema(json.loads(source(schema_path)))

    if ERRORS:
        print("Phase 6 world contract verification FAILED:")
        for item in ERRORS:
            print(f"  - {item}")
        return 1
    print("Phase 6 world, collision, streaming, rendering, schemas, and tool contracts verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
