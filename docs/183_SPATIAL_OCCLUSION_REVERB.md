# Spatial Audio, Occlusion, and Reverb

Roentgen coordinates are converted from fixed millimetres to floating-point metres only at the audio boundary. Dynamic voices carry position, velocity, distance bounds, obstruction, occlusion, and acoustic zone.

Playroma now prepares both transmission and low-pass matrices for acoustic portals. A closed path lowers level and cutoff. Each active voice owns preallocated low-pass state; filter coefficients are smoothed in the render callback without allocation.

The initial bus graph includes dry world buses and small, medium, large, and outdoor reverb returns. Generated structures may later emit zone and portal definitions directly.
