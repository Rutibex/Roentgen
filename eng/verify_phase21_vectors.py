#!/usr/bin/env python3
from pathlib import Path
import hashlib,json,re,sys
ROOT=Path(__file__).resolve().parents[1]
v=json.loads((ROOT/'eng/phase21-golden-vectors.json').read_text())
p=ROOT/'content/roentgen/phase21-launch-content.json'
d=json.loads(p.read_text())
errors=[]
if hashlib.sha256(p.read_bytes()).hexdigest()!=v['sourceSha256']: errors.append('source content SHA differs')
if d.get('schemaVersion')!=v['schemaVersion'] or d.get('packageId')!=v['packageId']: errors.append('package identity differs')
for key,count in v['counts'].items():
    if len(d.get(key,[]))!=count: errors.append(f'{key} expected {count}, found {len(d.get(key,[]))}')
behavior=sum(1 for x in d['perks'] if x.get('behavioral'))*100//len(d['perks'])
if behavior<v['minimumBehavioralPerkPercent']: errors.append('behavioral perk vector failed')
if min(len(x.get('solutionRoutes',[])) for x in d['questKernels'])<v['minimumQuestRoutes']: errors.append('quest route vector failed')
roles={x['role'] for x in d['campaignKernels']}
for role in v['requiredCampaignRoles']:
    if role not in roles: errors.append('missing campaign role '+role)
profile=(ROOT/'src/Roentgen.World/WorldGenerationProfile.cs').read_text()
for meters in v['launchWorldMeters']:
    if f'= {meters}' not in profile: errors.append(f'launch world size {meters} not found')
if v['fixedSoundFontRequired'] and not (ROOT/'content/music/soundfonts/default.sf2').is_file(): errors.append('fixed SoundFont absent')
if errors:
    print('Phase 21 vectors FAILED:'); [print(' -',e) for e in errors]; sys.exit(1)
print(f'Phase 21 vectors PASSED: {sum(v["counts"].values())} budgeted records, {behavior}% behavioral perks, content SHA {v["sourceSha256"][:16]}.')
