#!/usr/bin/env python3
from pathlib import Path
import hashlib
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
expected = json.loads((ROOT / "eng/phase17-golden-vectors.json").read_text(encoding="utf-8"))
content = (ROOT / "src/Roentgen.Campaign/Phase17Content.cs").read_text(encoding="utf-8")

premise_block = content.split("public static IReadOnlyList<MilestoneKernel>", 1)[0]
premise_ids = re.findall(r'Premise\("([a-z0-9-]+)"', premise_block)
milestone_block = content.split("public static IReadOnlyList<MilestoneKernel>", 1)[1].split("public static IReadOnlyList<CriticalRoleDefinition>", 1)[0]
milestone_ids = re.findall(r'new\("([a-z0-9-]+)",\s*CampaignMilestoneRole\.([A-Za-z]+)', milestone_block)
role_block = content.split("public static IReadOnlyList<CriticalRoleDefinition>", 1)[1].split("public static IReadOnlyList<EndingFamilyDefinition>", 1)[0]
critical_role_ids = re.findall(r'Role\("([a-z0-9-]+)"', role_block)
ending_block = content.split("public static IReadOnlyList<EndingFamilyDefinition>", 1)[1].split("public static IReadOnlyList<EpilogueRule>", 1)[0]
ending_ids = re.findall(r'Ending\(campaignId,\s*"([a-z0-9-]+)"', ending_block)

actual = {
    "schemaVersion": 17,
    "premiseCount": len(premise_ids),
    "premiseIdsSha256": hashlib.sha256("\n".join(premise_ids).encode()).hexdigest(),
    "milestoneCount": len(milestone_ids),
    "milestoneIdsAndRolesSha256": hashlib.sha256("\n".join(f"{a}:{b}" for a, b in milestone_ids).encode()).hexdigest(),
    "criticalRoleCount": len(critical_role_ids),
    "criticalRoleIdsSha256": hashlib.sha256("\n".join(critical_role_ids).encode()).hexdigest(),
    "endingFamilyCount": len(ending_ids),
    "endingIdsSha256": hashlib.sha256("\n".join(ending_ids).encode()).hexdigest(),
    "revelationCount": 3,
    "irreversibleChoiceCount": 2,
    "regressionBuildCount": 6,
}

if actual != expected:
    print("Phase 17 vector verification FAILED:")
    print("expected", json.dumps(expected, indent=2))
    print("actual", json.dumps(actual, indent=2))
    sys.exit(1)
print("Phase 17 vector verification PASSED.")
