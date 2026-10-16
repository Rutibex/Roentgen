# Milestones, Revelations, and Irreversible Choices

## Milestone plans

`GeneratedMilestonePlan` binds a semantic milestone kernel to a deterministic Phase 16 quest plan. It records sequence, region, principal factions, location, predecessor/successor IDs, revelation or choice IDs, required critical roles, eligibility day, deadline, and recovery policy.

The runtime states are `Planned`, `Eligible`, `Offered`, `Active`, `AwaitingDecision`, `Resolved`, `Failed`, `Recovered`, `Superseded`, or `LockedOut`. A recovered milestone satisfies successor prerequisites but remains visibly distinct from an ordinary resolution.

## Director operation

At fixed simulation boundaries the director:

1. observes active quest state;
2. moves offered quests to active;
3. resolves completed milestones or opens a required decision;
4. proves milestone-bound revelations;
5. records deadline failures;
6. selects the next eligible milestone only when no live milestone exists;
7. emits canonical campaign events for diagnostics and replay.

## Revelations

Each `RevelationDefinition` points to hidden historical facts and concrete evidence IDs. Evidence collection progresses from hidden to suspected to proven. Milestone completion may prove a revelation when the quest itself is the verified evidence path. The final revelation is required before convergence.

## Commitments

An irreversible choice creates a `CampaignCommitmentRecord` containing the choice, selected option, responsible actor, tick/day, facts known at decision time, and the exact flags added or removed. Rejected options become unavailable because the milestone resolves and the commitment is immutable.

Choice options can alter faction trust, fear, respect, campaign flags, convergence eligibility, and ending eligibility. They never rely on a global morality score.

## Failure and recovery

The campaign state distinguishes failure from bounded recovery. Critical campaign roles use replacement ladders rather than immortal named NPCs. A successor may be a named replacement, qualified faction member, office, document, terminal, or aftermath route. Replacement records preserve the fact that the original role holder was lost.
