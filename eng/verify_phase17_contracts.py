#!/usr/bin/env python3
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
errors: list[str] = []


def require(path: str, *markers: str) -> None:
    file = ROOT / path
    if not file.is_file():
        errors.append(f"missing {path}")
        return
    text = file.read_text(encoding="utf-8")
    for marker in markers:
        if marker not in text:
            errors.append(f"{path} missing contract marker {marker!r}")


require("src/Roentgen.Campaign/Roentgen.Campaign.csproj", "Roentgen.Generation", "Roentgen.World", "Roentgen.Factions", "Roentgen.Narrative")
require("src/Roentgen.Campaign/CampaignContracts.cs", "RegionalHistoryPlan", "GeneratedCampaignPlan", "GeneratedMilestonePlan", "EndingFamilyDefinition", "CriticalRoleAssignmentPlan")
require("src/Roentgen.Campaign/RegionalHistoryCompiler.cs", "RegionalHistoryCompiler", "RegionalHistoryProjector", "cross-region")
require("src/Roentgen.Campaign/Phase17Content.cs", "CreatePremiseKernels", "CreateMilestoneKernels", "CreateEndingFamilies", "CreateCriticalRoles")
require("src/Roentgen.Campaign/CampaignGenerator.cs", "CurrentGeneratorVersion = 17", "SelectPremise", "BuildMilestones", "BuildConvergence", "ComputePlanFingerprint")
require("src/Roentgen.Campaign/CampaignRuntimeState.cs", "SchemaVersion = 17", "CampaignWorldState", "ToCanonicalPayload", "ComputeChecksum")
require("src/Roentgen.Campaign/CampaignDirector.cs", "CommitChoice", "CollectEvidence", "ResolveConvergence", "RepairCriticalRoles", "SelectEndingAndGenerateEpilogue")
require("src/Roentgen.Campaign/CampaignValidation.cs", "CampaignPlanValidator", "CampaignStateValidator", "CampaignRepairEngine")
require("src/Roentgen.Campaign/CampaignAutoAgent.cs", "CompleteRegressionBuilds", "CampaignAutoCompletionResult", "SelectConvergence")
require("src/Roentgen.Simulation/Phase17CampaignRuntime.cs", "Phase17CampaignRuntime", "Initialize", "CommitChoice", "ResolveConvergence")
require("src/Roentgen.Persistence/CampaignSnapshotV17Serializer.cs", "RGCAMP17", "CampaignSnapshotV17", "RequiredSections", "global checksum", "WriteAtomic")
require("src/Roentgen.App/RoentgenGame.cs", "Phase 17 — Campaign Director", "Phase17CampaignRuntime", "CampaignSnapshotV17", "DebugCommitCampaignChoice", "DebugResolveConvergence")
require("tools/Roentgen.CampaignLab/Program.cs", "inspect-history", "inspect-premise", "solve-all-builds", "kill-critical-role", "analyze-endings")
require("tests/Roentgen.Campaign.Tests/Phase17CampaignTests.cs", "All_six_regression_builds_reach_an_ending", "Missing_critical_actor_uses_deterministic_replacement_ladder", "Phase17_snapshot_round_trip_validates_each_section_and_campaign_identity")

manifest = json.loads((ROOT / "eng/projects.json").read_text(encoding="utf-8"))
if manifest.get("schemaVersion", 0) < 17:
    errors.append("project manifest schema must be at least 17")
names = {item["name"] for group in ("runtimeProjects", "testProjects", "toolProjects") for item in manifest[group]}
for name in ("Roentgen.Campaign", "Roentgen.Campaign.Tests", "Roentgen.CampaignLab"):
    if name not in names:
        errors.append(f"project manifest missing {name}")

for path in (
    "schemas/campaign-plan.schema.json",
    "schemas/campaign-state.schema.json",
    "schemas/campaign-snapshot-v17.schema.json",
    "samples/campaign/phase17-campaign-profile.json",
    "docs/170_PHASE17_CAMPAIGN_DIRECTOR.md",
    "docs/171_REGIONAL_HISTORY_AND_PREMISE.md",
    "docs/172_MILESTONES_REVELATIONS_CHOICES.md",
    "docs/173_ENDINGS_EPILOGUES_AND_REPLACEMENT.md",
    "docs/174_CAMPAIGNLAB.md",
    "eng/phase17-golden-vectors.json",
    ".github/workflows/campaign-director.yml",
):
    if not (ROOT / path).is_file():
        errors.append(f"missing {path}")

if errors:
    print("Phase 17 contract verification FAILED:")
    for error in errors:
        print("  - " + error)
    sys.exit(1)
print("Phase 17 contract verification PASSED.")
