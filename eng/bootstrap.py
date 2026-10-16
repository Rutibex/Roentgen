#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    parser = argparse.ArgumentParser(description='Verify Roentgen integrated source dependencies.')
    parser.add_argument('--force', action='store_true', help='Retained for compatibility; integrated sources are never replaced.')
    parser.parse_args()

    lock = json.loads((ROOT / 'eng/dependencies.lock.json').read_text(encoding='utf-8'))
    if lock.get('dependencies'):
        raise RuntimeError('Phase 19 ships no external source dependencies; dependencies must be empty.')

    for source in lock.get('integratedSources', []):
        missing = [project for project in source.get('projects', []) if not (ROOT / project).is_file()]
        if missing:
            raise RuntimeError(f"{source['id']} is missing integrated projects: {', '.join(missing)}")
        sound_font = source.get('soundFont')
        if sound_font:
            asset = ROOT / sound_font['asset']
            if not asset.is_file():
                raise RuntimeError(f"Missing bundled SoundFont: {sound_font['asset']}")
            actual = hashlib.sha256(asset.read_bytes()).hexdigest()
            if actual != sound_font['sha256']:
                raise RuntimeError(f"Bundled SoundFont hash is {actual}, expected {sound_font['sha256']}.")
        print(f"Verified integrated source: {source['id']}")

    print('No external checkout is required.')
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f'Bootstrap verification failed: {exc}', file=sys.stderr)
        raise SystemExit(1)
