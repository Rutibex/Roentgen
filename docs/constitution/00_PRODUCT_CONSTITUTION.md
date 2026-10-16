# Roentgen Product Constitution

**Document status:** Binding Phase 0 baseline  
**Constitution version:** 0.1.0  
**Target release:** Roentgen 1.0

---

## Article I — Product identity

### 1.1 Title

The game is named **Roentgen**.

### 1.2 Product statement

Roentgen is a finite, completable, single-player, first-person, post-apocalyptic computer role-playing game in which the world, assets, inhabitants, soundscape, score, and much of the campaign structure are reconstructed deterministically from code, rules, semantic templates, and seeds.

The game is not an endless procedural sandbox and not a technical demo. A valid campaign must have:

- A comprehensible opening.
- Escalating regional conflicts.
- Major faction choices.
- A central mystery or revelation chain.
- A final convergence.
- One or more valid endings.
- A generated epilogue reflecting player choices.

### 1.3 Player experience promise

A completed version of Roentgen must let the player:

- Create a distinct character build.
- Explore a coherent generated wasteland.
- Discover settlements, ruins, interiors, and hidden routes.
- Fight in real time or use the full-pause **Roentgen Focus** targeting system.
- Target meaningful anatomical or mechanical components.
- Resolve major problems through combat, stealth, technical, scientific, medical, social, economic, or factional methods.
- Develop relationships with factions and companions.
- Alter persistent world conditions.
- Finish a main campaign and receive an epilogue.

### 1.4 Primary design pillars

#### Pillar A — Generated, not arbitrary

Generated content must express causes, history, and gameplay purpose. Random placement without semantic or systemic justification is not sufficient.

#### Pillar B — Systemic role-playing

Character abilities, world state, relationships, equipment, injuries, and knowledge must open or close concrete options.

#### Pillar C — Diagnose and exploit

The game’s identity centers on discovering hidden structure: anatomy, machinery, weaknesses, contamination, lies, infrastructure, and historical layers.

#### Pillar D — Small distribution, large reconstruction

Roentgen stores generators, recipes, seeds, templates, and deltas rather than a conventional library of final models, textures, recorded sound effects, or rendered music.

#### Pillar E — Finishability over theoretical infinity

Every generator must be bounded, testable, inspectable, and repairable. The launch campaign must be completable across the supported seed space.

---

## Article II — Scope boundaries

### 2.1 Included in version 1.0

- First-person exploration.
- Real-time ranged and melee combat.
- Full-pause body-part targeting through Roentgen Focus.
- Character attributes, skills, traits, backgrounds, levels, and perks.
- Inventory, equipment condition, repairs, modifications, crafting, and trade.
- Procedural terrain, roads, settlements, buildings, interiors, props, weapons, armor, characters, creatures, materials, textures, sound effects, ambience, and adaptive music.
- Factions, reputation, crimes, disguises, companions, schedules, and strategic settlement simulation.
- Generated quests assembled from authored kernels and world facts.
- A finite main campaign with multiple outcomes.
- Manual saves, quick saves, autosaves, migrations, backups, and corruption checks.
- Keyboard, mouse, and gamepad support.
- Core accessibility settings.
- Windows x64 release.
- Linux x64 compatibility target after Windows stabilization.

### 2.2 Explicitly excluded from version 1.0

- Multiplayer or cooperative play.
- Live-service seasons.
- Online-only generation.
- Runtime dependence on a cloud language model.
- Photorealistic graphics.
- Fully deformable terrain.
- General-purpose rigid-body destruction.
- Driveable open-world vehicles.
- Base-building as a primary game mode.
- Infinite character leveling.
- Endless procedural campaign generation after the main ending.
- Full voice acting.
- Imported commercial model, texture, animation, music, or sound libraries as required launch content.
- Modding support before the core data formats stabilize.

### 2.3 Scope-change rule

An excluded feature may enter 1.0 only if an equivalent amount of work is removed and the change passes constitutional review. “It would be cool” is not sufficient justification.

---

## Article III — World and setting identity

### 3.1 Setting premise

Before the collapse, a network of industrial, medical, infrastructural, and military diagnostic systems was built to inspect hidden flaws in matter and living tissue. During the catastrophe, the network exposed, catalogued, altered, and weaponized what it observed. Civilization failed unevenly, leaving regions shaped by contamination, failed automation, improvised medicine, buried infrastructure, and societies with incompatible beliefs about diagnosis and repair.

### 3.2 Thematic vocabulary

Roentgen’s fiction should repeatedly engage with:

