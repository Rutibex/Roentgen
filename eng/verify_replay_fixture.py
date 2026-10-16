#!/usr/bin/env python3
from __future__ import annotations

import base64
import hashlib
import json
import struct
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / 'tests' / 'fixtures' / 'phase2-golden.replay.jsonl'


def ws(value: str | None) -> bytes:
    if value is None:
        return b'\x00'
    data = unicodedata.normalize('NFC', value).encode('utf-8')
    return b'\x08' + struct.pack('>i', len(data)) + data


def wi(value: int) -> bytes:
    return b'\x02' + struct.pack('>i', value)


def wu(value: int) -> bytes:
    return b'\x05' + struct.pack('>Q', value)


def wb(value: bytes) -> bytes:
    return b'\x09' + struct.pack('>i', len(value)) + value


def wid(value: str) -> bytes:
    return b'\x0a' + bytes.fromhex(value)


def wsum(value: str) -> bytes:
    return b'\x0b' + bytes.fromhex(value)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def stable_id(namespace_id: str, key: str) -> str:
    return digest(ws('roentgen.stable-id') + wi(1) + ws(namespace_id) + ws(key))[:32]


def command_id(issuer: str, tick: int, priority: int, sequence: int, type_id: str, schema: int, payload_sum: str) -> str:
    value = digest(
        ws('roentgen.command')
        + wid(issuer)
        + wu(tick)
        + wi(priority)
        + wu(sequence)
        + ws(type_id)
        + wi(schema)
        + wsum(payload_sum)
    )
    return value[:32]


def header_hash(header: dict[str, object]) -> str:
    return digest(
        ws('roentgen.replay.header')
        + wi(int(header['FormatVersion']))
        + ws(str(header['Product']))
        + ws(str(header['ConstitutionVersion']))
        + wu(int(header['CampaignSeed']))
        + wi(int(header['TicksPerSecond']))
        + ws(str(header['RandomAlgorithmId']))
        + ws(str(header['BuildFingerprint']))
    )


def command_hash(previous: str, command: dict[str, object]) -> str:
    payload = base64.b64decode(str(command['Payload']))
    return digest(
        ws('roentgen.replay.command')
        + wsum(previous)
        + wid(str(command['CommandId']))
        + wid(str(command['IssuerId']))
        + wu(int(command['TargetTick']))
        + wi(int(command['OriginPriority']))
        + wu(int(command['Sequence']))
        + ws(str(command['TypeId']))
        + wi(int(command['SchemaVersion']))
        + wb(payload)
        + wsum(str(command['PayloadChecksum']))
    )


def checkpoint_hash(previous: str, checkpoint: dict[str, object]) -> str:
    return digest(
        ws('roentgen.replay.checkpoint')
        + wsum(previous)
        + wu(int(checkpoint['Tick']))
        + wsum(str(checkpoint['StateChecksum']))
    )


def build_fixture() -> list[dict[str, object]]:
    header: dict[str, object] = {
        'RecordType': 'header',
        'FormatVersion': 1,
        'Product': 'Roentgen',
        'ConstitutionVersion': '0.1.0',
        'CampaignSeed': 7351982471,
        'TicksPerSecond': 60,
        'RandomAlgorithmId': 'pcg32-roentgen-v1',
        'BuildFingerprint': 'phase2-golden',
    }
    chain = header_hash(header)
    header['HeaderChecksum'] = chain

    payload = b'\x02' + struct.pack('>i', 5)
    payload_sum = digest(payload)
    issuer = stable_id('fixture', 'issuer')
    command: dict[str, object] = {
        'RecordType': 'command',
        'CommandId': command_id(issuer, 12, 300, 1, 'test.add', 1, payload_sum),
        'IssuerId': issuer,
        'TargetTick': 12,
        'OriginPriority': 300,
        'Sequence': 1,
        'TypeId': 'test.add',
        'SchemaVersion': 1,
        'Payload': base64.b64encode(payload).decode('ascii'),
        'PayloadChecksum': payload_sum,
        'PreviousChecksum': chain,
    }
    chain = command_hash(chain, command)
    command['RecordChecksum'] = chain

    checkpoint: dict[str, object] = {
        'RecordType': 'checkpoint',
        'Tick': 12,
        'StateChecksum': 'a' * 64,
        'PreviousChecksum': chain,
    }
    chain = checkpoint_hash(chain, checkpoint)
    checkpoint['RecordChecksum'] = chain

    footer: dict[str, object] = {
        'RecordType': 'footer',
        'RecordCount': 2,
        'FinalChecksum': chain,
    }
    return [header, command, checkpoint, footer]


def main() -> int:
    expected = build_fixture()
    actual = [json.loads(line) for line in FIXTURE.read_text(encoding='utf-8').splitlines() if line]
    if actual != expected:
        print('Replay fixture verification FAILED.')
        print('Expected:')
        print(json.dumps(expected, indent=2))
        print('Actual:')
        print(json.dumps(actual, indent=2))
        return 1

    print('Replay fixture verification PASSED.')
    print(f"  Records: {len(actual)}")
    print(f"  Final checksum: {actual[-1]['FinalChecksum']}")
    return 0


if __name__ == '__main__':
    sys.exit(main())
