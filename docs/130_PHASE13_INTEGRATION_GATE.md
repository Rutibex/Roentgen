# Phase 13 Integration Gate

Phase 13 is no longer an isolated item-domain library. The playable regression campaign now constructs one authoritative `Phase13ItemRuntime` and projects combat weapons, loaded ammunition, reserves, armor layers, consumables, crafting, repair, trade, loot-facing inventory, and persistence from its `ItemWorldState`.

## Authority flow

1. Character creation grants are instantiated into the player inventory.
2. Generated item IDs are also the IDs registered with the combat runtime.
3. Weapon components compile into combat definitions; loaded ammunition and item condition compile into runtime state.
4. Each combat update commits ammunition use, reserve use, weapon damage, and armor damage back to the same item instances.
5. Inventory, equipment, merchant, and crafting screens read presentation models from the item world and submit typed operations through the application runtime.
6. Item campaign snapshots are versioned, length-bounded, SHA-256 protected, and restore the item world, recipe knowledge, active weapon selection, and throwable selection through canonical readers.

## Playable controls

- `I`: inventory; Enter equips/uses, `R` repairs with an available material.
- `M`: merchant; Enter completes the selected buy or sell operation, and `Tab` switches between merchant stock and player inventory.
- `B`: crafting; Enter attempts the selected known recipe using available inventory.
- `O`: equipment overview.
- `V`: inspect the selected item.
- `F5` / `F9`: save and restore the complete Phase 13 regression snapshot, including learned recipes and active equipment selections.

## Gate

The regression campaign can loot or hold stable item identities, equip real generated weapons and layered armor, consume actual ammunition, suffer condition damage, repair, craft, trade, and round-trip the authoritative item state without a parallel conceptual inventory.
