# Weapons, Ammunition, and Armor

`WeaponAssemblyCompiler` compiles a weapon family plus installed component items and condition into one immutable capability snapshot. Receiver, barrel, grip, stock, feed, sight, power, and modification slots contribute bounded changes to damage, capacity, range, spread, recoil, reliability, attack timing, reload timing, mass, assets, and acoustic identity.

Reserve ammunition is no longer a conceptual integer in the item domain. Ammunition exists as item stacks, and each weapon owns an authoritative feed state containing ammunition definition, loaded count, capacity, and chamber state. Focus reservations use the same item quantities.

`ArmorCapabilityCompiler` projects equipped armor items into ordered layers while retaining each source item ID. Combat can therefore return condition damage to the exact plate, garment, helmet, or respirator that stopped the attack.
