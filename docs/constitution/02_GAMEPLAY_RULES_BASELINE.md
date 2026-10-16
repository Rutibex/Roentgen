# Gameplay Rules Baseline

These formulas are starting points for implementation and automated balance testing. Tuning changes do not require constitutional amendment unless they alter a system’s declared purpose.

## 1. Attribute allocation

Recommended character-creation baseline:

- Each attribute begins at 4.
- Minimum: 1.
- Maximum at creation: 10.
- The player receives 14 additional allocation points.
- Backgrounds may shift values but may not exceed creation limits.

This creates an average attribute near 6 without requiring every build to be broadly competent.

## 2. Derived statistics

### Maximum health

```text
MaxHealth = 60 + (Vitality × 10) + ((Level - 1) × (3 + floor(Vitality / 3)))
```

### Stamina

```text
MaxStamina = 70 + (Vitality × 8) + (Reflex × 4)
```

### Carry capacity in kilograms

```text
CarryCapacity = 20 + (Physique × 7)
```

A soft encumbrance band begins at 85% of capacity. Hard encumbrance begins above 100%.

### Focus capacity

```text
MaxFocus = 45 + (Awareness × 4) + (Reflex × 5)
```

### Focus recovery

Base recovery occurs outside committed Focus execution and may be modified by perks, drugs, injuries, and armor.

```text
FocusPerSecond = 3 + (Reflex × 0.25)
```

### Initiative

```text
Initiative = Awareness × 4 + Reflex × 3 + Fortune
```

### Critical reserve

Fortune affects critical-event probability through a bounded curve rather than direct linear multiplication.

```text
CriticalReserve = 2 + Fortune × 0.75
```

Critical reserve is a conceptual budget used by combat math and may not be shown directly.

## 3. Skill initialization

Each skill starts from an attribute contribution plus background modifiers.

Suggested mapping:

- Ballistics: Awareness and Reflex.
- Directed Energy: Awareness and Intellect.
- Demolitions: Awareness and Intellect.
- Melee: Physique and Reflex.
- Unarmed: Physique and Reflex.
- Athletics: Physique and Vitality.
- Stealth: Reflex and Awareness.
- Security: Reflex and Intellect.
- Engineering: Intellect and Awareness.
- Science: Intellect.
- Medicine: Intellect and Awareness.
- Fieldcraft: Awareness and Vitality.
- Influence: Presence and Awareness.
- Commerce: Presence and Intellect.

Baseline:

```text
StartingSkill = 5 + PrimaryAttribute × 3 + SecondaryAttribute × 2
```

Specialized skills receive +15 at creation.

## 4. Skill checks

Difficulty bands:

- Routine: 20.
- Trained: 35.
- Difficult: 50.
- Expert: 65.
- Exceptional: 80.
- Legendary: 95.

Default deterministic check:

```text
Margin = EffectiveSkill - Difficulty
```

- Margin 0 or greater: success.
- Margin -10 through -1: partial or costly success when the interaction supports it.
- Margin below -10: failure.

Fortune, tools, preparation, evidence, injury, time pressure, and companion assistance modify EffectiveSkill.

Randomized checks may be used for contested or chaotic actions, but the UI must communicate uncertainty.

## 5. Experience and levels

Recommended level thresholds use a smooth increasing curve:

```text
XPToNextLevel = 200 + 75 × Level + 15 × Level²
```

Experience should reward:

- Quest resolution.
- Discovery.
- Difficult skill use.
- Combat threats.
- Noncombat resolution.
- Major world changes.

Repeated trivial actions must not provide exploitable progression.

## 6. Skill points

Baseline per level:

```text
SkillPoints = 8 + floor(Intellect / 2)
```

Skill cost may increase above 75 to preserve specialization.

Suggested cost:

- Skill 0–50: 1 point per rank.
- 51–75: 2 points per rank.
- 76–100: 3 points per rank.

## 7. Perk cadence

- One perk point every even level.
- Some perks have ranks.
- A perk rank must change behavior or create a visible threshold whenever possible.
- Respec is not assumed for launch; limited retraining may exist through rare in-world procedures.

