using Roentgen.Contracts;
using Roentgen.Core;
using Roentgen.Entities;
using Roentgen.Simulation;

namespace Roentgen.Combat;

public sealed record CombatFeedbackEvent(CombatFeedbackEntry Entry) : ISimulationEvent
{
    public string TypeId => "roentgen.combat.feedback";
    public int SchemaVersion => 1;
    public void WritePayload(CanonicalWriter writer) => Entry.WriteCanonical(writer);
}
