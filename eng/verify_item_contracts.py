#!/usr/bin/env python3
from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
required=[
 'src/Roentgen.Items/ItemContracts.cs','src/Roentgen.Items/ItemDefinitions.cs','src/Roentgen.Items/ItemBlueprints.cs',
 'src/Roentgen.Items/ItemState.cs','src/Roentgen.Items/ItemWorldState.cs','src/Roentgen.Items/Inventory.cs',
 'src/Roentgen.Items/Transactions.cs','src/Roentgen.Items/Weapons.cs','src/Roentgen.Items/Ammunition.cs',
 'src/Roentgen.Items/Armor.cs','src/Roentgen.Items/Consumables.cs','src/Roentgen.Items/Repair.cs',
 'src/Roentgen.Items/Crafting.cs','src/Roentgen.Items/Loot.cs','src/Roentgen.Items/Economy.cs',
 'src/Roentgen.Items/Merchant.cs','src/Roentgen.Items/Validation.cs','src/Roentgen.Items/Phase13Content.cs','src/Roentgen.Items/ItemFactory.cs',
 'src/Roentgen.Combat/ItemCombatAdapter.cs','src/Roentgen.Rpg/Phase13ItemCapabilityProvider.cs',
 'src/Roentgen.Simulation/Phase13ItemInteractionBridge.cs','src/Roentgen.UI/Phase13ItemScreens.cs',
 'tools/Roentgen.ItemLab/Roentgen.ItemLab.csproj','tests/Roentgen.Items.Tests/Roentgen.Items.Tests.csproj']
missing=[x for x in required if not (root/x).is_file()]
if missing: raise SystemExit('Missing Phase 13 files: '+', '.join(missing))
text='\n'.join((root/x).read_text(errors='ignore') for x in required if x.endswith('.cs'))
for token in ['ItemInstanceState','ItemWorldState','ItemWorldCheckpoint','RestoreItemCheckpoint','ItemTransactionService','WeaponAssemblyCompiler','ArmorCapabilityCompiler','CraftingService','LootGenerator','SettlementEconomyState','MerchantService','ItemWorldValidator','NormalizeStartingDefinition']:
 if token not in text: raise SystemExit('Missing Phase 13 contract: '+token)
content=(root/'src/Roentgen.Items/Phase13Content.cs').read_text()
checks={
 'weapon families':len(re.findall(r'Family\("weapon\.',content)),
 'weapon components':len(re.findall(r'Add\("(?:receiver|barrel|grip|stock|feed|sight|power|mod)\.',content)),
 'ammunition':len(re.findall(r'Ammo\("ammo\.',content)),
 'armor':len(re.findall(r'Armor\("armor\.',content)),
 'consumables':len(re.findall(r'Consumable\("consumable\.',content)),
}
expected={'weapon families':6,'weapon components':24,'ammunition':8,'armor':7,'consumables':12}
for key,value in expected.items():
 if checks[key] != value: raise SystemExit(f'Expected {value} {key}, found {checks[key]}.')
if 'recipes.Take(30)' not in content: raise SystemExit('Phase 13 recipe catalog is not bounded to 30 recipes.')
if len(re.findall(r'new\("merchant\.',content)) != 4: raise SystemExit('Expected 4 merchant profiles.')
if len(re.findall(r'"clinic-storage"',content)) < 1 or 'industrial-ruin' not in content: raise SystemExit('Required loot profiles absent.')
print('Phase 13 item, equipment, crafting, loot, merchant, and economy contracts verified.')
