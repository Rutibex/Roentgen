#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / 'tests/fixtures/phase3-empty.rgsnap'
EXPECTED_SHA256 = '41e9e1879e5b43209d33b0ea7d438d81589a1934999fce6cedd34401a69bb286'

actual = hashlib.sha256(FIXTURE.read_bytes()).hexdigest()
if actual != EXPECTED_SHA256:
    print(f'Fixture SHA mismatch: expected {EXPECTED_SHA256}, actual {actual}')
    sys.exit(1)
print(f'Phase 3 snapshot fixture verified: {actual}')
