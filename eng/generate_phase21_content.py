#!/usr/bin/env python3
import json, os, re, hashlib, itertools
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
out=ROOT/'content/roentgen/phase21-launch-content.json'
out.parent.mkdir(parents=True, exist_ok=True)
slug=lambda s: re.sub(r'[^a-z0-9]+','-',s.lower()).strip('-')
regions=[
 ('glass-basin','Glass Basin','Fused ground, exposed foundations, diagnostic mirages and buried urban cuts',['fused-glass','exposed-foundation','mirage','buried-transit'],['glass-storm','heat-refraction','radiographic-reflection'],'silica','audio.region.glass-basin','music.region.glass-basin'),
 ('ossuary-fields','Ossuary Fields','Pale forests and medical-industrial disposal ridges where tissue and mineral deposits persist',['pale-scrub','calcified-forest','waste-ridge','ossuary-channel'],['preservation-bloom','bone-dust','biological-seep'],'biomass','audio.region.ossuary-fields','music.region.ossuary-fields'),
 ('blackwater-works','Blackwater Works','Flooded treatment works, chemical marshes and elevated pipe corridors',['industrial-mud','flooded-works','pipeway','treatment-lagoon'],['chemical-tide','sinking-ground','toxic-vapor'],'chemicals','audio.region.blackwater-works','music.region.blackwater-works'),
 ('broken-meridian','Broken Meridian','A collapsed transport corridor of rail trenches, exchanges, freight depots and roadside settlements',['broken-asphalt','rail-trench','freight-yard','service-tunnel'],['unstable-overpass','traffic-killzone','signal-arc'],'transport','audio.region.broken-meridian','music.region.broken-meridian'),
 ('red-quarry-country','Red Quarry Country','Terraced excavations, exposed rock, conveyor ruins and cliff settlements',['exposed-rock','quarry-terrace','spoil-ridge','mine-cut'],['rockfall','blast-pocket','unstable-cut'],'metals','audio.region.red-quarry','music.region.red-quarry'),
 ('quiet-range','Quiet Range','High ridges, observatories, buried installations and weather-isolated communities',['highland','ridge-road','observatory','buried-bunker'],['electrical-storm','whiteout','signal-hallucination'],'signal-data','audio.region.quiet-range','music.region.quiet-range'),
]
region_defs=[]
for rid,name,identity,terrain,hazards,resource,audio,music in regions:
    landmarks=[f'{rid}.landmark.{i:02d}' for i in range(1,7)]
    region_defs.append(dict(id=rid,name=name,identity=identity,terrainTags=terrain,hazards=hazards,strategicResource=resource,landmarkFamilies=landmarks,audioProfileId=audio,musicProfileId=music))

settlement_specs=[
 ('medical-compound','Medical Compound','medical refuge','treatment and diagnostics','layered gate and clinic guard'),
 ('industrial-campus','Industrial Campus','containment station','chemical processing and recovery','credentials, sealed zones and patrol corridors'),
 ('excavation-settlement','Excavation Settlement','worker cooperative','metal and structural extraction','overwatch terraces and work crews'),
 ('linear-road-town','Linear Road Town','route service stop','transport, lodging and exchange','road gates and overlapping fields of fire'),
 ('fortified-chokepoint','Fortified Chokepoint','defensive post','tolls, security and protected passage','concentric barriers and controlled lanes'),
 ('concentric-civic-town','Concentric Civic Town','regional administration','civic services and distribution','rings of public, controlled and secure access'),
 ('cliff-terrace-town','Cliff Terrace Town','quarry refuge','vertical trade and extraction','elevated stairs, lifts and fallback terraces'),
 ('flood-platform-town','Flood Platform Town','waterworks refuge','fishing, pumping and chemical salvage','raised walks and controlled bridge approaches'),
 ('ruin-infill-community','Ruin Infill Community','salvage commune','repair, reuse and local markets','blocked streets and defended courtyards'),
 ('distributed-agricultural','Distributed Agricultural Settlement','food collective','cultivation, storage and livestock','watch posts and evacuation routes'),
 ('subterranean-bunker','Subterranean Bunker Settlement','sealed refuge','storage, fabrication and archival services','airlocks and compartment defense'),
 ('highland-relay-station','Highland Relay Station','signal enclave','communications, observation and weather forecasting','ridge checkpoints and tower overwatch'),
]
settlements=[]
for i,(sid,name,founding,econ,defense) in enumerate(settlement_specs):
    settlements.append(dict(id=sid,name=name,foundingPurpose=founding,economicRole=econ,defenseDoctrine=defense,districtTags=['civic','market','residential','workshop','defensive']+[('medical' if i==0 else 'industrial' if i in (1,2) else 'agriculture' if i==9 else 'ideological')],serviceTags=['water','food','market','repair','security','medical','transport'],populationCapacity=180+i*35,compatibleRegions=[region_defs[i%6]['id'],region_defs[(i+2)%6]['id']],structureGrammarIds=[]))

