# Acceptance Tests

## A. Phase 0 completion tests

Phase 0 is complete only when all of the following are true:

- [ ] Product Constitution exists and has a version.
- [ ] Included and excluded launch scope is explicit.
- [ ] Product pillars are explicit.
- [ ] Attribute and skill lists are canonical.
- [ ] Roentgen Focus is named and behaviorally defined.
- [ ] Campaign shape and validity rules are defined.
- [ ] Procedural asset policy is defined.
- [ ] Determinism and save-compatibility policies are defined.
- [ ] Audio and music ownership boundaries are defined.
- [ ] Content budgets exist.
- [ ] Performance and distribution budgets exist.
- [ ] Originality rules exist.
- [ ] Initial risks have owners and controls.
- [ ] Initial JSON schemas parse successfully.
- [ ] Canonical C# contracts compile in Phase 1.
- [ ] Decision-log entries cover the major Phase 0 choices.

## B. Product-level invariants

### Campaign

- [ ] Every release seed creates at least one valid ending path.
- [ ] Required locations are reachable.
- [ ] Critical actors have replacement, inheritance, or recovery logic.
- [ ] Main-quest evidence cannot be permanently destroyed without an alternate source.
- [ ] Faction choices alter later content and epilogues.
- [ ] At least three solution families exist for every major crisis.

### Character builds

- [ ] Ballistics specialist completion.
- [ ] Melee/unarmed specialist completion.
- [ ] Stealth/security specialist completion.
- [ ] Engineering/science specialist completion.
- [ ] Medicine/fieldcraft specialist completion.
- [ ] Influence/commerce specialist completion.

### Roentgen Focus

- [ ] Simulation fully pauses during planning.
- [ ] UI and tactical camera remain responsive.
- [ ] Every combat archetype provides valid target parts.
- [ ] Damaging target parts changes gameplay.
- [ ] Preview explains uncertainty.
- [ ] Queued action execution is reproducible.
- [ ] Accessibility options preserve usability.

### Generation

- [ ] Same seed reproduces the same semantic world.
- [ ] Decorative generator changes do not alter unrelated campaign bindings.
- [ ] Invalid structures enter repair before rejection.
- [ ] Repair attempts are bounded.
- [ ] Failures export provenance.
- [ ] Critical generators pass stress seed suites.

### Persistence

- [ ] Saves are atomic.
- [ ] Backups are created.
- [ ] Corruption is detected.
- [ ] Old generator behavior is pinned or migrated.
- [ ] Changed-world deltas survive unload and reload.
- [ ] Save size remains within budget for representative late-game campaigns.

### Audio and music

- [ ] No audio callback allocations in steady state.
- [ ] No graph compilation on realtime path.
- [ ] No underruns in stress suite.
- [ ] Weapon and creature identities remain acoustically stable across variations.
- [ ] Music transitions occur on valid musical boundaries.
- [ ] Final game requires no external SoundFont.

### Accessibility and input

- [ ] Keyboard-only completion path for menus.
- [ ] Mouse and keyboard gameplay path.
- [ ] Gamepad gameplay and menu path.
- [ ] Full remapping.
- [ ] Interface scaling.
- [ ] Subtitle sizing.
- [ ] Motion and flash reductions.
- [ ] Roentgen overlay intensity control.

## C. Phase-gate evidence

Every phase exit must include:

1. Executable demonstration or automated test.
2. Written acceptance report.
3. Known limitations.
4. Performance measurements.
5. Determinism results where applicable.
6. Migration impact.
7. Updated risk register.
8. Next-phase entry checklist.

## D. Severity policy

- **Critical:** Data loss, security issue, main campaign impossible, common startup crash, save corruption, unrecoverable install failure.
- **High:** Major system unavailable, frequent crash, common generator invalidity, severe performance failure, inaccessible core UI.
- **Medium:** Workaround exists; substantial incorrect behavior or repeated content defect.
- **Low:** Cosmetic, rare presentation issue, minor tuning problem.

Release candidate requirement:

- Zero known critical defects.
- Zero known high defects.
- Medium defects reviewed and explicitly accepted.
