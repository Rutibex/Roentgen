#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import struct
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VECTORS = ROOT / "eng" / "character-golden-vectors.json"


def write_string(value: str) -> bytes:
    data = unicodedata.normalize("NFC", value).encode("utf-8")
    return b"\x08" + struct.pack(">i", len(data)) + data


def write_int32(value: int) -> bytes:
    return b"\x02" + struct.pack(">i", value)


def write_uint64(value: int) -> bytes:
    return b"\x05" + struct.pack(">Q", value)


def write_stable_id(value: str) -> bytes:
    return b"\x0a" + bytes.fromhex(value)


def stable_id(namespace_id: str, key: str) -> str:
    payload = write_string("roentgen.stable-id") + write_int32(1) + write_string(namespace_id) + write_string(key)
    return hashlib.sha256(payload).digest()[:16].hex()


def child_id(parent: str, kind: str, key: str) -> str:
    payload = write_string("roentgen.stable-id.child") + write_int32(1) + write_stable_id(parent) + write_string(kind) + write_string(key)
    return hashlib.sha256(payload).digest()[:16].hex()


def domain_seed(campaign_seed: int, domain: str) -> int:
    normalized = domain.strip().lower()
    payload = write_string("roentgen.domain-seed") + write_int32(1) + write_uint64(campaign_seed) + write_string(normalized)
    return struct.unpack(">Q", hashlib.sha256(payload).digest()[:8])[0]


def normalized_weights(values: list[float]) -> list[float]:
    clamped = [max(0.0, value) for value in values]
    total = sum(clamped)
    return [1.0, 0.0, 0.0, 0.0] if total <= 0.000001 else [value / total for value in clamped]


def main() -> int:
    vectors = json.loads(VECTORS.read_text(encoding="utf-8"))
    failures: list[str] = []
    for item in vectors["characters"]:
        actual = stable_id(item["namespace"], item["key"])
        if actual != item["expected"]:
            failures.append(f"stable ID {item['key']}: {actual} != {item['expected']}")
        actual_domain = domain_seed(vectors["campaignSeed"], item["characterRootDomain"])
        if actual_domain != item["expectedRootDomainSeed"]:
            failures.append(f"domain seed {item['key']}: {actual_domain} != {item['expectedRootDomainSeed']}")
        actual_head = child_id(actual, "bone", "head")
        if actual_head != item["headBoneId"]:
            failures.append(f"head bone ID {item['key']}: {actual_head} != {item['headBoneId']}")

    for vector in vectors["skinWeightVectors"]:
        actual = normalized_weights(vector["input"])
        if any(abs(a - b) > 1e-9 for a, b in zip(actual, vector["expected"], strict=True)):
            failures.append(f"skin weights {vector['input']}: {actual} != {vector['expected']}")

    if failures:
        print("Character vector verification FAILED:")
        for failure in failures:
            print(f"  - {failure}")
        return 1

    print("Character vector verification PASSED.")
    print(f"  Regression character IDs: {len(vectors['characters'])}")
    print(f"  Skin-weight vectors: {len(vectors['skinWeightVectors'])}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