room_bases=['public-private-spine','secure-loop','central-machinery-hall','twin-wing-ward','courtyard-circulation','service-ring','vertical-checkpoint','flooded-lower-level','hidden-maintenance-bypass','collapsed-alternate-route','split-industrial-floor','controlled-quarantine','market-courtyard','residential-cluster','loading-yard-spine','observation-tower-core','subterranean-ring','bridge-linked-platforms','terraced-switchback','archive-vault-sequence']
room_variants=['compact','standard','expanded','damaged']
room_motifs=[]
for base in room_bases:
    for vi,var in enumerate(room_variants):
        room_motifs.append(dict(id=f'room-motif.{base}.{var}',name=f'{base.replace("-"," ").title()} — {var.title()}',circulation=base,variant=var,minimumRooms=3+vi,maximumRooms=7+vi*3,requiresAlternateRoute=base in {'secure-loop','hidden-maintenance-bypass','collapsed-alternate-route','controlled-quarantine'},tags=[base,var]))
assert len(room_motifs)==80

structure_groups={
 'residential':['row-shelter','boarding-block','family-courtyard','converted-freight-home','cliff-dwelling','platform-habitation','bunker-dormitory','ruin-infill-house'],
 'civic':['council-house','market-hall','archive-office','courier-station','public-kitchen','assembly-hall','jurisdiction-post'],
 'industrial':['pump-station','water-purifier','transformer-yard','machine-hall','refinery-unit','conveyor-house','quarry-control','mine-access','chemical-separator','fabrication-shop'],
 'medical':['clinic-ward','surgical-theatre','quarantine-house','field-laboratory','diagnostic-archive','mortuary-catalogue'],
 'security':['guard-post','road-checkpoint','containment-gate','watch-tower','regional-command'],
 'survival':['greenhouse','granary','water-cistern','animal-shelter'],
 'transport':['rail-depot','freight-yard-office','collapsed-interchange','service-tunnel-access'],
 'underground':['regional-bunker','utility-vault','subterranean-dormitory','sealed-research-level'],
 'strange':['radiographic-evidence-repository','exposure-chamber']
}
structures=[]; si=0
for group,names in structure_groups.items():
    for name in names:
        motifs=[room_motifs[(si*3)%80]['id'],room_motifs[(si*3+7)%80]['id']]
        structures.append(dict(id=f'structure.{name}',name=name.replace('-',' ').title(),group=group,purposeTags=[group,name],compatibleRegions=[region_defs[si%6]['id'],region_defs[(si+1)%6]['id']],roomMotifIds=motifs,damageProfileIds=[],minimumFloors=1,maximumFloors=1+(si%4),questReservationTags=['evidence','actor','critical-item','alternate-route'],audioProfileId=f'audio.structure.{name}'))
        si+=1
assert len(structures)==50
for i,s in enumerate(structures): settlements[i%12]['structureGrammarIds'].append(s['id'])

damage_names=['fire-partial-repair','flood-sediment','structural-subsidence','controlled-demolition','siege-damage','chemical-corrosion','radiographic-embrittlement','loot-stripping','emergency-reinforcement','vegetation-invasion','internal-excavation','deliberate-sealing','failed-decontamination','vibration-fatigue','impact-collapse','foundation-exposure','quarantine-abandonment','faction-occupation','multiple-reconstruction-eras','recent-combat']
damage=[]
for i,n in enumerate(damage_names):
    damage.append(dict(id=f'damage.{n}',name=n.replace('-',' ').title(),severityPermille=180+(i*37)%700,repairStages=['reconnect-room','move-doorway','add-alternate-passage','remove-debris','relocate-critical-object','reduce-severity'],tags=[n,'bounded-repair']))
for i,s in enumerate(structures): s['damageProfileIds']=[damage[i%20]['id'],damage[(i+5)%20]['id']]

faction_specs=[
 ('prospect-clinic-compact','Prospect Clinic Compact','universal treatment under reciprocal civic duty','care-versus-triage','medical','music.faction.prospect'),
 ('blackwater-recovery-authority','Blackwater Recovery Authority','credentialed industrial containment and controlled access','containment-versus-openness','industrial','music.faction.blackwater'),
 ('red-quarry-cooperative','Red Quarry Cooperative','collective extraction and open accounting','collective-versus-private-control','quarry','music.faction.quarry'),
 ('archive-custodians','Archive Custodians','preserve diagnostic records and restrict dangerous knowledge','preservation-versus-disclosure','archive','music.faction.archive'),
 ('open-diagnosis-movement','Open Diagnosis Movement','make hidden medical and historical information universally accessible','openness-versus-safety','diagnostic','music.faction.open-diagnosis'),
 ('meridian-caravans','Meridian Caravans','control transport knowledge while maintaining neutral exchange','neutrality-versus-obligation','caravan','music.faction.meridian'),
 ('baseline-covenant','Baseline Covenant','protect unaltered human life from deliberate transformation','purity-versus-adaptation','baseline','music.faction.baseline'),
 ('continuance-assembly','Continuance Assembly','use controlled transformation as a survival technology','transformation-versus-consent','altered','music.faction.continuance'),
]
factions=[]
for i,(fid,name,ideology,division,equip,music) in enumerate(faction_specs):
    factions.append(dict(id=fid,name=name,foundingEvent=f'{name} formed after collapse event {i+1} exposed a regional dependency.',ideology=ideology,materialNeeds=[['water','medicine','diagnostic-materials','metals','fuel','food'][i%6],['food','ammunition','electronics','chemicals'][i%4]],taboos=[f'taboo.{fid}.1',f'taboo.{fid}.2'],leadership=['consensus','technical-qualification','election','custodial-college','assembly','contract-council','elder-covenant','adaptive-council'][i],internalDivisions=[division,f'{division}-reformers',f'{division}-hardliners'],equipmentTags=[equip,'field-gear'],settlementTags=[settlements[i%12]['id']],speechTags=[equip,'regional'],objectives=[f'objective.{fid}.survival',f'objective.{fid}.regional'],musicProfileId=music))

