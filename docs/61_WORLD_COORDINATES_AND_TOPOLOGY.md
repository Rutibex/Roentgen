# World Coordinates and Regional Topology

## Units and addressing

Authoritative horizontal positions are signed 32-bit centimetres. Rendering converts centimetres to metres only at the bridge. Chunk addressing uses mathematical floor division, so negative coordinates map correctly.

The Phase 6 profile is centered on world origin and spans 2,048 × 2,048 metres. It contains sixteen chunks per axis. Chunk addresses range from `-8` through `7`; each chunk is 128 metres.

## Global sample rule

A terrain vertex is evaluated from its absolute world coordinate, never from an independently seeded local border. Adjacent chunks therefore ask the same evaluator for the same shared edge coordinates. This rule is the primary crack-prevention invariant.

## Topology fields

Every field uses the same 16 metre regional grid:

- elevation in centimetres;
- moisture from 0 through 65,535;
- contamination from 0 through 65,535;
- industrial disturbance from 0 through 65,535;
- settlement suitability from 0 through 65,535;
- one canonical region-family identifier per cell.

Fields are immutable after generation and contribute to the topology checksum.

## Region classification

Phase 6 deliberately proves three visible prototype identities:

- **Glass Basin:** exposed, dry, fused terrain.
- **Broken Meridian:** the depressed industrial transport corridor.
- **Blackwater Works:** wet and contaminated lowland.

The enum also reserves Ossuary Fields, Red Quarry Country and Quiet Range for later content completion without changing persisted identifiers.

## Roads and trails

Roads are regional graph products. A deterministic A* search scores slope, moisture, contamination and industrial disturbance. The graph is generated before terrain detail, then the height evaluator flattens and feathers the reserved corridor. Roads therefore cross chunk boundaries continuously.

## Landmark reservations

Reservations are stable world facts, not decorative props. Each stores purpose, center, half extents, target elevation, maximum slope and road-access requirement. Phase 7 structure generators must honor these parcels rather than searching arbitrary finished terrain.
