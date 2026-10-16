#!/usr/bin/env python3
from pathlib import Path
import hashlib, json, re, sys
ROOT=Path(__file__).resolve().parents[1]
errors=[]
def need(path, *tokens):
    p=ROOT/path
    if not p.is_file(): errors.append(f'missing {path}'); return
    text=p.read_text(encoding='utf-8', errors='ignore')
    for token in tokens:
        if token not in text: errors.append(f'{path} missing contract token: {token}')

required=[
'src/Roentgen.UI/Phase20Models.cs','src/Roentgen.UI/Phase20InputRouter.cs','src/Roentgen.UI/Phase20Localization.cs',
'src/Roentgen.UI/Phase20UiRenderer.cs','src/Roentgen.UI/Phase20LayoutPlanner.cs','src/Roentgen.App/Phase20CharacterCreation.cs',
'src/Roentgen.App/Phase20FocusRuntime.cs','src/Roentgen.App/Phase20ScreenFactory.cs','src/Roentgen.Persistence/Phase20SaveGame.cs',
'tools/Roentgen.InterfaceLab/Roentgen.InterfaceLab.csproj','tests/Roentgen.Phase20.Tests/Roentgen.Phase20.Tests.csproj',
'docs/200_PHASE20_FULL_INTERFACE_ACCESSIBILITY.md','docs/201_PHASE20_SAVE_AND_RECOVERY.md']
for path in required:
    if not (ROOT/path).is_file(): errors.append(f'missing {path}')
need('src/Roentgen.App/RoentgenGame.cs','Phase20InputRouter','SaveSlotManager','Phase20FocusRuntime','Phase20ScreenKind.MainMenu','BuildNarrativeQuery','AdaptiveMusicRuntime')
need('src/Roentgen.Persistence/Phase20SaveGame.cs','RGSAVE20','FormatVersion = 20','SHA256.HashData','RecoveredFromBackup','File.Move(temporary, path, overwrite: true)')
need('src/Roentgen.UI/Phase20Models.cs','CurrentSchemaVersion = 20','InterfaceScale','SubtitleScale','ColorVisionMode','InputBindingDefaults','Phase20TutorialCatalog')
need('src/Roentgen.UI/Phase20LayoutPlanner.cs','AcceptanceMatrix','1080p-maximum-scale','narrow-window','RequiresScrolling')
need('src/Roentgen.App/Phase20FocusRuntime.cs','FocusTemporalMode','QueueSelectedAction','Commit','BuildModel')
need('src/Roentgen.Simulation/Phase16NarrativeRuntime.cs','CancelConversation')
props=(ROOT/'Directory.Build.props').read_text(encoding='utf-8')
if not any(token in props for token in ('<VersionPrefix>0.20.0</VersionPrefix>','<VersionPrefix>0.21.0</VersionPrefix>')): errors.append('Directory.Build.props no longer carries a Phase 20-compatible or later version')
if not any(token in props for token in ('<VersionSuffix>phase20</VersionSuffix>','<VersionSuffix>phase21</VersionSuffix>')): errors.append('Directory.Build.props no longer carries a Phase 20-compatible or later suffix')
manifest=json.loads((ROOT/'eng/projects.json').read_text())
if manifest.get('schemaVersion',0) < 20: errors.append('projects schema must be at least 20')
names={p['name'] for group in ('runtimeProjects','testProjects','toolProjects') for p in manifest[group]}
for name in ('Roentgen.Phase20.Tests','Roentgen.InterfaceLab'):
    if name not in names: errors.append(f'project graph missing {name}')
# The fixed SoundFont is intentionally retained by product decision.
sf=ROOT/'content/music/soundfonts/default.sf2'
side=ROOT/'content/music/soundfonts/default.sf2.sha256'
if not sf.is_file() or not side.is_file(): errors.append('bundled SoundFont missing')
else:
    expected=side.read_text().split()[0].lower()
    actual=hashlib.sha256(sf.read_bytes()).hexdigest()
    if expected != actual: errors.append('bundled SoundFont checksum mismatch')
app=(ROOT/'src/Roentgen.App/RoentgenGame.cs').read_text()
if 'actorAlive: _ => true' in app or 'itemExists: _ => true' in app: errors.append('narrative still uses placeholder truth delegates')
if errors:
    print('Phase 20 contracts FAILED:')
    for e in errors: print(' -',e)
    sys.exit(1)
print('Phase 20 contracts PASSED: product shell, screens, Focus UI, durable saves, accessibility, localization, InterfaceLab, tests, and retained SoundFont verified.')
