#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    vectors = json.loads((ROOT / "eng/structure-golden-vectors.json").read_text(encoding="utf-8"))
    expected = vectors.pop("contractSha256")
    actual = hashlib.sha256(json.dumps(vectors, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    errors: list[str] = []
    if actual != expected:
        errors.append(f"contract vector hash mismatch: expected {expected}, got {actual}")
    catalog = (ROOT / "src/Roentgen.World/Structures/StructureGrammarCatalog.cs").read_text(encoding="utf-8")
    declared = sorted(set(re.findall(r'G\("([a-z0-9-]+)"', catalog)))
    if declared != vectors["grammarIds"]:
        errors.append(f"grammar catalog mismatch: expected {vectors['grammarIds']}, got {declared}")
    profile = json.loads((ROOT / "samples/structures/phase7-structure-profile.json").read_text(encoding="utf-8"))
    if profile["campaignSeed"] != vectors["defaultCampaignSeed"]:
        errors.append("default campaign seed does not match the Phase 7 vector")
    if profile["validationStructureTarget"] != vectors["structureExitTarget"]:
        errors.append("structure exit target does not match the Phase 7 vector")
    if profile["gridCentimeters"] != vectors["gridCentimeters"]:
        errors.append("structure grid does not match the Phase 7 vector")
    if errors:
        print("Phase 7 structure vector verification FAILED:")
        for item in errors:
            print(f"  - {item}")
        return 1
    print(f"Phase 7 structure contract vectors verified: {actual}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