org_names=['repair-union','water-rights-council','burial-society','diagnostic-fellowship','caravan-couriers','salvage-crew-network','chemical-guild','agricultural-cooperative','refugee-association','research-circle','criminal-exchange','mercenary-company','mutation-support-group','former-patient-network','archive-fraternity','independent-militia','smuggling-route','weather-watch','rail-workers-league','undertakers-register']
organizations=[]
for i,n in enumerate(org_names):
    organizations.append(dict(id=f'organization.{n}',name=n.replace('-',' ').title(),authorityKind=['professional','civic','religious','commercial','criminal'][i%5],services=['training','rumors','specialized-equipment','quest-leverage'][0:2+(i%3)],compatibleFactions=[factions[i%8]['id'],factions[(i+3)%8]['id']],compatibleRegions=[region_defs[i%6]['id']]))

humanoid_names=['settlement-guard','patrol-rifleman','breacher','marksman','shielded-enforcer','heavy-weapons-operator','demolition-specialist','infiltrator','field-medic','combat-engineer','directed-energy-operator','radiographic-hunter','scavenger','raider','fanatic','faction-officer','warrant-agent','contaminated-survivor','altered-human','elite-diagnostic-operative']
humanoids=[]
for i,n in enumerate(humanoid_names):
    humanoids.append(dict(id=f'humanoid.{n}',name=n.replace('-',' ').title(),combatRole=n,preferredRange=['close','medium','long'][i%3],moraleProfile=['steady','aggressive','cautious','fanatical'][i%4],surrenderThresholdPermille=100+(i*43)%650,skills=[['ballistics','melee','demolitions','medicine','engineering','stealth','directed-energy'][i%7]],equipmentTags=[n.split('-')[0],'armor.field'],targetPartProfileId='target.humanoid.standard',audioProfileId=f'audio.humanoid.{n}',compatibleFactions=[factions[i%8]['id']]))

creature_specs=[
 ('glass-hound','quadruped','pursuit'),('ossuary-grazer','quadruped','herd'),('blackwater-maw','quadruped','ambush'),('quarry-stalker','quadruped','flanker'),('ridge-courser','quadruped','scout'),('filmback-scavenger','quadruped','scavenger'),('white-rib-predator','quadruped','elite'),
 ('hollow-worker','humanoid-mutation','brute'),('lantern-skin','humanoid-mutation','controller'),('echo-patient','humanoid-mutation','mimic'),('calcified-wanderer','humanoid-mutation','tank'),
 ('glass-mite-cloud','arthropod','swarm'),('suture-beetle','arthropod','armored'),('blackwater-louse','arthropod','parasite'),('rail-spider','arthropod','trapper'),('quarry-centipede','arthropod','charger'),
 ('sediment-burrower','burrower','ambush'),('foundation-mole','burrower','terrain-breaker'),('pale-rooter','burrower','herd'),
 ('cable-serpent','serpentine','constrictor'),('marsh-eel','serpentine','electrical'),('film-ribbon','serpentine','radiological'),
 ('phosphor-drifter','floating','controller'),('diagnostic-bloom','floating','support'),
 ('gantry-walker','industrial-mutation','artillery'),('pipe-leg-harvester','industrial-mutation','scavenger'),('surgical-assembly','industrial-mutation','disabler'),
 ('mirror-wing','avian','skirmisher'),('relay-bat','avian','sonic'),('quarantine-colony','colony','area-denial')]
creatures=[]
for i,(n,body,role) in enumerate(creature_specs):
    target=['sensory-organ','jaw','forelimbs','hind-limbs','abdomen']
    if body in ('arthropod','industrial-mutation'): target+=['armor-plates']
    if i%4==0: target+=['poison-gland']
    if body=='avian': target+=['wings']
    creatures.append(dict(id=f'creature.{n}',name=n.replace('-',' ').title(),bodyPlan=body,combatRole=role,habitatTags=[region_defs[i%6]['id'],'nest'],senseTags=[['sight','smell','vibration','radiographic'][i%4]],targetParts=target,injuryConsequences=[f'disable-{p}' for p in target[:3]],lootTags=[f'loot.{n}.tissue',f'loot.{n}.organ'],audioProfileId=f'audio.creature.{n}',musicThreatTag=f'threat.{role}'))
assert len(creatures)==30

