#!/usr/bin/env python3
from pathlib import Path
import json,sys
ROOT=Path(__file__).resolve().parents[1]
errors=[]
def require(path,*markers):
 p=ROOT/path
 if not p.is_file(): errors.append(f'missing {path}'); return
 text=p.read_text(encoding='utf-8')
 for marker in markers:
  if marker not in text: errors.append(f"{path} missing contract marker {marker!r}")
require('src/Roentgen.Factions/SocietyContracts.cs','FactionIds','FactionKernelDefinition','SettlementGenerationProfile','IdeologyAxis','CrimeKind','DisguiseEvaluation')
require('src/Roentgen.Factions/SocietyWorldState.cs','SchemaVersion = 15','SocietyWorldState','RestoreFrom','ComputeChecksum','Touch')
require('src/Roentgen.Factions/FactionGenerator.cs','{place} Clinic Compact','{place} Recovery Authority','{place} Cooperative','CreateRegressionWorld')
require('src/Roentgen.Factions/SocietySimulation.cs','ResolveShipments','DispatchShipments','ResolveLeadership','ProcessCrimes','ApplyReputationEvidence')
require('src/Roentgen.Factions/SocietyCommands.cs','SocietyCommandProcessor','TransferSettlementControlCommand','RecordCrimeCommand','StartConflictCommand')
require('src/Roentgen.Factions/SocietyValidation.cs','SocietyValidator','SocietyRepairEngine','shipment.nonconservative','crime.omniscient-report')
require('src/Roentgen.Items/Phase15SocietyEconomyBridge.cs','Phase15SocietyEconomyBridge','SocietyMarketProjection')
require('src/Roentgen.Simulation/Phase15SocietyRuntime.cs','Phase15SocietyRuntime','UpdateFromSimulationTick','AdvanceDebugDay')
require('src/Roentgen.Persistence/SocietyWorldSnapshotSerializer.cs','RGSOC15','SHA256','WriteAtomic')
require('src/Roentgen.UI/Phase15SocietyScreens.cs','Phase15SocietyScreenBuilder','BuildFactionDetail','BuildSettlement','BuildReputation','BuildTerritory')
require('src/Roentgen.App/RoentgenGame.cs','Phase15SocietyRuntime','Phase15Content.CreateRegressionWorld')
require('tools/Roentgen.FactionLab/Program.cs','simulate-days','kill-leader','record-crime','apply-disguise','replay')
require('tests/Roentgen.Factions.Tests/Phase15SystemTests.cs','Trade_is_bilateral_and_conservative','Crime_requires_witness_or_evidence_before_report','Long_simulation_is_deterministic')
manifest=json.loads((ROOT/'eng/projects.json').read_text(encoding='utf-8'))
if manifest.get('schemaVersion', 0)<15: errors.append('project manifest schema must be at least 15')
names={item['name'] for group in ('runtimeProjects','testProjects','toolProjects') for item in manifest[group]}
for name in ('Roentgen.Factions','Roentgen.Factions.Tests','Roentgen.FactionLab'):
 if name not in names: errors.append(f'project manifest missing {name}')
for path in ('schemas/faction-kernel.schema.json','schemas/society-world-state.schema.json','schemas/trade-shipment.schema.json','schemas/crime-record.schema.json','schemas/disguise-observation.schema.json'):
 if not (ROOT/path).is_file(): errors.append(f'missing {path}')
if errors:
 print('Phase 15 contract verification FAILED:')
 for error in errors: print('  - '+error)
 sys.exit(1)
print('Phase 15 contract verification PASSED.')
