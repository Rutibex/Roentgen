# Player guide (development preview)

## Starting a campaign

Run `Roentgen.App` and use the main menu. Choose **New Campaign**, configure the seed and character, then enter the current small generated region. The game also exposes Continue, Load, Settings, Accessibility, Tutorials, Credits and Quit screens. If an old save is rejected after an upgrade, start a new campaign without deleting the old save files.

## Navigation and interaction

- **W/A/S/D**: movement; use the **mouse** to look around. **Space** jumps, **Left Shift** sprints, **Ctrl/C** crouches.
- **E**: interact with an available nearby object or character. Some dialogue, trade and crafting actions require appropriate distance, visibility, and a living/valid actor or station.
- **M**: regional map, showing terrain, roads, heading and waypoints. Pan/zoom and interior-floor map features are not complete.
- **J**: quest journal; **F**: factions; **H**: companions.

## Combat and character

**Inventory, equipment and crafting:** Inventory management and equipment are available from the dedicated screens; some crafting operations require a nearby station.


- **Left click** attack; **right click** aim/secondary attack; **R** reload; **1/2/3** select sidearm/rifle/melee; **G** throw grenade.
- **V** opens Roentgen Focus to queue targeted actions; actions consume Focus and are executed by the simulation.
- **I** opens inventory, **O** equipment, **P** character, **K** skills, **L** perks, **B** crafting, **T** trade.

## Saving and options

- **Escape** pauses; **F5** quick saves and **F9** quick loads in supported contexts.
- In Settings and Accessibility, customize bindings, interface/subtitle scale, contrast, motion reduction, aim/crouch/sprint hold/toggle and other presentation options.
- UI buttons and tabs support mouse and keyboard; gamepad mappings exist but have not been verified on physical controllers during this packaging pass.

**Known limits:** this is a prototype: not all locations are fully traversable, content combinations have not received full QA, several player interactions have context-specific shortcuts, and quest resolution routes may still be incomplete. See [Troubleshooting](TROUBLESHOOTING.md).
