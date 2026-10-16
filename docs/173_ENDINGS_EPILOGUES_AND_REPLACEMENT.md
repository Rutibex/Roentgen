# Convergence, Endings, Epilogues, and Critical-Role Replacement

## Final convergence

`ConvergenceDefinition` binds the final convergence milestone to a deterministic set of forms selected from the campaign premise:

- multi-faction negotiation;
- siege or assault;
- evacuation;
- system repair;
- system shutdown;
- deliberate exposure;
- containment;
- transfer of control;
- destruction;
- controlled transformation.

Convergence requires both irreversible choices and the final proven revelation. It applies explicit campaign flags used by ending eligibility; it does not infer an ending from a single morality value.

## Ending families

Phase 17 supplies eight ending families, including repair and disclosure, negotiated containment, controlled transformation, evacuation, shutdown, single-faction custody, destructive liberation, and a bounded fallback survival ending. Each ending declares typed requirements and a priority. The highest-priority eligible family is selected deterministically.

## Epilogues

Epilogue rules are evaluated for the player, factions, settlements, and world outcomes. One highest-priority rule is selected per subject. Every `CampaignEpilogueRecord` stores the rule ID and the requirement reasons supporting its text, preventing an ending from referring to state that does not exist.

## Critical roles

Campaign content binds semantic roles rather than assuming one irreplaceable actor. Eight initial roles cover medical authority, negotiation, historical testimony, diagnostic records, settlement representation, opposition command, technical expertise, and final-system custody.

`CampaignDirector.RepairCriticalRoles` applies a deterministic fallback order. Documents, machines, and aftermath roles can preserve campaign completion while still changing dialogue and consequences. The repair record is canonical and survives save/load.
