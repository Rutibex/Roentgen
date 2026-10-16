#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import struct
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VECTORS = ROOT / 'eng' / 'determinism-golden-vectors.json'
MASK64 = (1 << 64) - 1
MASK32 = (1 << 32) - 1


def write_string(value: str | None) -> bytes:
    if value is None:
        return b'\x00'
    data = unicodedata.normalize('NFC', value).encode('utf-8')
    return b'\x08' + struct.pack('>i', len(data)) + data


def write_int32(value: int) -> bytes:
    return b'\x02' + struct.pack('>i', value)


def write_uint64(value: int) -> bytes:
    return b'\x05' + struct.pack('>Q', value)


def write_bool(value: bool) -> bytes:
    return b'\x01' + (b'\x01' if value else b'\x00')


def write_stable_id(value: str) -> bytes:
    return b'\x0a' + bytes.fromhex(value)


def stable_id(namespace_id: str, local_key: str) -> str:
    data = (
        write_string('roentgen.stable-id')
        + write_int32(1)
        + write_string(namespace_id)
        + write_string(local_key)
    )
    return hashlib.sha256(data).digest()[:16].hex()


def child_stable_id(parent: str, child_kind: str, local_key: str) -> str:
    data = (
        write_string('roentgen.stable-id.child')
        + write_int32(1)
        + write_stable_id(parent)
        + write_string(child_kind)
        + write_string(local_key)
    )
    return hashlib.sha256(data).digest()[:16].hex()


def domain_seed(campaign_seed: int, domain: str) -> tuple[int, str, int]:
    normalized = domain.strip().lower()
    data = (
        write_string('roentgen.domain-seed')
        + write_int32(1)
        + write_uint64(campaign_seed)
        + write_string(normalized)
    )
    return struct.unpack('>Q', hashlib.sha256(data).digest()[:8])[0], normalized, 1


def stream_seed(domain_value: int, domain: str, derivation_version: int, stream_name: str) -> tuple[int, int]:
    normalized = stream_name.replace('/', '.').strip().lower()
    data = (
        write_string('roentgen.random-stream')
        + write_int32(1)
        + write_string('pcg32-roentgen-v1')
        + write_uint64(domain_value)
        + write_string(domain)
        + write_int32(derivation_version)
        + write_string(normalized)
    )
    digest = hashlib.sha256(data).digest()
    return struct.unpack('>Q', digest[:8])[0], struct.unpack('>Q', digest[8:16])[0]


class Pcg32:
    def __init__(self, seed: int, sequence: int) -> None:
        self.state = 0
        self.increment = ((sequence << 1) | 1) & MASK64
        self._next_core()
        self.state = (self.state + seed) & MASK64
        self._next_core()

    def _next_core(self) -> int:
        old_state = self.state
        self.state = (old_state * 6364136223846793005 + self.increment) & MASK64
        xorshifted = (((old_state >> 18) ^ old_state) >> 27) & MASK32
        rotation = (old_state >> 59) & 31
        return ((xorshifted >> rotation) | (xorshifted << ((-rotation) & 31))) & MASK32

    def next_uint32(self) -> int:
        return self._next_core()


def main() -> int:
    vectors = json.loads(VECTORS.read_text(encoding='utf-8'))
    failures: list[str] = []

    for vector in vectors['stableIds']:
        actual = stable_id(vector['namespace'], vector['key'])
        if actual != vector['expected']:
            failures.append(f"stable ID {vector['namespace']}/{vector['key']}: {actual} != {vector['expected']}")

    for vector in vectors['childStableIds']:
        actual = child_stable_id(vector['parent'], vector['kind'], vector['key'])
        if actual != vector['expected']:
            failures.append(f"child stable ID {vector['kind']}/{vector['key']}: {actual} != {vector['expected']}")

    for vector in vectors['randomStreams']:
        domain_value, domain, version = domain_seed(vector['campaignSeed'], vector['domain'])
        if domain_value != vector['expectedDomainSeed']:
            failures.append(f"domain seed {vector['domain']}: {domain_value} != {vector['expectedDomainSeed']}")
        seed, sequence = stream_seed(domain_value, domain, version, vector['stream'])
        random = Pcg32(seed, sequence)
        actual = [random.next_uint32() for _ in range(len(vector['expectedUInt32']))]
        if actual != vector['expectedUInt32']:
            failures.append(f"random stream {vector['domain']}/{vector['stream']}: {actual} != {vector['expectedUInt32']}")

    if failures:
        print('Golden-vector verification FAILED:')
        for failure in failures:
            print(f'  - {failure}')
        return 1

    print('Golden-vector verification PASSED.')
    print(f"  Stable IDs: {len(vectors['stableIds'])}")
    print(f"  Child IDs: {len(vectors['childStableIds'])}")
    print(f"  Random streams: {len(vectors['randomStreams'])}")
    return 0


if __name__ == '__main__':
    sys.exit(main())
