#!/usr/bin/env python3
from pathlib import Path
import json, sys
ROOT=Path(__file__).resolve().parents[1]
errors=[]
def require(path,*markers):
 p=ROOT/path
 if not p.is_file(): errors.append(f'missing {path}'); return
 text=p.read_text(encoding='utf-8')
 for marker in markers:
  if marker not in text: errors.append(f"{path} missing contract marker {marker!r}")
require('src/Roentgen.Navigation/NavigationContracts.cs','NavigationAgentProfile','NavigationRequest','DynamicNavigationObstacleRegistry','DynamicNavigationDoorState','BreacherHumanoid','RoboticChassis')
require('src/Roentgen.Navigation/OutdoorNavigation.cs','OutdoorNavigationGraphBuilder','OutdoorRouteFinder','MaximumExpandedNodes')
require('src/Roentgen.Navigation/InteriorNavigationRouting.cs','HierarchicalNavigationService','EnterStructure','ExitStructure','TryResolveDoorTraversal','FindNearestCover')
require('src/Roentgen.AI/AgentMemory.cs','AgentAwarenessMemory','AgentBrainRegistry')
require('src/Roentgen.AI/Perception.cs','AiStimulusRegistry','HasLineOfSight')
require('src/Roentgen.AI/UtilityAi.cs','UtilityAiEvaluator','SwitchingPenalty','Surrender')
require('src/Roentgen.AI/NavigationMotor.cs','CollisionNavigationMovementQuery','INavigationInteractionHandler','stuckTicks','route replanned')
require('src/Roentgen.AI/LocalAvoidance.cs','NavigationAgentSpatialRegistry','AvoidanceNavigationMovementQuery','Stable IDs choose opposite deterministic separation directions')
require('src/Roentgen.AI/SchedulesAndSimulation.cs','AgentSimulationTier','ReducedAgentSimulation','StrategicAgentSimulation','TravelArrivalTick','LastStrategicTick')
require('src/Roentgen.AI/CompanionCommands.cs','CompanionCommandService','AttackTarget','WaitUntil')
require('src/Roentgen.AI/FullAiController.cs','FullAiController','ShareAlerts','Surrender')
require('src/Roentgen.App/Phase13ItemRuntime.cs','ActivateInventoryItem','ItemCampaignSnapshotSerializer','SynchronizeCombatWeapon')
require('src/Roentgen.App/Phase14WorldAgentRuntime.cs','HierarchicalNavigationService','AgentSimulationTierService','ReducedAgentSimulation','StrategicAgentSimulation','PlayerNavigationInteractionHandler')
require('src/Roentgen.App/RoentgenGame.cs','Phase 14 Navigation and Full AI','BuildItemOverlay','HandleItemInterface')
require('src/Roentgen.Persistence/ItemWorldSnapshotSerializer.cs','RGITEM01','SHA256','FormatVersion')
require('src/Roentgen.Persistence/ItemCampaignSnapshotSerializer.cs','RGITCM01','RecipeKnowledgeState','ActiveWeaponId')
manifest=json.loads((ROOT/'eng/projects.json').read_text(encoding='utf-8'))
names={p['name'] for group in ('runtimeProjects','testProjects','toolProjects') for p in manifest[group]}
for name in ('Roentgen.AI','Roentgen.Navigation','Roentgen.Persistence','Roentgen.Phase14.Tests','Roentgen.AgentLab'):
 if name not in names: errors.append(f'project manifest missing {name}')
if errors:
 print('Phase 14 contract verification FAILED:')
 for e in errors: print(f'  - {e}')
 sys.exit(1)
print('Phase 14 contract verification PASSED.')
