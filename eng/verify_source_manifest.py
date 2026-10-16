#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / 'eng/source-manifest.sha256'
errors: list[str] = []
entries: set[str] = set()
for number, line in enumerate(MANIFEST.read_text(encoding='utf-8').splitlines(), start=1):
    if not line.strip():
        continue
    try:
        expected, relative = line.split('  ', 1)
    except ValueError:
        errors.append(f'line {number}: invalid format')
        continue
    if relative in entries:
        errors.append(f'line {number}: duplicate path {relative}')
        continue
    entries.add(relative)
    path = ROOT / relative
    if not path.is_file():
        errors.append(f'missing file: {relative}')
        continue
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != expected:
        errors.append(f'checksum mismatch: {relative}')

if errors:
    print('Source manifest verification FAILED:')
    for error in errors:
        print(f'  - {error}')
    sys.exit(1)
print(f'Source manifest verification PASSED ({len(entries)} files).')
