#!/usr/bin/env python3
from __future__ import annotations
from pathlib import Path
import hashlib, json, re, sys, xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
errors=[]
def need(path,*tokens):
    p=ROOT/path
    if not p.is_file(): errors.append(f'missing {path}'); return ''
    text=p.read_text(encoding='utf-8',errors='ignore')
    for token in tokens:
        if token not in text: errors.append(f'{path} missing contract token: {token}')
    return text

def require_count(data,key,count):
    actual=len(data.get(key,[]))
    if actual != count: errors.append(f'{key}: expected {count}, found {actual}')

required={
 'regions':6,'settlementGrammars':12,'structureGrammars':50,'roomMotifs':80,'damageProfiles':20,
 'factions':8,'organizations':20,'humanoidArchetypes':20,'creatureFamilies':30,'robotChassis':12,
 'companionArchetypes':12,'weaponFamilies':14,'weaponComponents':80,'armorFamilies':12,'armorComponents':60,
 'consumables':50,'craftingProcesses':40,'perks':110,'backgrounds':18,'traits':30,'questKernels':120,
 'campaignKernels':30,'dialogueActs':250,'rumorTemplates':100,'tutorials':18,'epilogueRules':96,
 'audioProfiles':157,'musicBindings':44
}
pack_path=ROOT/'content/roentgen/phase21-launch-content.json'
sidecar=ROOT/'content/roentgen/phase21-launch-content.sha256'
if not pack_path.is_file(): errors.append('canonical Phase 21 content pack missing'); data={}
else:
    try: data=json.loads(pack_path.read_text(encoding='utf-8'))
    except Exception as exc: errors.append(f'content JSON invalid: {exc}'); data={}
if data:
    if data.get('schemaVersion') != 21: errors.append('content schema must be 21')
    if data.get('packageId') != 'roentgen.launch-content': errors.append('unexpected packageId')
    for key,count in required.items(): require_count(data,key,count)
    all_ids=[]
    for key,value in data.items():
        if isinstance(value,list):
            ids=[entry.get('id') for entry in value if isinstance(entry,dict)]
            if len(ids) != len(set(ids)): errors.append(f'duplicate IDs in {key}')
            all_ids.extend(ids)
    if len(all_ids) != len(set(all_ids)): errors.append('content IDs are not globally unique')
    behavior=sum(1 for perk in data['perks'] if perk.get('behavioral'))
    if behavior*100 < len(data['perks'])*70: errors.append('behavioral perks below 70 percent')
    for quest in data['questKernels']:
        routes=quest.get('solutionRoutes',[])
        if len(routes)<3 or quest.get('nonCombatRouteCount',0)<1: errors.append(f'invalid route coverage {quest.get("id")}')
        if not quest.get('hiddenFact') or not quest.get('recoveryKinds') or not quest.get('consequenceTags') or not quest.get('followUpIds'): errors.append(f'incomplete quest consequences {quest.get("id")}')
    roles={entry.get('role') for entry in data['campaignKernels']}
    for role in ('opening-crisis','regional-crisis','faction-introduction','revelation','regional-expansion','irreversible-choice','counteraction','final-convergence'):
        if role not in roles: errors.append(f'missing campaign role {role}')
    if len(data.get('epilogueRules',[]))<50: errors.append('epilogue coverage below floor')
    audio={x['id'] for x in data.get('audioProfiles',[])}
    music={x['id'] for x in data.get('musicBindings',[])}
    regions={x['id'] for x in data.get('regions',[])}
    factions={x['id'] for x in data.get('factions',[])}
    quests={x['id'] for x in data.get('questKernels',[])}
    epilogues={x['id'] for x in data.get('epilogueRules',[])}
    structures={x['id'] for x in data.get('structureGrammars',[])}
    motifs={x['id'] for x in data.get('roomMotifs',[])}
    damage={x['id'] for x in data.get('damageProfiles',[])}
    weapons={x['id'] for x in data.get('weaponFamilies',[])}
    armors={x['id'] for x in data.get('armorFamilies',[])}
    def refs(entries, field, available, category):
        for entry in entries:
            values=entry.get(field,[])
            if isinstance(values,str): values=[values]
            for value in values:
                if value not in available: errors.append(f'{category} {entry.get("id")} missing reference {value}')
    refs(data['regions'],'audioProfileId',audio,'region audio'); refs(data['regions'],'musicProfileId',music,'region music')
    refs(data['settlementGrammars'],'compatibleRegions',regions,'settlement region'); refs(data['settlementGrammars'],'structureGrammarIds',structures,'settlement structure')
    refs(data['structureGrammars'],'compatibleRegions',regions,'structure region'); refs(data['structureGrammars'],'roomMotifIds',motifs,'structure motif'); refs(data['structureGrammars'],'damageProfileIds',damage,'structure damage'); refs(data['structureGrammars'],'audioProfileId',audio,'structure audio')
    refs(data['weaponComponents'],'compatibleWeaponFamilies',weapons,'weapon component'); refs(data['armorComponents'],'compatibleArmorFamilies',armors,'armor component')
    refs(data['companionArchetypes'],'personalQuestKernelId',quests,'companion quest'); refs(data['companionArchetypes'],'endingRuleId',epilogues,'companion epilogue'); refs(data['companionArchetypes'],'audioProfileId',audio,'companion audio'); refs(data['companionArchetypes'],'musicProfileId',music,'companion music')
    for creature in data['creatureFamilies']:
        if len(creature.get('targetParts',[]))<4 or len(creature.get('injuryConsequences',[]))<2: errors.append(f'creature target graph incomplete {creature.get("id")}')
    for robot in data['robotChassis']:
        if len(robot.get('targetParts',[]))<7 or len(robot.get('componentConsequences',[]))<7: errors.append(f'robot component graph incomplete {robot.get("id")}')
    for tutorial in data['tutorials']:
        if not tutorial.get('title') or not tutorial.get('body') or not tutorial.get('replayable') or not tutorial.get('skippable'): errors.append(f'tutorial presentation incomplete {tutorial.get("id")}')
