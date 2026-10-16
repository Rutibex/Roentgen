# Phase 21 Validation Strategy

## Commit tier

Every content change runs JSON parsing, checksum verification, exact budget checks, ID and reference checks, source-manifest verification, Phase 21 contract verification, and Phase 21 golden-vector verification.

## Native test tier

`Roentgen.Phase21.Tests` verifies:

- every launch budget;
- canonical binary compilation and round-trip;
- launch adapters for RPG, items, society, narrative, campaign, and world content;
- the 8 × 8 kilometre launch profile;
- the 70% behavioral-perk requirement;
- all 157 semantic audio profiles and all 44 adaptive music bindings;
- all creature, robot, and humanoid target-part profiles;
- all 18 tutorial bodies through the product UI catalog.

## Nightly tier

The existing world, structure, navigation, campaign, audio, music, save, and soak runners should be invoked across hundreds of campaigns using the Phase 21 content fingerprint. Reports must retain seed, content fingerprint, generator versions, and repair history.

## Release-tier invariants

- Same seed, command history, generator versions, and content fingerprint produce the same checksums.
- Every major quest has three or more routes and at least one noncombat route.
- Every creature and robot exposes consequential target parts.
- Every structure supports a validated alternate route.
- Every major faction, companion, weapon, armor family, creature, robot, region, and interface family has semantic audio coverage.
- Every region, faction, and companion has a valid adaptive music binding using the retained SoundFont path.
