#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def main() -> int:
    if len(sys.argv) != 4:
        print('Usage: write_build_manifest.py <publish-directory> <runtime-id> <version>', file=sys.stderr)
        return 2

    root = Path(sys.argv[1]).resolve()
    if not root.is_dir():
        print(f'Publish directory does not exist: {root}', file=sys.stderr)
        return 2

    manifest = {
        'schemaVersion': 1,
        'product': 'Roentgen',
        'version': sys.argv[3],
        'runtimeIdentifier': sys.argv[2],
        'createdUtc': datetime.now(timezone.utc).isoformat(),
        'files': [
            {
                'path': path.relative_to(root).as_posix(),
                'size': path.stat().st_size,
                'sha256': sha256(path)
            }
            for path in sorted(root.rglob('*'))
            if path.is_file() and path.name != 'build-manifest.json'
        ]
    }
    (root / 'build-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
