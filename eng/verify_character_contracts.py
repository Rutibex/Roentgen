#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

REQUIRED = [
    "src/Roentgen.Contracts/CharacterContracts.cs",
    "src/Roentgen.Entities/CharacterState.cs",
    "src/Roentgen.Animation/SkeletonDefinition.cs",
    "src/Roentgen.Animation/CharacterMeshProduct.cs",
    "src/Roentgen.Animation/PoseBuffer.cs",
    "src/Roentgen.Animation/CharacterPoseRuntime.cs",
    "src/Roentgen.Animation/CpuSkinner.cs",
    "src/Roentgen.Generation/HumanoidCharacterGenerator.cs",
    "src/Roentgen.Generation/QuadrupedCreatureGenerator.cs",
    "src/Roentgen.Rendering/SkinnedVertex.cs",
    "src/Roentgen.Rendering/GpuSkinnedMesh.cs",
    "src/Roentgen.Rendering/DynamicCpuSkinnedMesh.cs",
    "src/Roentgen.App/Phase9WorldScene.cs",
    "tools/Roentgen.CreatureLab/Roentgen.CreatureLab.csproj",
    "tests/Roentgen.Characters.Tests/Roentgen.Characters.Tests.csproj",
    "src/Roentgen.App/Content/Effects/CharacterSkinning.fx",
    "src/Roentgen.App/Content/Effects/CharacterShadowSkinning.fx",
]


def main() -> int:
    failures: list[str] = []
    for item in REQUIRED:
        if not (ROOT / item).is_file():
            failures.append(f"missing {item}")

    checks = {
        "src/Roentgen.Animation/SkeletonDefinition.cs": ["48 bones", "BindWorldMatrices", "InverseBindWorldMatrices"],
        "src/Roentgen.Animation/CharacterMeshProduct.cs": ["SkinInfluences", "CharacterLodProduct", "FirstPersonArms"],
        "src/Roentgen.Animation/CharacterPoseRuntime.cs": ["ApplyLocomotion", "ApplyFootPlacement", "ApplyInjury", "ApplyQuadrupedLocomotion"],
        "src/Roentgen.Generation/HumanoidCharacterGenerator.cs": ["BuildSkeleton", "BuildFirstPersonArms", "BuildTargetParts", "EquipmentPoseProfile"],
        "src/Roentgen.Generation/QuadrupedCreatureGenerator.cs": ["front_l_upper", "rear_r", "Exposed Growth"],
        "src/Roentgen.Rendering/RendererFoundation.cs": ["DrawCharacterItem", "DrawCharacterShadowPass", "UploadCharacterNow"],
        "src/Roentgen.App/Phase9WorldScene.cs": ["clinic-caretaker", "blackwater-quadruped", "relay-technician"],
    }
    for relative, terms in checks.items():
        text = (ROOT / relative).read_text(encoding="utf-8")
        for term in terms:
            if term not in text:
                failures.append(f"{relative} missing contract marker {term!r}")

    projects = json.loads((ROOT / "eng/projects.json").read_text(encoding="utf-8"))
    all_names = {item["name"] for key in ("runtimeProjects", "testProjects", "toolProjects") for item in projects[key]}
    for name in ("Roentgen.Animation", "Roentgen.Generation", "Roentgen.CreatureLab", "Roentgen.Characters.Tests"):
        if name not in all_names:
            failures.append(f"eng/projects.json missing {name}")

    animation_project = (ROOT / "src/Roentgen.Animation/Roentgen.Animation.csproj").read_text(encoding="utf-8")
    if "Roentgen.Entities" in animation_project:
        failures.append("Roentgen.Animation must remain independent of persistent entity and world assemblies")

    deterministic_character_roots = [
        ROOT / "src/Roentgen.Animation",
        ROOT / "src/Roentgen.Entities",
        ROOT / "src/Roentgen.Generation",
        ROOT / "src/Roentgen.Rendering",
    ]
    character_text = "\n".join(
        path.read_text(encoding="utf-8")
        for source_root in deterministic_character_roots
        for path in source_root.rglob("*.cs")
    )
    if re.search(r"System\.Random|Random\.Shared", character_text):
        failures.append("non-deterministic System.Random use detected in character runtime")

    imported_character_suffixes = {".fbx", ".gltf", ".glb", ".dae", ".obj", ".bvh", ".anim", ".blend"}
    imported = [path.relative_to(ROOT).as_posix() for path in ROOT.rglob("*") if path.is_file() and path.suffix.lower() in imported_character_suffixes]
    if imported:
        failures.append(f"imported character model/animation assets detected: {', '.join(imported[:8])}")

    if failures:
        print("Character contract verification FAILED:")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("Character contract verification PASSED.")
    print(f"  Required artifacts: {len(REQUIRED)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