- Exposure and concealment.
- Diagnosis and denial.
- Repair, replacement, and amputation.
- Bodies treated as systems.
- Machines treated as patients.
- Medical knowledge used for healing or control.
- Radiation as contamination, information, and transformation.
- Hidden structures revealed beneath surfaces.
- The ethical cost of “fixing” people or societies.

### 3.3 Tonal range

The setting may be bleak, strange, satirical, humane, horrifying, or darkly funny, but it must not become emotionally uniform. Settlements need ordinary life, affection, humor, work, culture, and hope so that loss and danger remain meaningful.

### 3.4 Originality rule

Roentgen may inherit the broad traditions of post-apocalyptic CRPGs, tabletop role-playing, immersive sims, and retro science fiction. It must not reproduce another franchise’s protected terminology, mascot imagery, faction identities, interface metaphors, quest text, perk names, lore, or distinctive presentation.

---

## Article IV — Campaign constitution

### 4.1 Campaign duration target

A normal first campaign should support approximately:

- 30–50 hours for the main story and major faction content.
- Additional exploration and side content beyond that range.
- A fast critical path for repeat players.

These are design targets, not promises of identical playtime for every build.

### 4.2 Mandatory campaign shape

Every generated campaign must contain:

1. A constrained opening region that teaches survival and interaction.
2. An early local crisis with at least three resolution families.
3. Introduction to at least three major factions.
4. A revelation that changes the meaning of the opening problem.
5. Expansion into all major environmental regions.
6. A midgame irreversible or costly alignment decision.
7. A second revelation concerning the diagnostic network or its consequences.
8. A final convergence involving at least three major world-state variables.
9. A valid final resolution.
10. Epilogues for major settlements, factions, companions, and the player.

### 4.3 Campaign validity

A campaign is invalid if:

- The main quest becomes impossible because a critical actor dies without replacement logic.
- A required site is unreachable.
- Required evidence cannot be acquired.
- All valid endings are blocked.
- The player is forced into a skill or faction path that was not constitutionally declared mandatory.
- The final outcome contradicts established world facts.

### 4.4 Procedural authorship model

Roentgen uses authored semantic kernels rather than unconstrained story generation.

A kernel defines:

- Dramatic roles.
- Required facts.
- Interests and conflicts.
- Hidden information.
- Valid solution families.
- Consequences.
- Failure recovery.

The generator binds those roles to generated people, factions, places, resources, and histories.

---

## Article V — Character constitution

### 5.1 Core attributes

Attributes range from 1 to 10.

- **Physique** — force, carrying capacity, heavy handling, knockdown resistance, physical intimidation.
- **Awareness** — detection, ranged precision, trap recognition, initiative, diagnostic clarity.
- **Vitality** — health, stamina, contamination tolerance, recovery, endurance.
- **Presence** — persuasion, deception, leadership, social intimidation, first impressions.
- **Intellect** — skill growth, science, engineering, medicine, hacking, analysis.
- **Reflex** — action capacity, movement, reload speed, stealth motion, evasion.
- **Fortune** — critical variance, unusual opportunities, rare discoveries, gambling, catastrophic-failure mitigation.

These names are canonical unless changed by constitutional amendment.

### 5.2 Skills

Skills range from 0 to 100.

1. Ballistics.
2. Directed Energy.
3. Demolitions.
4. Melee.
5. Unarmed.
6. Athletics.
7. Stealth.
8. Security.
9. Engineering.
10. Science.
11. Medicine.
12. Fieldcraft.
13. Influence.
14. Commerce.

### 5.3 Character creation

Character creation must include:

- Attribute allocation.
- Three specialized skills.
- Background.
- Two traits.
- Starting equipment package.
- Optional questionnaire-generated recommendation.
- Complete manual override.

No questionnaire answer may permanently hide manual choices.

### 5.4 Advancement

- Maximum launch level: 50.
- Skill points: every level.
- Perk point: every two levels unless balance testing changes cadence globally.
- Attribute increase: at major thresholds or through rare permanent opportunities.
- No infinite repeatable level scaling in the main campaign.
- Training, manuals, discoveries, and procedures may provide bounded permanent bonuses.

### 5.5 Build viability

At minimum, launch validation must demonstrate viable campaign completion for:

- Ballistics combat specialist.
- Melee or unarmed specialist.
- Stealth and security specialist.
- Engineering and science specialist.
- Medicine and fieldcraft specialist.
- Influence and commerce specialist.

A viable build may use companions, consumables, or faction alliances, but it must not require abandoning its declared specialty.

---

## Article VI — Perk constitution

### 6.1 Perk purpose

