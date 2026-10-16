# Crafting, Repair, and Loot

Crafting supports workbenches, chemistry stations, medical stations, energy benches, cooking fires, armor frames, and diagnostic tables. Recipes use exact definitions or semantic tags, deterministic skill resolution, timed activities, exact input selection, bounded costly success, deterministic child IDs, byproducts, and knowledge provenance.

Recipes may be learned from backgrounds, books, faction training, reverse engineering, experiments, property discovery, quests, or world discoveries. Closing the interface cannot reroll a result because a crafting plan stores its activity ID and resolution seed.

Repairs preserve item identity and consume compatible component items. The permitted maximum repair is constrained by plan quality, skill, and the selected parts. Jury-rigged repairs can be represented with lower condition ceilings and runtime tags.

Loot is generated per container domain. Opening order, sorting, save/load, or taking one item cannot reroll other slots. Once changed, the save records the item-world delta.
