#!/usr/bin/env python3
from __future__ import annotations

import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'eng/source-manifest.sha256'
EXCLUDED_PARTS = {'.git', 'artifacts', '.packages', 'bin', 'obj', '__pycache__'}


def included(path: Path) -> bool:
    if path == OUTPUT:
        return False
    return not any(part in EXCLUDED_PARTS for part in path.relative_to(ROOT).parts)


lines: list[str] = []
for path in sorted((item for item in ROOT.rglob('*') if item.is_file() and included(item)), key=lambda item: item.relative_to(ROOT).as_posix()):
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    lines.append(f'{digest}  {path.relative_to(ROOT).as_posix()}')
OUTPUT.write_text('\n'.join(lines) + '\n', encoding='utf-8')
print(f'Wrote {len(lines)} entries to {OUTPUT.relative_to(ROOT)}.')
