#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import struct
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VECTORS = ROOT / "eng" / "combat-golden-vectors.json"


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
    return hashlib.sha256(write_string("roentgen.stable-id") + write_int32(1) + write_string(namespace_id) + write_string(key)).digest()[:16].hex()


def child_id(parent: str, kind: str, key: str) -> str:
    return hashlib.sha256(write_string("roentgen.stable-id.child") + write_int32(1) + write_stable_id(parent) + write_string(kind) + write_string(key)).digest()[:16].hex()


def combat_roll(seed: int, actor: str, object_id: str, tick: int, sequence: int, domain: str) -> int:
    payload = (
        write_string("roentgen.combat-roll") + write_int32(1) + write_uint64(seed) +
        write_stable_id(actor) + write_stable_id(object_id) + write_uint64(tick) +
        write_uint64(sequence) + write_string(domain)
    )
    return int.from_bytes(hashlib.sha256(payload).digest()[:8], "big") % 1000


def main() -> int:
    vectors = json.loads(VECTORS.read_text(encoding="utf-8"))
    failures: list[str] = []
    actor = stable_id(vectors["actor"]["namespace"], vectors["actor"]["key"])
    if actor != vectors["actor"]["expected"]:
        failures.append(f"actor stable ID {actor} != {vectors['actor']['expected']}")
    weapon = child_id(actor, vectors["weapon"]["kind"], vectors["weapon"]["key"])
    if weapon != vectors["weapon"]["expected"]:
        failures.append(f"weapon stable ID {weapon} != {vectors['weapon']['expected']}")
    for item in vectors["rolls"]:
        actual = combat_roll(vectors["campaignSeed"], actor, weapon, item["tick"], item["sequence"], item["domain"])
        if actual != item["expected"]:
            failures.append(f"{item['domain']} tick {item['tick']} sequence {item['sequence']}: {actual} != {item['expected']}")
    if vectors["boundedState"]["feedbackCapacity"] != 128:
        failures.append("feedback capacity vector must remain 128")
    if failures:
        print("Combat vector verification FAILED:")
        for failure in failures:
            print(f"  - {failure}")
        return 1
    print("Combat vector verification PASSED.")
    print(f"  Deterministic rolls: {len(vectors['rolls'])}")
    print(f"  Actor ID: {actor}")
    print(f"  Weapon ID: {weapon}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
