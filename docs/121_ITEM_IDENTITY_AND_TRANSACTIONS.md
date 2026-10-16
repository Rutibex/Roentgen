# Item Identity and Transactions

Every item instance has a `StableId`, definition ID, optional generated blueprint, location, quantity, condition, runtime tags, charges, installed components, and revision. Generated items also preserve their seed, generator version, material selection, manufacturer history, faction alteration, asset descriptor, and fingerprint.

All mutations use `ItemTransactionService`. Preconditions bind a transaction to expected item revisions. Validation runs before mutation, so rejected commands leave the item world unchanged. Reservations prevent Focus queues, crafting activities, merchants, reloads, and ordinary transfers from claiming the same quantity.

Supported operations include transfer, split, merge, equip, unequip, consume, condition damage, repair, reserve, and release. The model is intentionally extensible for detachable magazines, quest custody, theft, and companion inventories.