Perks alter rules, unlock actions, transform play styles, or create new solutions. Pure percentage increases are allowed but must not dominate the system.

### 6.2 Perk composition target

- At least 70% behavior, interaction, or option-changing perks.
- At most 30% primarily numerical perks.
- Launch target: 100–120 perks, including ranks.
- Every major combat and noncombat skill must have a meaningful progression path.

### 6.3 Perk families

- Ballistics.
- Directed Energy.
- Explosives.
- Melee.
- Unarmed.
- Roentgen Focus.
- Survival and fieldcraft.
- Technical and scientific.
- Medical.
- Social and factional.
- Commerce and scavenging.
- Exploration and navigation.
- Mutation and radiological adaptation.
- Companion leadership.

### 6.4 Perk originality

Perks may cover common genre functions but may not reproduce another game’s distinctive name, description, iconography, rank sequence, or exact mechanical package.

---

## Article VII — Combat constitution

### 7.1 Combat modes

Roentgen has one combat simulation expressed through two control modes:

- Real-time direct control.
- Full-pause tactical planning through Roentgen Focus.

They must share the same weapon, projectile, armor, injury, probability, and status systems.

### 7.2 Damage categories

Canonical launch categories:

- Kinetic.
- Cutting.
- Blunt.
- Thermal.
- Electrical.
- Radiological.
- Chemical.
- Biological.
- Sonic.

### 7.3 Locational damage

Humans, creatures, and machines expose target-part profiles. Damage to a part must have gameplay consequences, not merely a floating damage multiplier.

### 7.4 Armor model

Armor may use:

- Coverage by body part.
- Flat threshold.
- Percentage resistance.
- Material interaction.
- Penetration.
- Degradation.
- Special vulnerability.

### 7.5 Weapon condition

Condition can affect:

- Reliability.
- Accuracy.
- Recoil.
- Energy efficiency.
- Heat.
- Reload behavior.
- Damage consistency.

Condition must never create unavoidable failure without visible warning and mitigation.

### 7.6 Combat readability

The player must be able to inspect:

- Why a shot is inaccurate.
- Why damage was reduced.
- Which part was hit.
- Which injury or status was applied.
- Why an enemy’s behavior changed.
- What Roentgen Focus predicts and what remains uncertain.

---

## Article VIII — Roentgen Focus constitution

### 8.1 Canonical name

The body-part targeting system is named **Roentgen Focus**.

### 8.2 Required behavior

When activated:

1. Simulation time pauses completely.
2. Interface and tactical camera remain responsive.
3. Targets and valid target parts are identified according to player knowledge and visibility.
4. The player spends a finite Focus resource to queue actions.
5. The interface displays probability, predicted effects, armor interaction, and uncertainty.
6. The player commits the sequence.
7. Simulation resumes in controlled slow time for execution.
8. Normal control returns after completion or interruption.

### 8.3 Target families

Humanoids must support at least:

- Head.
- Torso.
- Left arm.
- Right arm.
- Left leg.
- Right leg.
- Equipped weapon where physically exposed.

Machines and creatures use authored body-part graphs appropriate to their construction.

### 8.4 Consequence rule

A target part is valid only when damaging it can change at least one of:

- Available attacks.
- Accuracy.
- Movement.
- Senses.
- Defense.
- Morale.
- Hazard production.
- Special ability.
- Resource integrity.
- Death or incapacitation path.

### 8.5 Accessibility

Players may disable or reduce:

- Cinematic camera cuts.
- Flashing.
- Chromatic distortion.
- Overlay intensity.
- Slow-motion intensity.
- Camera shake.

The system must remain fully usable with those effects disabled.

---

## Article IX — World generation constitution

### 9.1 History before decoration

The generator must determine infrastructure, collapse events, migration, faction history, current needs, and quest reservations before decorative clutter.

### 9.2 Region library

The launch library contains six canonical environmental identities:

1. Glass Basin.
2. Ossuary Fields.
3. Blackwater Works.
4. Broken Meridian.
5. Red Quarry Country.
6. Quiet Range.

Campaigns vary their shape, adjacency, ownership, internal history, and generated sites.

### 9.3 World scale

The launch target is a dense traversable world roughly equivalent to an 8 km by 8 km outdoor region, plus interiors and underground spaces. Exact topology may make the traversable area nonrectangular.

### 9.4 Generation order

Required order:

1. Seed domains.
2. Pre-collapse geography and infrastructure.
3. Collapse history.
4. Present-day factions and resources.
5. Campaign reservations.
6. Regional topology and transport graph.
7. Settlements and major sites.
8. Structures and interiors.
9. Encounters and ecology.
10. Decorative assets.
11. Validation.
12. Repair.

