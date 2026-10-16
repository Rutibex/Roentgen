#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
bridge = (ROOT / "src/Roentgen.App/Phase18AudioBridge.cs").read_text(encoding="utf-8")
world_project = (ROOT / "src/Roentgen.App/Roentgen.App.csproj").read_text(encoding="utf-8")
enums = (ROOT / "src/Roentgen.World/Structures/StructureEnums.cs").read_text(encoding="utf-8")

def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"Build Fix 20 verification FAILED: {message}")

require("using Roentgen.World.Structures;" in bridge, "Phase18AudioBridge does not import DoorLifecycleState namespace")
require("DoorLifecycleState" in bridge, "Phase18AudioBridge no longer uses DoorLifecycleState")
require("public enum DoorLifecycleState" in enums, "DoorLifecycleState declaration is missing")
require('ProjectReference Include="..\\Roentgen.World\\Roentgen.World.csproj"' in world_project, "Roentgen.App no longer references Roentgen.World")
print("Build Fix 20 verification PASSED: DoorLifecycleState namespace and project reference are present.")