robot_names=['inspection-crawler','medical-orderly','surgical-frame','cargo-hauler','security-tripod','tracked-gun-platform','maintenance-spider','relay-drone','floodworks-servitor','quarry-excavator','diagnostic-archivist','heavy-containment-chassis']
robots=[]
for i,n in enumerate(robot_names):
    robots.append(dict(id=f'robot.{n}',name=n.replace('-',' ').title(),mobility=['tracks','legs','wheels','hover'][i%4],combatRole=['inspection','support','melee','transport','security','artillery'][i%6],targetParts=['sensor-assembly','processor','power-unit','mobility','manipulator','weapon-mount','cooling','communications','specialized-subsystem'],componentConsequences=['blind','confused','shutdown','immobilized','disarmed','weapon-disabled','overheat','isolated','role-disabled'],audioProfileId=f'audio.robot.{n}'))

companion_specs=[('clinic-diagnostician','Clinic Diagnostician','medicine'),('quarry-mechanic','Quarry Mechanic','engineering'),('caravan-negotiator','Caravan Negotiator','commerce'),('former-inspector','Former Authority Inspector','security'),('field-naturalist','Field Naturalist','fieldcraft'),('disgraced-officer','Disgraced Faction Officer','leadership'),('altered-survivor','Altered Survivor','radiological'),('salvage-historian','Salvage Historian','science'),('robot-custodian','Robot Custodian','repair'),('settlement-mediator','Settlement Mediator','influence'),('radiographic-researcher','Radiographic Researcher','directed-energy'),('independent-tracker','Independent Tracker','stealth')]
companions=[]
for i,(cid,name,cap) in enumerate(companion_specs):
    companions.append(dict(id=f'companion.{cid}',name=name,primaryCapability=cap,combatPackage=['support','ranged','melee','control'][i%4],ideologicalThresholds=[f'approve.{i}.1',f'object.{i}.1'],personalQuestKernelId=f'quest.companion-{cid}.personal',loyaltyCrisis=f'crisis.{cid}',departureCondition=f'departure.{cid}',endingRuleId=f'epilogue.companion.{cid}',audioProfileId=f'audio.companion.{cid}',musicProfileId=f'music.companion.{cid}'))

weapon_specs=[('revolver','Revolver','ballistics','cartridge'),('self-loading-pistol','Self-loading Pistol','ballistics','cartridge'),('scrap-pistol','Scrap Pistol','ballistics','cartridge'),('shotgun','Shotgun','ballistics','shell'),('hunting-rifle','Hunting Rifle','ballistics','rifle-cartridge'),('automatic-rifle','Automatic Rifle','ballistics','rifle-cartridge'),('submachine-weapon','Submachine Weapon','ballistics','light-cartridge'),('heavy-ballistic','Heavy Ballistic Weapon','ballistics','heavy-cartridge'),('beam-weapon','Beam Weapon','directed-energy','energy-cell'),('arc-weapon','Arc Weapon','directed-energy','energy-cell'),('radiographic-weapon','Radiographic Weapon','directed-energy','radiographic-charge'),('chemical-projector','Chemical Projector','demolitions','chemical-canister'),('improvised-melee','Improvised Melee Weapon','melee','none'),('powered-melee','Powered Melee Weapon','melee','energy-cell')]
weapons=[]
for i,(wid,name,skill,ammo) in enumerate(weapon_specs):
    slots=['receiver','barrel','grip']+(['stock'] if i not in (0,1,2,12) else [])+(['feed'] if ammo!='none' else [])+['sight','power','modification']
    weapons.append(dict(id=f'weapon.{wid}',name=name,skill=skill,ammunitionTag=f'ammo.{ammo}',componentSlots=slots,buildDirections=['reliable','high-damage','specialized'],focusActions=['target-part','predict-penetration'],audioProfileId=f'audio.weapon.{wid}',assetRecipeId=f'weapon/{wid}'))

component_slots=['receiver','barrel','grip','stock','feed','sight','power','modification']
weapon_components=[]
for i in range(80):
    slot=component_slots[i%len(component_slots)]
    fam=weapons[i%14]['id']
    weapon_components.append(dict(id=f'weapon-component.{slot}.{i+1:02d}',name=f'{slot.title()} Assembly {i+1:02d}',slot=slot,compatibleWeaponFamilies=[fam,weapons[(i+3)%14]['id']],effects=[f'{slot}.effect.{i%5}'],assetVariant=f'{slot}-{i%10}'))

armor_specs=[('civilian-layers','Civilian Layered Clothing','social'),('field-armor','Field Armor','general'),('guard-armor','Settlement Guard Armor','ballistic'),('quarry-protection','Quarry Work Protection','blunt'),('containment-suit','Industrial Containment Suit','chemical'),('isolation-equipment','Medical Isolation Equipment','biological'),('scout-gear','Scout and Reconnaissance Gear','mobility'),('infiltration-clothing','Concealment Clothing','stealth'),('heavy-ballistic','Heavy Ballistic Armor','kinetic'),('energy-protection','Directed-energy Protection','thermal'),('exposure-suit','Radiographic Exposure Suit','radiological'),('powered-frame','Powered Support Frame','strength')]
armor=[]
for i,(aid,name,spec) in enumerate(armor_specs):
    armor.append(dict(id=f'armor-family.{aid}',name=name,specialization=spec,layers=['underlayer','clothing','torso','arms','legs','head','respirator','utility'],coverageTags=['head','torso','left-arm','right-arm','left-leg','right-leg'],socialTags=[aid,'faction-readable'],audioProfileId=f'audio.armor.{aid}',assetRecipeId=f'armor/{aid}'))
