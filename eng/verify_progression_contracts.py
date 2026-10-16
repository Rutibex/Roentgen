#!/usr/bin/env python3
from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
required=[
 'src/Roentgen.Rpg/ProgressionContracts.cs','src/Roentgen.Rpg/AttributeSkillSets.cs','src/Roentgen.Rpg/DerivedStatistics.cs',
 'src/Roentgen.Rpg/ProgressionState.cs','src/Roentgen.Rpg/CharacterCreation.cs','src/Roentgen.Rpg/Advancement.cs',
 'src/Roentgen.Rpg/SkillChecks.cs','src/Roentgen.Rpg/Perks.cs','src/Roentgen.Rpg/Phase12Content.cs',
 'src/Roentgen.Rpg/CapabilityService.cs','src/Roentgen.Rpg/BuildViability.cs','src/Roentgen.UI/CharacterScreen.cs',
 'tests/Roentgen.Rpg.Tests/Roentgen.Rpg.Tests.csproj','tools/Roentgen.BalanceLab/Roentgen.BalanceLab.csproj']
missing=[x for x in required if not (root/x).is_file()]
if missing: raise SystemExit('Missing Phase 12 files: '+', '.join(missing))
text='\n'.join((root/x).read_text(errors='ignore') for x in required if x.endswith('.cs'))
for token in ['CharacterProgressionState','DerivedStatCalculator','CharacterCreationDraft','ExperienceSystem','SkillCheckResolver','PerkCatalog','ProgressionCapabilityService','BuildViabilitySimulator']:
 if token not in text: raise SystemExit('Missing progression contract: '+token)
content=(root/'src/Roentgen.Rpg/Phase12Content.cs').read_text()
block=content[content.index('public static IReadOnlyList<PerkDefinition> Perks'):content.index('private static BackgroundDefinition')]
count=len(re.findall(r'^\s*(?:P|Focus)\(',block,re.M))
if count != 40: raise SystemExit(f'Expected 40 initial perks, found {count}.')
if content.count('Background("') != 8: raise SystemExit('Expected 8 backgrounds.')
if content.count('Trait("') != 16: raise SystemExit('Expected 16 traits.')
print('Phase 12 character progression contracts verified.')
