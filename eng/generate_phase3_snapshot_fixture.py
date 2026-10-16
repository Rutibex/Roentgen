#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import struct
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'tests/fixtures/phase3-empty.rgsnap'
SEED = 7_351_982_471


def string(value: str) -> bytes:
    encoded = unicodedata.normalize('NFC', value).encode('utf-8')
    return b'\x08' + struct.pack('>i', len(encoded)) + encoded


def int32(value: int) -> bytes:
    return b'\x02' + struct.pack('>i', value)


def uint64(value: int) -> bytes:
    return b'\x05' + struct.pack('>Q', value)


def double(value: float) -> bytes:
    if value == 0.0:
        value = 0.0
    return b'\x07' + struct.pack('>d', value)


def count(value: int) -> bytes:
    return b'\x0c' + struct.pack('>i', value)


def raw_bytes(value: bytes) -> bytes:
    return b'\x09' + struct.pack('>i', len(value)) + value


def checksum(value: bytes) -> bytes:
    if len(value) != 32:
        raise ValueError('checksum length')
    return b'\x0b' + value


entity_section = b''.join([
    string('roentgen.entity-registry'), int32(1), uint64(0), count(0)
])
state_section = b''.join([
    string('roentgen.simulation-state-store'), int32(1), uint64(0), count(0)
])
checksum_payload = b''.join([
    string('roentgen.simulation-state'), int32(1), uint64(0), count(2),
    string('entities.registry'), raw_bytes(entity_section),
    string('simulation.state-store'), raw_bytes(state_section),
])
state_checksum = hashlib.sha256(checksum_payload).digest()

payload = b''.join([
    string('roentgen.simulation-snapshot'),
    int32(1),
    uint64(SEED),
    int32(60),
    uint64(0),
    double(1.0),
    uint64(0),
    uint64(0),
    uint64(0),
    count(0),
    uint64(0),
    count(0),
    count(0),
    checksum(state_checksum),
])
file_bytes = b'RGSNAP01' + struct.pack('>q', len(payload)) + payload + hashlib.sha256(payload).digest()
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
OUTPUT.write_bytes(file_bytes)
print(f'Wrote {OUTPUT.relative_to(ROOT)} ({len(file_bytes)} bytes).')
print(f'State checksum: {state_checksum.hex()}')
print(f'File SHA-256: {hashlib.sha256(file_bytes).hexdigest()}')
