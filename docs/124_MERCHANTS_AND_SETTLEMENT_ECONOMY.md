# Merchants and Settlement Economy

Merchant stock is composed of real item instances while active. Trade quotes bind item lines to buyer and merchant inventory revisions and an expiration tick. Confirming a valid quote transfers every line atomically.

Settlement economies simulate strategic commodity stocks rather than every individual object. Each economy tracks production, consumption, trade routes, preferred and prohibited goods, currency policy, taxes, and merchant risk. Daily fixed-point updates keep stock nonnegative.

Prices combine base value, item condition, bounded scarcity, merchant spread, tax, risk, relationship, and Commerce capability. Hard minimum and maximum values prevent collapse and runaway inflation. Immediate buy/sell loops retain a negative spread for the player, preventing trivial arbitrage.
