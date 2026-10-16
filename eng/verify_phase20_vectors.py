#!/usr/bin/env python3
from pathlib import Path
import json,re,sys
ROOT=Path(__file__).resolve().parents[1]
v=json.loads((ROOT/'eng/phase20-golden-vectors.json').read_text())
models=(ROOT/'src/Roentgen.UI/Phase20Models.cs').read_text()
layout=(ROOT/'src/Roentgen.UI/Phase20LayoutPlanner.cs').read_text()
save=(ROOT/'src/Roentgen.Persistence/Phase20SaveGame.cs').read_text()
errors=[]
for screen in v['requiredScreens']:
    if re.search(r'\b'+re.escape(screen)+r'\b',models) is None: errors.append('missing screen '+screen)
for profile in v['layoutProfiles']:
    if profile not in layout: errors.append('missing layout profile '+profile)
if v['saveMagic'] not in save: errors.append('save magic differs')
action_body=re.search(r'public enum UiAction\s*\{([^}]*)\}',models,re.S)
actions=[x.strip() for x in action_body.group(1).split(',') if x.strip()] if action_body else []
if len(actions)<v['minimumSemanticActions']: errors.append(f'only {len(actions)} semantic actions')
tutorial_count = models.count('new(\"')
phase21_pack = ROOT / 'content/roentgen/phase21-launch-content.json'
if phase21_pack.is_file():
    tutorial_count = max(tutorial_count, len(json.loads(phase21_pack.read_text(encoding='utf-8')).get('tutorials', [])))
if tutorial_count < v['minimumTutorialCards']: errors.append('tutorial catalog below target')
if v['fixedSoundFontRequired'] and not (ROOT/'content/music/soundfonts/default.sf2').is_file(): errors.append('fixed SoundFont absent')
if errors:
 print('Phase 20 vectors FAILED:'); [print(' -',e) for e in errors]; sys.exit(1)
print(f'Phase 20 vectors PASSED: {len(v["requiredScreens"])} screens, {len(actions)} semantic actions, {len(v["layoutProfiles"])} layout profiles.')