armor_components=[]
for i in range(60):
    family=armor[i%12]['id']; slot=['underlayer','plate','seal','filter','frame'][i%5]
    armor_components.append(dict(id=f'armor-component.{slot}.{i+1:02d}',name=f'{slot.title()} Component {i+1:02d}',slot=slot,compatibleArmorFamilies=[family,armor[(i+2)%12]['id']],effects=[f'protection.{i%9}',f'social.{i%4}']))

consumable_bases=['trauma-dressing','stimulant','antitoxin','burn-gel','contamination-binder','clean-water','field-ration','pain-suppressant','coagulant','electrolyte']
consumable_grades=['field','standard','refined','concentrated','experimental']
consumables=[]
for base in consumable_bases:
    for g,grade in enumerate(consumable_grades):
        consumables.append(dict(id=f'consumable.{base}.{grade}',name=f'{grade.title()} {base.replace("-"," ").title()}',useKind='self',magnitude=20+g*15,durationTicks=0 if base in ('trauma-dressing','clean-water','field-ration') else 300+g*120,effectTags=[base,f'grade.{grade}'],requirementTags=[] if grade!='experimental' else ['medicine.60']))
assert len(consumables)==50

craft_bases=['filtration','distillation','sterilization','machining','winding','calibration','plating','sealing','fermentation','rendering','chemical-extraction','tissue-preparation','reprogramming','radiographic-exposure','smelting','forging','casting','cutting','grinding','polishing','welding','riveting','lamination','insulation','compounding','synthesis','decontamination','preservation','cooking','drying','salting','assembly','disassembly','repair','reinforcement','diagnosis','reverse-engineering','mapping','signal-tuning','field-construction']
crafting=[]
for i,n in enumerate(craft_bases):
    crafting.append(dict(id=f'craft.{n}',name=n.replace('-',' ').title(),station=['workbench','chemistry-station','medical-station','energy-bench','cooking-fire','armor-frame','diagnostic-table'][i%7],primarySkill=['engineering','science','medicine','fieldcraft'][i%4],difficulty=25+(i%8)*5,inputTags=[f'material.{i%10}',f'tool.{i%5}'],outputTags=[f'process-output.{n}'],failurePolicy=['bounded-loss','recoverable-contamination','damaged-output'][i%3]))

perk_families=['sidearms','rifles','shotguns','heavy-weapons','directed-energy','explosives','melee','unarmed','focus','survival','field-medicine','engineering','science','security','crafting','commerce','influence','companions','exploration','radiological','faction','capstone']
perks=[]
for fi,fam in enumerate(perk_families):
    for rank in range(1,6):
        idx=fi*5+rank
        behavioral=(idx%5)!=0
        effect=['combat.action.unlock','combat.targeting.unlock','focus.knowledge.unlock','interaction.solution.unlock','world.discovery.reveal','dialogue.option.unlock','economy.option.unlock'][idx%7] if behavioral else 'skill.effective.modify'
        perks.append(dict(id=f'perk.{fam}.{rank:02d}',name=f'{fam.replace("-"," ").title()} Doctrine {rank}',family=fam,maximumRank=1,behavioral=behavioral,requirements=[f'level:{max(2,rank*4)}',f'skill:{fam}:{20+rank*10}'],effectType=effect,effectTarget=f'{fam}.tier.{rank}',effectValue=1 if behavioral else 5+rank,tags=[fam,'behavioral' if behavioral else 'numerical']))
assert len(perks)==110

background_specs=[('perimeter-scout','Perimeter Scout','awareness','fieldcraft'),('clinic-orderly','Clinic Orderly','vitality','medicine'),('relay-technician','Relay Technician','intellect','engineering'),('quarry-mechanic','Quarry Mechanic','physique','engineering'),('caravan-factor','Caravan Factor','presence','commerce'),('salvage-inspector','Salvage Inspector','awareness','science'),('settlement-mediator','Settlement Mediator','presence','influence'),('demolition-recovery-worker','Demolition Recovery Worker','reflex','demolitions'),('bunker-resident','Bunker Resident','vitality','security'),('agricultural-worker','Agricultural Worker','physique','fieldcraft'),('authority-clerk','Authority Clerk','intellect','commerce'),('contamination-surveyor','Contamination Surveyor','awareness','science'),('rail-worker','Rail Worker','physique','athletics'),('waterworks-operator','Waterworks Operator','intellect','engineering'),('faction-defector','Faction Defector','presence','stealth'),('remains-cataloguer','Remains Cataloguer','intellect','medicine'),('observatory-assistant','Observatory Assistant','awareness','science'),('independent-hunter','Independent Hunter','reflex','fieldcraft')]
backgrounds=[]
for i,(bid,name,attr,skill) in enumerate(background_specs):
    backgrounds.append(dict(id=f'background.{bid}',name=name,description=f'{name} survived through {skill} work and carries knowledge unavailable to most settlements.',attribute=attr,primarySkill=skill,secondarySkill=['stealth','science','engineering','influence'][i%4],equipmentPackageId=f'package.{bid}',knowledgeTags=[bid,region_defs[i%6]['id']],dialogueTags=[f'background.{bid}'],startingBenefitId=['world.discovery.reveal','skill.option.unlock','interaction.solution.unlock','dialogue.option.unlock'][i%4]))

