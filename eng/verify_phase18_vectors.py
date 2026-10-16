#!/usr/bin/env python3
from pathlib import Path
import json,re,sys
R=Path(__file__).resolve().parents[1]; v=json.loads((R/'eng/phase18-golden-vectors.json').read_text()); errors=[]
if v['integratedProjectCount']!=len(list((R/'src').glob('Playroma.Sound.*/*.csproj'))): errors.append('integrated project count')
settings=json.loads((R/'config/roentgen.settings.json').read_text())['audio']
for key in ['renderBlockFrames','submittedBufferFrames']:
 if settings[key]!=v[key]: errors.append(key)
cat=(R/'src/Roentgen.Audio.Playroma/IntegratedSoundCatalog.cs').read_text()
for asset in v['requiredAssets']:
 parts=asset.lower().split('.')
 lower=cat.lower()
 if asset not in cat and not all(part in lower for part in parts): errors.append('missing asset '+asset)
regions=(R/'src/Roentgen.Audio/AudioEnums.cs').read_text(); body=re.search(r'enum AudioRegion \{([^}]+)\}',regions).group(1)
if len([x for x in body.split(',') if x.strip()])!=v['initialRegions']: errors.append('region count')
if errors: print('\n'.join('ERROR '+x for x in errors));sys.exit(1)
print('Phase 18 golden vectors verified.')
