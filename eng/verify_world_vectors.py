#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DATA = json.loads((ROOT / "eng/world-golden-vectors.json").read_text(encoding="utf-8"))
ERRORS: list[str] = []


def require(condition: bool, message: str) -> None:
    if not condition:
        ERRORS.append(message)


def floor_divide(value: int, divisor: int) -> int:
    quotient = abs(value) // divisor
    if value < 0:
        quotient = -quotient
    remainder = value - quotient * divisor
    return quotient - 1 if remainder < 0 else quotient


def positive_modulo(value: int, divisor: int) -> int:
    remainder = value % divisor
    return remainder if remainder >= 0 else remainder + divisor


def main() -> int:
    require(DATA.get("schemaVersion") == 1, "World vector schema version must be 1.")
    profile = DATA["profile"]
    chunk_cm = profile["chunkSizeMeters"] * 100
    require(profile["worldWidthMeters"] % profile["chunkSizeMeters"] == 0, "World width is not divisible by chunk size.")
    require(profile["worldDepthMeters"] % profile["chunkSizeMeters"] == 0, "World depth is not divisible by chunk size.")
    require(profile["chunkCountX"] == profile["worldWidthMeters"] // profile["chunkSizeMeters"], "Chunk count X mismatch.")
    require(profile["chunkCountZ"] == profile["worldDepthMeters"] // profile["chunkSizeMeters"], "Chunk count Z mismatch.")
    require(profile["maximumChunkX"] - profile["minimumChunkX"] + 1 == profile["chunkCountX"], "Chunk X range mismatch.")
    require(profile["maximumChunkZ"] - profile["minimumChunkZ"] + 1 == profile["chunkCountZ"], "Chunk Z range mismatch.")

    expected_lods = [profile["nearChunkSamples"], (profile["nearChunkSamples"] - 1) // 2 + 1, (profile["nearChunkSamples"] - 1) // 4 + 1]
    require(profile["lodSampleCounts"] == expected_lods, "LOD sample counts do not follow the Phase 6 rule.")
    expected_spacings = [chunk_cm // (samples - 1) for samples in expected_lods]
    require(profile["lodSpacingCentimeters"] == expected_spacings, "LOD spacings do not span exactly one chunk.")

    for vector in DATA["floorDivision"]:
        actual_q = floor_divide(vector["value"], vector["divisor"])
        actual_r = positive_modulo(vector["value"], vector["divisor"])
        require(actual_q == vector["quotient"], f"Floor quotient mismatch for {vector['value']}.")
        require(actual_r == vector["remainder"], f"Positive remainder mismatch for {vector['value']}.")

    for vector in DATA["sharedEdges"]:
        lod = vector["lod"]
        spacing = profile["lodSpacingCentimeters"][lod]
        samples = profile["lodSampleCounts"][lod]
        if "leftChunk" in vector:
            left_x = vector["leftChunk"][0] * chunk_cm + (samples - 1) * spacing
            right_x = vector["rightChunk"][0] * chunk_cm
            require(left_x == right_x == vector["leftEdgeWorldXCentimeters"], "X shared edge coordinate mismatch.")
            expected = [vector["leftChunk"][1] * chunk_cm + index * spacing for index in (0, 1, (samples - 1) // 2, samples - 2, samples - 1)]
            require(expected == vector["sampleWorldZCentimeters"], "X seam sample coordinates mismatch.")
        else:
            back_z = vector["backChunk"][1] * chunk_cm + (samples - 1) * spacing
            front_z = vector["frontChunk"][1] * chunk_cm
            require(back_z == front_z == vector["backEdgeWorldZCentimeters"], "Z shared edge coordinate mismatch.")
            expected = [vector["backChunk"][0] * chunk_cm + index * spacing for index in (0, 1, (samples - 1) // 2, samples - 2, samples - 1)]
            require(expected == vector["sampleWorldXCentimeters"], "Z seam sample coordinates mismatch.")

    profile_source = (ROOT / "src/Roentgen.World/WorldGenerationProfile.cs").read_text(encoding="utf-8")
    for literal in ("2048", "128", "65", "16", "MaximumCpuResidentBytes", "MaximumPendingGenerationRequests"):
        require(literal in profile_source, f"World profile source is missing {literal}.")

    if ERRORS:
        print("World golden-vector verification FAILED:")
        for item in ERRORS:
            print(f"  - {item}")
        return 1
    print("World coordinate, LOD, and seam golden vectors verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