trait_specs=['methodical','adaptable','impulsive','scar-tissue','radiographic-sensitivity','iron-stomach','commanding','unassuming','last-reserve','heavy-handed','fine-touch','field-naturalist','machine-empathy','lucky-breaks','pack-frame','quiet-breath','storm-blooded','night-eyes','ledger-minded','oath-bound','restless','slow-healer','clean-hands','rough-voice','diagnostic-dreams','route-memory','contamination-tolerance','fragile-frame','jury-rigger','unsettling-calm']
traits=[]
for i,t in enumerate(trait_specs):
    traits.append(dict(id=f'trait.{t}',name=t.replace('-',' ').title(),description=f'{t.replace("-"," ").title()} changes how the character approaches danger and opportunity.',benefitType=['skill','derived-stat','capability','failure-transform'][i%4],benefitTarget=f'benefit.{t}',benefitValue=5+(i%4)*5,drawbackType=['skill','derived-stat','social','recovery'][i%4],drawbackTarget=f'drawback.{t}',drawbackValue=-(3+(i%5)),incompatibleTraitIds=[],tags=[t]))
# preserve obvious incompatibilities
incompat=[('methodical','impulsive'),('commanding','unassuming'),('heavy-handed','fine-touch'),('fragile-frame','pack-frame')]
by={x['id'].split('.',1)[1]:x for x in traits}
for a,b in incompat: by[a]['incompatibleTraitIds'].append(f'trait.{b}'); by[b]['incompatibleTraitIds'].append(f'trait.{a}')

quest_themes=['regional-infrastructure','resource-dependency','faction-access','hidden-diagnosis','medical-anomaly','contamination','transport-failure','settlement-defense','leadership-crisis','crime-and-warrant','trade-dispute','labor-conflict','buried-installation','sealed-archive','hazardous-salvage','missing-person','creature-ecology','robot-malfunction','companion-loyalty','disguise-infiltration','evacuation','water-rights','food-security','power-grid','broadcast-truth','false-evidence','quarantine','succession','expedition','final-convergence']
quest_forms=['investigation','negotiation','recovery','transformation']
route_pool=['combat','stealth','technical','scientific','medical','social','economic','faction-leverage','exploration','engineering','influence','fieldcraft','ballistics','security','commerce','demolitions']
combat_routes={'combat','ballistics','demolitions'}
route_sets=[list(values) for values in itertools.combinations(route_pool,4) if sum(value not in combat_routes for value in values)>=2][:120]
quests=[]
qi=0
for theme in quest_themes:
    for form in quest_forms:
        routes=route_sets[qi]
        qid=f'quest.{theme}.{form}'
        quests.append(dict(id=qid,name=f'{theme.replace("-"," ").title()}: {form.title()}',category=theme,form=form,solutionRoutes=routes,nonCombatRouteCount=sum(r not in ('combat','ballistics','demolitions') for r in routes),hiddenFact=f'fact.hidden.{theme}.{form}',recoveryKinds=['actor-replacement','evidence-substitute','aftermath-route'],consequenceTags=[f'consequence.{theme}',f'consequence.{form}'],followUpIds=[f'follow-up.{theme}.{form}'],compatibleRegions=[region_defs[qi%6]['id'],region_defs[(qi+2)%6]['id']],compatibleFactions=[factions[qi%8]['id'],factions[(qi+3)%8]['id']],dialogueActTags=[f'act.{form}',f'act.{theme}']))
        qi+=1
assert len(quests)==120
# assign companions' quest ids to actual content
for i,c in enumerate(companions): c['personalQuestKernelId']=quests[80+i]['id']

campaign_types=[('opening-crisis',4),('regional-crisis',4),('faction-introduction',4),('revelation',5),('regional-expansion',3),('irreversible-choice',4),('counteraction',2),('final-convergence',4)]
campaigns=[]
ci=0
for typ,count in campaign_types:
    for j in range(count):
        campaigns.append(dict(id=f'campaign.{typ}.{j+1:02d}',name=f'{typ.replace("-"," ").title()} {j+1}',role=typ,requiredQuestCategories=[quest_themes[(ci+j)%30]],compatibleFactionCount=2+(j%3),revelationCount=1 if typ=='revelation' else 0,irreversible=typ in ('irreversible-choice','final-convergence'),endingTags=[f'ending.{typ}.{j+1}'] if typ=='final-convergence' else [],replacementPolicy='critical-role-ladder'))
    ci+=count
assert len(campaigns)==30

