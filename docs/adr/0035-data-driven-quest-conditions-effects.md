# ADR 0035 — Data-Driven Quest Conditions and Effects

Accepted.

Quest logic uses serializable typed conditions and effects rather than arbitrary delegates. This permits deterministic validation, save migration, graph solving, editor inspection, and replay. External effects are routed through typed domain requests.
