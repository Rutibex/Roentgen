#!/usr/bin/env python3
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
data=json.loads((root/'eng/progression-golden-vectors.json').read_text())
for vector in data['vectors']:
 a=vector['attributes']; level=vector['level']; e=vector['expected']
 actual={
  'MaximumHealth':60+a['Vitality']*10+(level-1)*(3+a['Vitality']//3),
  'MaximumStamina':70+a['Vitality']*8+a['Reflex']*4,
  'CarryCapacityGrams':(20+a['Physique']*7)*1000,
  'MaximumFocusMilliunits':(45+a['Awareness']*4+a['Reflex']*5)*1000,
  'FocusRecoveryPerSecondMilliunits':3000+a['Reflex']*250,
  'Initiative':a['Awareness']*4+a['Reflex']*3+a['Fortune'],
  'CriticalReserveMilliunits':2000+a['Fortune']*750,
  'SkillPointsPerLevel':8+a['Intellect']//2,
 }
 if actual != e: raise SystemExit(f"Vector {vector['name']} mismatch: {actual} != {e}")
print('Phase 12 progression golden vectors verified.')