intents=['greet','request','refuse','bargain','reveal','accuse','deflect','threaten','surrender','confess','diagnose','explain','rumor','doctrine','object','support','farewell','aftermath','warn','promise','question','challenge','mediate','trade','end']
tones=['measured','hostile','fearful','hopeful','clinical','plainspoken','ritual','weary','urgent','guarded']
dialogue=[]
for intent in intents:
    for tone in tones:
        dialogue.append(dict(id=f'dialogue.{intent}.{tone}',intent=intent,tone=tone,template=f'{{SPEAKER}} delivers a {tone} {intent} concerning {{SUBJECT}} while accounting for {{RELATIONSHIP}} and {{RECENT_EVENT}}.',requiredFacts=[],knowledgeFilter=True,relationshipAware=True,factionTerminology=True))
assert len(dialogue)==250

rumor_bases=['missing-shipment','contaminated-well','hidden-clinic','faction-betrayal','creature-migration','robot-patrol','sealed-bunker','lost-caravan','false-diagnosis','collapsed-route','forbidden-treatment','secret-alliance','quarry-blast','blackwater-rise','archive-theft','companion-past','leadership-change','market-shortage','radiographic-storm','buried-machine']
distortions=['actor-substitution','faction-attribution','location-drift','quantity-exaggeration','motive-substitution']
rumors=[]
for base in rumor_bases:
    for d in distortions:
        rumors.append(dict(id=f'rumor.{base}.{d}',baseFactTag=f'fact.{base}',distortion=d,template=f'Word says {{ACTOR}} is tied to {base.replace("-"," ")} near {{LOCATION}}, though the telling shows {d.replace("-"," ")}.',maximumTransmissionDepth=5,reliabilityLossPermille=120))
assert len(rumors)==100

tutorial_bodies={
'movement-interaction':'Move, look, crouch, jump, and use the contextual interaction action. Prompts display the current binding and unavailable actions explain what is missing.',
'inventory-equipment':'Inspect mass, condition, ammunition, modifications, and layered equipment before committing scarce supplies or changing a loadout.',
'real-time-combat':'Aim, fire, reload, use cover, and read combat feedback. Injuries and damaged equipment alter what every combatant can do.',
'armor-injuries':'Armor protects specific anatomical regions and degrades by material interaction. Treat bleeding, trauma, contamination, and impaired limbs deliberately.',
'roentgen-focus':'Pause the simulation, diagnose target parts, inspect hit and injury forecasts, queue actions within the Focus pool, then commit the sequence.',
'character-advancement':'Gain levels, assign skill points, and improve attributes at major thresholds. Familiarity and discoveries supplement rather than replace advancement.',
'perks':'Perks unlock behaviors, routes, targeting options, faction leverage, and build-defining capabilities. Requirements are shown before purchase.',
'crafting-repair':'Use the correct station, process, skill, and compatible parts. Repair quality depends on condition, material, tools, and engineering knowledge.',
'dialogue-evidence':'Dialogue choices can depend on skills, knowledge, evidence, relationships, obligations, reputation, equipment, and recent events.',
'faction-reputation':'Trust, fear, respect, obligation, ideological alignment, and known crimes are tracked separately and can produce different reactions.',
'crimes-disguises':'Jurisdictions remember witnessed crimes. Clothing, credentials, behavior, and familiarity determine whether a disguise is accepted or exposed.',
'companions':'Issue tactical orders, use a companion’s noncombat specialty, and consider their ideology, confidence, personal quest, and departure conditions.',
'settlement-services':'Settlements provide distinct food, water, medical, repair, trade, information, transport, training, and contamination services.',
'map-travel':'Use landmarks, roads, trails, discovered routes, and regional hazards to plan travel. Some paths change with weather, conflict, or infrastructure damage.',
'contamination-treatment':'Exposure carries both physical danger and diagnostic information. Identify the source before selecting medicine, isolation, filtration, or controlled exposure.',
'campaign-choices':'Major commitments alter factions, settlements, companions, infrastructure, and endings. Irreversible choices receive an explicit warning and pre-choice save.',
'saving-loading':'Manual, quick, rotating automatic, transition, and pre-choice saves use checksums, atomic replacement, backup recovery, and schema validation.',
'accessibility':'Remap controls and adjust interface scale, subtitles, contrast, color vision, motion effects, flashes, camera behavior, Focus intensity, difficulty, and audio buses.'
}
tutorials=[]
for i,(t,body) in enumerate(tutorial_bodies.items()): tutorials.append(dict(id=f'tutorial.{t}',title=t.replace('-',' ').title(),body=body,triggerFact=f'tutorial.trigger.{t}',completionFact=f'tutorial.complete.{t}',replayable=True,skippable=True,priority=1000-i*10))

