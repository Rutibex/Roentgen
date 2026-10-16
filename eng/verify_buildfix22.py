#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
motor = (ROOT / "src/Roentgen.Physics/KinematicPlayerMotor.cs").read_text(encoding="utf-8")

required = "long stepRise=raised.Valid?(long)raised.HeightMillimeters-state.FeetPosition.YMillimeters:long.MinValue;"
if required not in motor:
    raise SystemExit("Build Fix 22 verification FAILED: stepRise is not calculated in 64-bit coordinate space")
if "int stepRise=raised.Valid?raised.HeightMillimeters-state.FeetPosition.YMillimeters:int.MinValue;" in motor:
    raise SystemExit("Build Fix 22 verification FAILED: narrowing assignment that caused CS0266 is still present")
if "stepRise>10&&stepRise<=_settings.MaximumStepMillimeters" not in motor:
    raise SystemExit("Build Fix 22 verification FAILED: Build Fix 21 positive-rise wall protection was lost")
print("Build Fix 22 verification PASSED: player step-rise arithmetic remains 64-bit and wall protection is intact.")
