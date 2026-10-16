#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
cue = (ROOT / "src/Roentgen.Music.Playroma/RoentgenCueGenerator.cs").read_text(encoding="utf-8")
project = (ROOT / "src/Roentgen.Music.Playroma/Roentgen.Music.Playroma.csproj").read_text(encoding="utf-8")

def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(f"Build Fix 19 verification FAILED: {message}")

require("using Roentgen.Contracts;" in cue, "RoentgenCueGenerator does not import the namespace that defines StableId")
require("using Roentgen.Core;" in cue, "RoentgenCueGenerator does not import the namespace that defines StableIdFactory")
require(cue.count("private static readonly StableId ") >= 6, "regional StableId declarations unexpectedly changed")
require('ProjectReference Include="..\\Roentgen.Contracts\\Roentgen.Contracts.csproj"' in project, "music project no longer references Roentgen.Contracts")
require('ProjectReference Include="..\\Roentgen.Core\\Roentgen.Core.csproj"' in project, "music project no longer references Roentgen.Core")
print("Build Fix 19 verification PASSED: StableId namespace and project references are present.")