### 9.5 Generator obligations

Every critical generator must provide:

- Deterministic input.
- Versioned output behavior.
- Provenance.
- Validation.
- Bounded repair.
- Diagnostic export.
- Seed reproduction.
- Performance budget.

A generator that can fail but cannot explain or reproduce the failure is incomplete.

---

## Article X — Procedural asset constitution

### 10.1 Stored content

The shipped game may store:

- Code.
- Shader source or compiled effects.
- Fonts and legally distributable interface necessities.
- Semantic recipes.
- Numeric parameters.
- Text templates.
- Small hand-authored icons where procedural generation is impractical and approved.
- Testing fixtures.

### 10.2 Generated content

The target architecture generates or reconstructs:

- Terrain geometry.
- Prop and structure meshes.
- Character and creature bodies.
- Weapons and armor.
- Materials and texture atlases.
- Damage variations.
- Sound effects and ambience.
- Musical composition and instrument rendering.

### 10.3 Exception policy

Any conventional asset added to the shipping build requires:

- A reason procedural generation is unsuitable.
- License verification.
- Size impact.
- Runtime impact.
- A decision record.

### 10.4 Visual target

The art direction prioritizes:

- Strong silhouettes.
- Low-poly industrial construction.
- Limited palettes.
- Procedural wear.
- Atmospheric depth.
- Diagnostic overlays.
- Coherence over realism.

---

## Article XI — Audio and music constitution

### 11.1 Sound effects

Playroma Sound is the canonical procedural sound engine.

Roentgen will integrate its platform-neutral runtime components and provide a MonoGame output host. The game must not depend on the WinForms authoring application or NAudio playback host at runtime.

### 11.2 Music

Playroma Music is the canonical composition system. Final runtime synthesis should use Playroma Sound through a dedicated backend so Roentgen does not require a bundled SoundFont.

### 11.3 Runtime safety

- No graph compilation on the realtime audio callback.
- No unbounded allocation on the audio callback.
- Bounded command queues.
- Voice limits and virtualization.
- Underrun telemetry.
- Deterministic identity for generated cues and effects.

### 11.4 Adaptive score

Music must react to at least:

- Region.
- Location type.
- Faction control.
- Threat.
- Combat intensity.
- Discovery.
- Quest state.
- Player condition.
- Companion state.
- Time and weather where appropriate.

Transitions must occur musically rather than by abrupt track replacement whenever possible.

---

## Article XII — AI, factions, and simulation

### 12.1 Simulation levels

- Full simulation for nearby active actors.
- Reduced simulation for unloaded local actors.
- Strategic simulation for distant communities and factions.

### 12.2 NPC autonomy

NPCs should be able to:

- Work.
- Eat.
- Sleep.
- Travel.
- Trade.
- Patrol.
- Investigate.
- Fight.
- Retreat.
- Surrender.
- Heal.
- Repair.
- Respond to crimes and faction orders.

### 12.3 Faction reputation

Faction reputation is multidimensional. Canonical dimensions:

- Trust.
- Fear.
- Respect.
- Obligation.
- Ideological alignment.
- Known crimes.

A single positive or negative meter is insufficient for major factions.

### 12.4 Companion minimum

A campaign should generate or select 4–6 recruitable companions from a launch library of at least 12 companion archetypes.

Companions require:

- Personal objectives.
- Moral boundaries.
- Combat role.
- Noncombat contribution.
- Relationship reactions.
- Departure or conflict conditions.
- Ending state.

---

## Article XIII — Interface and accessibility

### 13.1 Interface metaphor

The interface uses a portable diagnostic and radiographic visual language. It must not imitate a wrist computer, mascot-driven display, or another franchise’s distinctive interface.

### 13.2 Clarity over fiction

Fictional presentation may decorate the UI but may not obscure:

- Text.
- Controls.
- Probabilities.
- Inventory comparisons.
- Quest state.
- Accessibility settings.

### 13.3 Mandatory accessibility baseline

- Rebindable keyboard, mouse, and gamepad controls.
- Interface scaling.
- Subtitle size.
- High-contrast options.
- Color-vision presets.
- FOV adjustment.
- Mouse sensitivity.
- Camera-shake toggle.
- Head-bob toggle.
- Flash reduction.
- Motion-effect reduction.
- Separate audio category volumes.
- Hold/toggle behavior for aim, crouch, sprint, and interaction where relevant.
- Roentgen Focus visual-intensity controls.

---

## Article XIV — Determinism, persistence, and compatibility

### 14.1 Stable randomness

