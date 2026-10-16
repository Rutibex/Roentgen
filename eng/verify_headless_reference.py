#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import struct
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / 'tests/fixtures/phase3-headless-golden.json'


def string(value: str) -> bytes:
    encoded = unicodedata.normalize('NFC', value).encode('utf-8')
    return b'\x08' + struct.pack('>i', len(encoded)) + encoded


def int32(value: int) -> bytes:
    return b'\x02' + struct.pack('>i', value)


def int64(value: int) -> bytes:
    return b'\x04' + struct.pack('>q', value)


def uint64(value: int) -> bytes:
    return b'\x05' + struct.pack('>Q', value)


def count(value: int) -> bytes:
    return b'\x0c' + struct.pack('>i', value)


def raw_bytes(value: bytes) -> bytes:
    return b'\x09' + struct.pack('>i', len(value)) + value


def checksum(value: bytes) -> bytes:
    return b'\x0b' + value


value_payload = int64(1)
value_checksum = hashlib.sha256(value_payload).digest()
entity_section = b''.join([string('roentgen.entity-registry'), int32(1), uint64(0), count(0)])
state_section = b''.join([
    string('roentgen.simulation-state-store'), int32(1), uint64(1), count(1),
    string('simulation.elapsed-ticks'), int32(1), raw_bytes(value_payload), checksum(value_checksum),
])
state_checksum_payload = b''.join([
    string('roentgen.simulation-state'), int32(1), uint64(1), count(2),
    string('entities.registry'), raw_bytes(entity_section),
    string('simulation.state-store'), raw_bytes(state_section),
])
actual = hashlib.sha256(state_checksum_payload).hexdigest()
expected = json.loads(FIXTURE.read_text(encoding='utf-8'))['finalChecksum']
if actual != expected:
    print(f'Headless reference mismatch: expected {expected}, actual {actual}')
    sys.exit(1)
print(f'Headless one-tick reference verified: {actual}')