if pack_path.is_file() and sidecar.is_file():
    expected=sidecar.read_text().split()[0].lower(); actual=hashlib.sha256(pack_path.read_bytes()).hexdigest()
    if expected != actual: errors.append('Phase 21 content checksum mismatch')
else: errors.append('Phase 21 content checksum sidecar missing')

required_files=[
'src/Roentgen.Content/Roentgen.Content.csproj','src/Roentgen.Content/ContentModels.cs','src/Roentgen.Content/ContentPackLoader.cs','src/Roentgen.Content/ContentValidation.cs','src/Roentgen.Content/ContentPackCompiler.cs','src/Roentgen.Content/ContentRuntime.cs',
'src/Roentgen.World/Phase21WorldContent.cs','src/Roentgen.World/Structures/Phase21StructureGrammarCatalog.cs','src/Roentgen.Items/Phase21ItemContent.cs','src/Roentgen.Rpg/Phase21RpgContent.cs','src/Roentgen.Factions/Phase21SocietyContent.cs','src/Roentgen.Narrative/Phase21NarrativeContent.cs','src/Roentgen.Campaign/Phase21CampaignContent.cs','src/Roentgen.Audio/Phase21AudioContent.cs','src/Roentgen.Music/Phase21MusicContent.cs','src/Roentgen.Combat/Phase21TargetProfileCatalog.cs',
'tools/Roentgen.ContentLab/Roentgen.ContentLab.csproj','tests/Roentgen.Phase21.Tests/Roentgen.Phase21.Tests.csproj','schemas/phase21-content-pack.schema.json','docs/210_PHASE21_CONTENT_COMPLETION.md','docs/211_PHASE21_CONTENT_PIPELINE.md','docs/212_PHASE21_VALIDATION.md']
for path in required_files:
    if not (ROOT/path).is_file(): errors.append(f'missing {path}')
need('src/Roentgen.App/RoentgenGame.cs','Phase21ContentRuntime','Phase21NarrativeContent.CreateLaunchKernels','Roentgen — Phase 21')
need('src/Roentgen.World/WorldGenerationProfile.cs','LaunchCampaign','WorldWidthMeters = 8192','RequiresAllRegionFamilies = true')
need('src/Roentgen.Content/ContentPackCompiler.cs','RCPACK21','SHA256.HashData','File.Move(temporary, path, overwrite: true)')
need('src/Roentgen.Items/Phase21ItemContent.cs','ContentBudgets.WeaponFamilies','AddLaunchMerchants')
need('src/Roentgen.Rpg/Phase21RpgContent.cs','result.Count != 110','Phase12Content.Perks')
need('src/Roentgen.UI/Phase20Models.cs','Phase21ContentRuntime','ContentBudgets.Tutorials','value.Body')
need('src/Roentgen.Audio/Phase21AudioContent.cs','audio.interface.complete','ProfilesBySemanticEvent')
need('src/Roentgen.Music/Phase21MusicContent.cs','ContentBudgets.RegionFamilies','BindingsBySubject')
need('src/Roentgen.Combat/Phase21TargetProfileCatalog.cs','ContentBudgets.CreatureFamilies','Materialize')

props=need('Directory.Build.props','<VersionPrefix>0.21.0</VersionPrefix>','<VersionSuffix>phase21</VersionSuffix>')
manifest_path=ROOT/'eng/projects.json'
if manifest_path.is_file():
    manifest=json.loads(manifest_path.read_text())
    if manifest.get('schemaVersion') != 21: errors.append('projects schema must be 21')
    names={p['name'] for group in ('runtimeProjects','testProjects','toolProjects') for p in manifest[group]}
    for name in ('Roentgen.Content','Roentgen.Phase21.Tests','Roentgen.ContentLab'):
        if name not in names: errors.append(f'project graph missing {name}')
else: errors.append('projects manifest missing')
for csproj in ROOT.rglob('*.csproj'):
    try: ET.parse(csproj)
    except Exception as exc: errors.append(f'invalid XML {csproj.relative_to(ROOT)}: {exc}')

sf=ROOT/'content/music/soundfonts/default.sf2'; sf_side=ROOT/'content/music/soundfonts/default.sf2.sha256'
if not sf.is_file() or not sf_side.is_file(): errors.append('retained SoundFont missing')
else:
    if hashlib.sha256(sf.read_bytes()).hexdigest() != sf_side.read_text().split()[0].lower(): errors.append('retained SoundFont checksum mismatch')

if errors:
    print('Phase 21 contracts FAILED:')
    for error in errors[:200]: print(' -',error)
    if len(errors)>200: print(f' - ... {len(errors)-200} additional errors')
    sys.exit(1)
print('Phase 21 contracts PASSED: launch budgets, stable IDs, references, gameplay adapters, ContentLab, tests, documentation, project graph, and retained SoundFont verified.')