Gameplay generation must not depend on unspecified behavior of platform random-number implementations. Roentgen will use a project-owned, versioned random algorithm.

### 14.2 Named seed domains

Random streams must be derived by stable domain names. Adding decorative generation must not change unrelated narrative, NPC, loot, or world outcomes.

### 14.3 Save model

Save files store:

- Campaign seed.
- Generator versions.
- Content fingerprint.
- Player and companion state.
- World-state deltas.
- Faction and quest state.
- Changed, destroyed, moved, looted, or placed entities.

Unchanged base content is reconstructed.

### 14.4 Compatibility

Generator changes may not silently mutate existing campaigns.

Supported responses to a generator change:

- Preserve old behavior behind a version boundary.
- Perform an explicit tested migration.
- Declare the save incompatible with a clear explanation and backup preservation.

### 14.5 Save safety

Required:

- Atomic save replacement.
- Rolling backups.
- Corruption detection.
- Schema versioning.
- Migration tests.
- Clear recovery messages.

---

## Article XV — Tools and validation

### 15.1 Mandatory internal tools

The project requires:

- WorldLab.
- AssetLab.
- StructureLab.
- CreatureLab.
- QuestLab.
- BalanceLab.
- Seed Inspector.
- Replay runner.
- Soak-test runner.

### 15.2 Tool-first rule

A generator family may not enter content production until a developer can:

- Generate examples rapidly.
- Inspect provenance.
- Reproduce a seed.
- See validation failures.
- Run repair stages.
- Compare variants.
- Export diagnostics.

### 15.3 Automated completion testing

Before release, automated agents must be able to complete representative campaigns and report:

- Critical path.
- Skills used.
- Combat frequency.
- Required resources.
- Failed objectives.
- Recovery routes.
- Ending reached.
- World checksum.

---

## Article XVI — Quantitative launch targets

### 16.1 Content targets

Launch targets include:

- 6 region families.
- 12 major settlements per campaign.
- 20–30 minor inhabited sites.
- 36–50 major landmark instances.
- 120–200 minor exploration locations.
- 8 major faction families.
- 12–20 minor organizations.
- 50 structure grammars.
- 120 quest kernels.
- 30 campaign milestone kernels.
- 30 creature families.
- 20 humanoid enemy archetypes.
- 14 weapon families.
- 12 armor families.
- 100–120 perks.
- 12 companion archetypes.

### 16.2 Runtime targets

- Fixed 60 Hz simulation.
- 60 FPS target at 1080p on the defined mid-range reference system.
- Typical memory usage below 2 GB.
- Typical save below 5 MB.
- No visible streaming interruption during ordinary traversal.
- No audio underruns under supported load.
- User-configurable generated-asset cache.

### 16.3 Distribution targets

- Procedural game data and recipes: under 25 MB compressed.
- Self-contained Windows distribution: target under 150 MB.
- No required SoundFont.
- No required conventional model or texture library.
- No required conventional recorded sound library.

---

## Article XVII — Definition of done

Roentgen 1.0 is finished only when:

- The complete main campaign can be generated and completed across the release seed suite.
- All declared launch systems exist and are integrated.
- All content budgets are met or formally amended.
- Roentgen Focus supports every combat archetype.
- Every critical generator validates and repairs its output.
- Multiple combat and noncombat builds are viable.
- The procedural sound catalog covers all required semantic events.
- Adaptive music works without an external SoundFont.
- Save migration, backup, and recovery are tested.
- Accessibility requirements are implemented.
- Keyboard, mouse, and gamepad paths are complete.
- Windows release packaging is validated.
- The Linux target passes the declared compatibility suite.
- No critical or high-severity defect remains.
- The release candidate passes clean-machine installation and full-campaign completion tests.
- Credits, licenses, notices, and originality review are complete.
- Developer commands are not required to complete the game.

---

## Article XVIII — Governance

### 18.1 Decision classes

- **Constitutional:** product identity, core systems, launch scope, mandatory terminology, determinism policy, release definition.
- **Architectural:** subsystem boundaries, libraries, data formats, threading, rendering strategy.
- **Content:** specific perk, faction, region, quest, creature, weapon, or style additions.
- **Tuning:** formulas and values that do not alter the declared system’s purpose.

### 18.2 Required record

Constitutional and architectural changes require an ADR containing:

- Context.
- Decision.
- Alternatives.
- Consequences.
- Migration.
- Test changes.
- Rollback plan.

### 18.3 Precedence

When documents conflict, precedence is:

1. Product Constitution.
2. Accepted ADRs.
3. Phase specifications.
4. Content schemas.
5. Implementation notes.
6. Source comments.