ending_names=['repair-and-disclosure','negotiated-containment','controlled-transformation','long-evacuation','quiet-shutdown','single-custodian','destructive-liberation','survival-without-resolution']
epilogues=[]
for n in ending_names: epilogues.append(dict(id=f'epilogue.ending.{n}',subjectKind='world',conditionTag=f'ending.{n}',template=f'The campaign closed through {n.replace("-"," ")}, reshaping every surviving institution.'))
for c in companions: epilogues.append(dict(id=c['endingRuleId'],subjectKind='companion',conditionTag=f'companion.{c["id"]}.resolved',template=f'{c["name"]} carried the consequences of the campaign into a changed region.'))
for f in factions: epilogues.append(dict(id=f'epilogue.faction.{f["id"]}',subjectKind='faction',conditionTag=f'faction.{f["id"]}.survives',template=f'{f["name"]} entered the epilogue altered by trust, fear, respect, obligation and known crimes.'))
for s in settlements: epilogues.append(dict(id=f'epilogue.settlement.{s["id"]}',subjectKind='settlement',conditionTag=f'settlement.{s["id"]}.survives',template=f'{s["name"]} endured with its services, population and controlling authority changed.'))
for r in region_defs: epilogues.append(dict(id=f'epilogue.region.{r["id"]}',subjectKind='region',conditionTag=f'region.{r["id"]}.resolved',template=f'{r["name"]} retained visible evidence of every repair, abandonment and conflict.'))
# additional systemic outcomes to reach robust library
for i in range(50): epilogues.append(dict(id=f'epilogue.systemic.{i+1:02d}',subjectKind='system',conditionTag=f'world.systemic.{i+1:02d}',template=f'Systemic outcome {i+1} records trade, migration, ecology, disclosure and infrastructure consequences.'))

# Audio profiles generated from every launch family.
audio=[]
def add_audio(aid, category, events):
    audio.append(dict(id=aid,category=category,semanticEvents=events,variationCount=max(4,len(events)*2),spatial=True,qualityBudget='runtime'))
for r in region_defs: add_audio(r['audioProfileId'],'region',['ambient-bed','weather','hazard','landmark'])
for s in structures: add_audio(s['audioProfileId'],'structure',['interior-bed','machinery','door','debris'])
for h in humanoids: add_audio(h['audioProfileId'],'humanoid',['idle','alert','attack','pain','injury','retreat','death','foley'])
for c in creatures: add_audio(c['audioProfileId'],'creature',['idle','alert','threat','attack','pain','injury','retreat','death','locomotion','habitat'])
for r in robots: add_audio(r['audioProfileId'],'robot',['idle','locomotion','sensor','weapon','damage','component-failure','power-failure','shutdown'])
for c in companions: add_audio(c['audioProfileId'],'companion',['acknowledge','object','support','injured','downed','quest','farewell'])
for w in weapons: add_audio(w['audioProfileId'],'weapon',['fire','mechanism','reload','dry-failure','malfunction','distant','enclosed','outdoor'])
for a in armor: add_audio(a['audioProfileId'],'armor',['equip','unequip','movement','impact','damage'])
add_audio('audio.interface.complete','interface',['navigate','confirm','cancel','error','focus-enter','focus-queue','focus-commit','focus-exit','journal','map','craft'])
# Music bindings
music=[]
for r in region_defs: music.append(dict(id=r['musicProfileId'],category='region',subjectId=r['id'],styleId={'glass-basin':'201','ossuary-fields':'202','blackwater-works':'203','broken-meridian':'204','red-quarry-country':'205','quiet-range':'206'}[r['id']],states=['exploration','suspicion','combat','aftermath']))
for f in factions: music.append(dict(id=f['musicProfileId'],category='faction',subjectId=f['id'],styleId=f'21{len(music)%10}',states=['neutral','trusted','hostile','victory','defeat']))
for c in companions: music.append(dict(id=c['musicProfileId'],category='companion',subjectId=c['id'],styleId=f'22{len(music)%10}',states=['travel','loyalty','crisis','resolution']))
for i,n in enumerate(['discovery','suspicion','combat','elite-combat','boss','escape','revelation','tragedy','victory','ending']): music.append(dict(id=f'music.state.{n}',category='dramatic-state',subjectId=n,styleId=f'21{8+i%6}',states=[n]))
for i,n in enumerate(ending_names): music.append(dict(id=f'music.ending.{n}',category='ending',subjectId=n,styleId='223',states=['epilogue','credits']))

pack=dict(schemaVersion=21,packageId='roentgen.launch-content',contentVersion='0.21.0',
 regions=region_defs,settlementGrammars=settlements,structureGrammars=structures,roomMotifs=room_motifs,damageProfiles=damage,
 factions=factions,organizations=organizations,humanoidArchetypes=humanoids,creatureFamilies=creatures,robotChassis=robots,companionArchetypes=companions,
 weaponFamilies=weapons,weaponComponents=weapon_components,armorFamilies=armor,armorComponents=armor_components,consumables=consumables,craftingProcesses=crafting,
 perks=perks,backgrounds=backgrounds,traits=traits,questKernels=quests,campaignKernels=campaigns,dialogueActs=dialogue,rumorTemplates=rumors,
 tutorials=tutorials,epilogueRules=epilogues,audioProfiles=audio,musicBindings=music)
text=json.dumps(pack,indent=2,ensure_ascii=False,sort_keys=False)+'\n'
out.write_text(text,encoding='utf-8')
sha=hashlib.sha256(text.encode()).hexdigest()
(out.parent/'phase21-launch-content.sha256').write_text(f'{sha}  phase21-launch-content.json\n',encoding='utf-8')
print(out, out.stat().st_size, sha)
for k,v in pack.items():
    if isinstance(v,list): print(k,len(v))