## 8. Accuracy model

The displayed chance should be derived from a transparent score.

```text
AccuracyScore = SkillContribution
              + WeaponAccuracy
              + AwarenessContribution
              + StanceBonus
              + AimBonus
              + EquipmentBonus
              - RangePenalty
              - TargetMotionPenalty
              - ShooterMotionPenalty
              - CoverPenalty
              - VisibilityPenalty
              - InjuryPenalty
              - RecoilPenalty
              - ConditionPenalty
              - TargetPartPenalty
```

Convert score to probability through a bounded logistic curve so extreme bonuses do not trivially produce impossible values.

Recommended visible bounds:

- Minimum ordinary displayed chance: 5%.
- Maximum ordinary displayed chance: 95%.
- Certain scripted or point-blank actions may exceed those bounds when logically guaranteed.

## 9. Roentgen Focus action costs

Action cost is based on time and difficulty:

```text
FocusCost = BaseActionTime × WeaponHandlingFactor × TargetPartFactor × InjuryFactor × PerkFactor
```

Examples:

- Quick sidearm shot: low cost.
- Heavy weapon shot: high cost.
- Reload: moderate to high cost depending on weapon.
- Targeting a small exposed component: additional cost.
- Moving a short tactical step may be supported later, but free movement during planning is not required for the first implementation.

## 10. Damage model

```text
RawDamage = WeaponBaseDamage × ChargeOrAttackFactor × CriticalFactor × ConditionFactor
```

Armor resolves in this order:

1. Coverage check.
2. Penetration interaction.
3. Flat threshold.
4. Percentage resistance.
5. Material-specific modifiers.
6. Part and injury modifiers.

Example baseline:

```text
PostThreshold = max(0, RawDamage - EffectiveThreshold)
FinalDamage = PostThreshold × (1 - EffectiveResistance)
```

Resistance is clamped before special effects.

## 11. Part health and injuries

Each target part has:

- Structural health.
- Injury thresholds.
- Disable threshold.
- Destroy threshold where appropriate.
- Linked functions.

Example humanoid consequences:

- Head: perception, consciousness, severe trauma.
- Torso: bleeding, breathing, organ trauma.
- Arm: accuracy, recoil control, melee use, weapon eligibility.
- Leg: speed, balance, evasion, sprinting.

Parts do not necessarily have independent hit-point bars visible to the player. The diagnostic interface may show condition bands.

## 12. Armor condition

Armor loses condition from absorbed damage, damage type, and material mismatch.

At low condition:

- Threshold declines.
- Resistance declines.
- Coverage may be compromised.
- Special functions may fail.

## 13. Encumbrance

- Below 85%: no penalty.
- 85–100%: increasing stamina and noise penalties.
- 100–120%: no sprint; severe stamina penalty.
- Above 120%: movement heavily restricted; fast travel prohibited.

## 14. Stealth

Detection compares observer sensing against target signature.

```text
DetectionScore = ObserverAwareness
               + SensorQuality
               + Alertness
               + LightingAdvantage
               - TargetStealth
               - Cover
               - Distance
               - EnvironmentalMasking
```

Noise and visual detection are tracked separately.

## 15. Reputation

Each major faction stores independent bounded values:

```text
Trust: -100 to 100
Fear: 0 to 100
Respect: -100 to 100
Obligation: -100 to 100
IdeologicalAlignment: -100 to 100
KnownCrimes: 0 to 100
```

Faction reactions use combinations rather than one aggregate number.

## 16. Difficulty

Difficulty should be decomposed into options rather than one opaque multiplier:

- Player damage received.
- Enemy damage received.
- Resource scarcity.
- Injury severity.
- Survival pressure.
- Roentgen Focus recovery.
- Enemy tactical intelligence.
- Detection sensitivity.
- Save restrictions, if any, must remain optional.

## 17. Currency

Phase 0 does not canonically name the setting’s currency. It must not use bottle caps as the universal currency. The economy schema therefore uses generic `valueUnits` until the setting bible defines one or more currencies.
