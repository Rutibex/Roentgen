#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
data=json.loads((root/'eng/item-golden-vectors.json').read_text())
for v in data['vectors']:
 name=v['name']
 if 'baseDamage' in v:
  actual=max(0,(v['baseDamage']+v['damageAdditive'])*v['damageMultiplierPermille']//1000)
  if actual != v['expectedDamage']: raise SystemExit(f'{name}: damage {actual} != {v["expectedDamage"]}')
  cap=max(0,v['baseCapacity']+v['capacityAdditive'])
  if cap != v['expectedCapacity']: raise SystemExit(f'{name}: capacity {cap} != {v["expectedCapacity"]}')
 elif 'baseRange' in v:
  actual=v['baseRange']
  for factor in v['rangeMultipliersPermille']: actual=actual*factor//1000
  if actual != v['expectedRange']: raise SystemExit(f'{name}: range {actual} != {v["expectedRange"]}')
 elif 'baseReliability' in v:
  actual=(v['baseReliability']+v['componentReliabilityAdditive'])*v['conditionPermille']//1000
  if actual != v['expectedReliability']: raise SystemExit(f'{name}: reliability {actual} != {v["expectedReliability"]}')
 else:
  actual=max(500,min(2500,v['target']*1000//max(1,v['quantity'])))
  if actual != v['expectedScarcityPermille']: raise SystemExit(f'{name}: scarcity {actual} != {v["expectedScarcityPermille"]}')
print('Phase 13 item golden vectors verified.')
