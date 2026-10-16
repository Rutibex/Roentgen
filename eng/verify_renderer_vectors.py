#!/usr/bin/env python3
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
data = json.loads((ROOT / 'eng/renderer-golden-vectors.json').read_text(encoding='utf-8'))
errors=[]
box=data['box']
if (box['vertices'], box['indices'], box['triangles']) != (24,36,12): errors.append('box topology')
t=data['terrain']; cx=t['cellsX']; cz=t['cellsZ']
if t['vertices'] != (cx+1)*(cz+1): errors.append('terrain vertices')
if t['indices'] != cx*cz*6 or t['triangles'] != cx*cz*2: errors.append('terrain topology')
c=data['cylinder']; sides=c['sides']
if c['vertices'] != sides*4 + 2*(sides+1): errors.append('cylinder vertices')
if c['indices'] != sides*12 or c['triangles'] != sides*4: errors.append('cylinder topology')
if errors:
 print('Renderer vectors FAILED:', ', '.join(errors)); sys.exit(1)
print('Renderer golden vectors verified.')
