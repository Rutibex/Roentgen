#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VECTORS = ROOT / "eng/phase19-golden-vectors.json"


def compute() -> dict[str, object]:
    catalog = json.loads((ROOT / "content/music/catalogs/generaluser-gs-roentgen.catalog.json").read_text(encoding="utf-8"))
    styles: list[dict[str, object]] = []
    for path in sorted((ROOT / "content/music/styles").glob("*-roentgen-*.style.json")):
        item = json.loads(path.read_text(encoding="utf-8"))
        styles.append({"id": item.get("id"), "inherits": item.get("inherits", item.get("basePackId", ""))})
    preset_projection = [
        [p.get("bank"), p.get("program"), p.get("name"), p.get("family"), bool(p.get("isDrumKit")), bool(p.get("excludedByDefault"))]
        for p in catalog["presets"]
    ]
    def digest(value: object) -> str:
        raw = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
        return hashlib.sha256(raw).hexdigest()
    return {
        "schemaVersion": 19,
        "soundFontSha256": catalog["sha256"],
        "presetCount": len(catalog["presets"]),
        "enabledPresetCount": sum(not bool(p.get("excludedByDefault")) for p in catalog["presets"]),
        "roentgenStyleCount": len(styles),
        "presetProjectionSha256": digest(preset_projection),
        "styleProjectionSha256": digest(styles),
        "settingsSha256": hashlib.sha256((ROOT / "config/roentgen.settings.json").read_bytes()).hexdigest(),
    }


def main() -> int:
    actual = compute()
    if not VECTORS.is_file():
        VECTORS.write_text(json.dumps(actual, indent=2) + "\n", encoding="utf-8")
        print("Wrote Phase 19 golden vectors.")
        return 0
    expected = json.loads(VECTORS.read_text(encoding="utf-8"))
    if actual != expected:
        print("Phase 19 vector verification FAILED:")
        print("expected:", json.dumps(expected, indent=2))
        print("actual:", json.dumps(actual, indent=2))
        return 1
    print("Phase 19 vector verification PASSED.")
    print(json.dumps(actual, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
