#!/usr/bin/env python3
from pathlib import Path
import json, sys
ROOT = Path(__file__).resolve().parents[1]
errors=[]
def require(path,*markers):
    p=ROOT/path
    if not p.is_file(): errors.append(f'missing {path}'); return
    text=p.read_text(encoding='utf-8')
    for marker in markers:
        if marker not in text: errors.append(f"{path} missing contract marker {marker!r}")
require('src/Roentgen.Narrative/NarrativeContracts.cs','NarrativeValue','FactRecord','KnowledgeClaim','SemanticEventRecord','SkillCheckAttemptRecord')
require('src/Roentgen.Narrative/NarrativeWorldState.cs','SchemaVersion = 16','NarrativeWorldState','ConsumedEventIds','RestoreFrom','ComputeChecksum')
require('src/Roentgen.Narrative/QuestContracts.cs','QuestKernelDefinition','GeneratedQuestPlan','QuestRouteDefinition','DialogueNodeDefinition','QuestRecoveryDefinition')
require('src/Roentgen.Narrative/QuestBinding.cs','QuestBinder','deterministic-index','NarrativeBindingCandidate')
require('src/Roentgen.Narrative/NarrativeConditions.cs','INarrativeQueryContext','NarrativeConditionEvaluator','FactExists','ActorKnowsFact')
require('src/Roentgen.Narrative/NarrativeEffects.cs','INarrativeExternalEffectRouter','PrepareAndCommit','NarrativeEffectExecutor','MarkConsequenceApplied')
require('src/Roentgen.Narrative/QuestRuntime.cs','IngestEvent','ResolveSkillCheck','ConsumeEvent','Reconcile')
require('src/Roentgen.Narrative/DialogueEngine.cs','DialogueIntent','DialogueTextRealizer','StartConversation','Choose')
require('src/Roentgen.Narrative/KnowledgeAndRumors.cs','KnowledgeSystem','RumorSystem','TransmissionChain')
require('src/Roentgen.Narrative/NarrativeValidation.cs','QuestKernelValidator','GeneratedQuestValidator','NarrativeRepairEngine','AbstractQuestSolver')
require('src/Roentgen.Narrative/Phase16Content.cs','The Pale Manifest','CreateInitialKernels','KernelCatalog','missing-shipment','evacuation-relocation')
require('src/Roentgen.Simulation/Phase16NarrativeRuntime.cs','Phase16NarrativeRuntime','DebugDiscoverEvidence','DebugResolveProblem','StartConversation')
require('src/Roentgen.Simulation/Phase16DomainEffectRouter.cs','Phase16DomainEffectRouter','ChangeReputation','ChangeRelationship','RecordCrime','TransferItem','ConsumeItem')
require('src/Roentgen.Persistence/NarrativeWorldSnapshotSerializer.cs','RGNAR16','SHA256','WriteAtomic')
require('src/Roentgen.Persistence/CampaignSnapshotV16Serializer.cs','RGCAMP16','CampaignSnapshotV16','NarrativeSnapshot','FormatVersion = 16')
require('src/Roentgen.UI/Phase16NarrativeScreens.cs','Phase16NarrativeScreenBuilder','BuildJournal','BuildKnowledge','BuildRumors','BuildDebug')
require('src/Roentgen.App/RoentgenGame.cs','Phase 16 — Quest and Dialogue Engine','Phase16NarrativeRuntime','Phase13OverlayKind.Dialogue','Phase13OverlayKind.Journal')
app_text=(ROOT/'src/Roentgen.App/RoentgenGame.cs').read_text(encoding='utf-8')
if 'CampaignSnapshotV16' not in app_text and 'CampaignSnapshotV17' not in app_text: errors.append("src/Roentgen.App/RoentgenGame.cs missing campaign snapshot integration")
require('tools/Roentgen.QuestLab/Program.cs','inspect-slots','inspect-facts','preview-dialogue','simulate-all-routes','analyze-repetition')
require('tests/Roentgen.Narrative.Tests/Phase16SystemTests.cs','Initial_library_contains_thirty_valid_kernels','Truth_belief_and_disclosure_are_distinct','Semantic_events_are_idempotent','All_six_regression_builds_can_solve_the_pale_manifest','Narrative_snapshot_round_trip_preserves_checksum','Quest_consequence_updates_authoritative_faction_reputation')
manifest=json.loads((ROOT/'eng/projects.json').read_text(encoding='utf-8'))
if manifest.get('schemaVersion', 0)<16: errors.append('project manifest schema must be at least 16')
names={item['name'] for group in ('runtimeProjects','testProjects','toolProjects') for item in manifest[group]}
for name in ('Roentgen.Narrative','Roentgen.Narrative.Tests','Roentgen.QuestLab'):
    if name not in names: errors.append(f'project manifest missing {name}')
for path in ('schemas/fact-record.schema.json','schemas/quest-kernel.schema.json','schemas/generated-quest-plan.schema.json','schemas/dialogue-graph.schema.json','schemas/narrative-world-state.schema.json','schemas/campaign-snapshot-v16.schema.json','samples/quests/pale-manifest.kernel.json'):
    if not (ROOT/path).is_file(): errors.append(f'missing {path}')
if errors:
    print('Phase 16 contract verification FAILED:')
    for error in errors: print('  - '+error)
    sys.exit(1)
print('Phase 16 contract verification PASSED.')
