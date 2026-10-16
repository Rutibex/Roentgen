using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Factions;

public interface ISocietyCommand : ICanonicalWritable
{
    StableId Id { get; }
    ulong ExpectedRevision { get; }
    string Kind { get; }
}

public sealed record SocietyCommandResult(StableId CommandId, SocietyCommandStatus Status, string Message, StateChecksum BeforeChecksum, StateChecksum AfterChecksum);
public sealed record ChangeFactionPolicyCommand(StableId Id, ulong ExpectedRevision, StableId FactionId, string PolicyId, int ValuePermille) : ISocietyCommand { public string Kind => "change-faction-policy"; public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteUInt64(ExpectedRevision); writer.WriteStableId(FactionId); writer.WriteString(PolicyId); writer.WriteInt32(ValuePermille); } }
public sealed record TransferSettlementControlCommand(StableId Id, ulong ExpectedRevision, StableId SettlementId, StableId NewFactionId) : ISocietyCommand { public string Kind => "transfer-settlement-control"; public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteUInt64(ExpectedRevision); writer.WriteStableId(SettlementId); writer.WriteStableId(NewFactionId); } }
public sealed record SetTradeRouteActiveCommand(StableId Id, ulong ExpectedRevision, StableId TradeRouteId, bool Active) : ISocietyCommand { public string Kind => "set-trade-route-active"; public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteUInt64(ExpectedRevision); writer.WriteStableId(TradeRouteId); writer.WriteBoolean(Active); } }
public sealed record RecordCrimeCommand(StableId Id, ulong ExpectedRevision, CrimeKind CrimeKind, StableId JurisdictionFactionId, StableId LocationId, StableId SuspectedActorId, int SeverityPermille, IReadOnlyList<StableId> WitnessIds, int IdentityConfidencePermille, IReadOnlyList<StableId>? EvidenceIds = null) : ISocietyCommand
{
    public string Kind => "record-crime";
    public void WriteCanonical(CanonicalWriter writer)
    {
        writer.WriteStableId(Id); writer.WriteUInt64(ExpectedRevision); writer.WriteInt32((int)CrimeKind); writer.WriteStableId(JurisdictionFactionId); writer.WriteStableId(LocationId); writer.WriteStableId(SuspectedActorId); writer.WriteInt32(SeverityPermille);
        writer.WriteCount(WitnessIds.Count); foreach (StableId witness in WitnessIds.Order()) writer.WriteStableId(witness); writer.WriteInt32(IdentityConfidencePermille);
        IReadOnlyList<StableId> evidence = EvidenceIds ?? Array.Empty<StableId>(); writer.WriteCount(evidence.Count); foreach (StableId evidenceId in evidence.Order()) writer.WriteStableId(evidenceId);
    }
}
public sealed record ApplyReputationEvidenceCommand(StableId Id, ulong ExpectedRevision, ReputationEvidenceState Evidence) : ISocietyCommand { public string Kind => "apply-reputation-evidence"; public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteUInt64(ExpectedRevision); Evidence.WriteCanonical(writer); } }
public sealed record VacateLeadershipCommand(StableId Id, ulong ExpectedRevision, StableId FactionId) : ISocietyCommand { public string Kind => "vacate-leadership"; public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteUInt64(ExpectedRevision); writer.WriteStableId(FactionId); } }
public sealed record StartConflictCommand(StableId Id, ulong ExpectedRevision, StableId AggressorFactionId, StableId DefenderFactionId, StableId LocationId, StrategicConflictStage Stage, int PlayerRelevancePermille) : ISocietyCommand { public string Kind => "start-conflict"; public void WriteCanonical(CanonicalWriter writer) { writer.WriteStableId(Id); writer.WriteUInt64(ExpectedRevision); writer.WriteStableId(AggressorFactionId); writer.WriteStableId(DefenderFactionId); writer.WriteStableId(LocationId); writer.WriteInt32((int)Stage); writer.WriteInt32(PlayerRelevancePermille); } }

public sealed class SocietyCommandProcessor
{
    private readonly IReadOnlySet<StableId> campaignProtectedSettlements;
    public SocietyCommandProcessor(IReadOnlySet<StableId>? campaignProtectedSettlements = null) => this.campaignProtectedSettlements = campaignProtectedSettlements ?? new SortedSet<StableId>();

    public SocietyCommandResult Execute(SocietyWorldState world, ISocietyCommand command)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(command); StateChecksum before = world.ComputeChecksum(); if (command.ExpectedRevision != world.Revision) return new SocietyCommandResult(command.Id, SocietyCommandStatus.Rejected, $"Expected revision {command.ExpectedRevision}, found {world.Revision}.", before, before);
        byte[] rollback = world.ToCanonicalPayload();
        try
        {
            string message = command switch
            {
                ChangeFactionPolicyCommand value => ChangePolicy(world, value),
                TransferSettlementControlCommand value => TransferControl(world, value),
                SetTradeRouteActiveCommand value => SetRoute(world, value),
                RecordCrimeCommand value => RecordCrime(world, value),
                ApplyReputationEvidenceCommand value => ApplyEvidence(world, value),
                VacateLeadershipCommand value => VacateLeadership(world, value),
                StartConflictCommand value => StartConflict(world, value),
                _ => throw new InvalidOperationException($"Unsupported society command '{command.Kind}'.")
            };
            world.Touch();
            return new SocietyCommandResult(command.Id, SocietyCommandStatus.Committed, message, before, world.ComputeChecksum());
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            world.RestoreFrom(rollback); return new SocietyCommandResult(command.Id, SocietyCommandStatus.Rejected, exception.Message, before, before);
        }
    }

    private static string ChangePolicy(SocietyWorldState world, ChangeFactionPolicyCommand command) { world.GetFaction(command.FactionId).SetPolicy(command.PolicyId, command.ValuePermille); return $"Policy '{command.PolicyId}' updated."; }
    private string TransferControl(SocietyWorldState world, TransferSettlementControlCommand command)
    {
        SettlementState settlement = world.GetSettlement(command.SettlementId); if (campaignProtectedSettlements.Contains(settlement.Id) && command.NewFactionId.IsEmpty) throw new InvalidOperationException("Campaign-protected settlement cannot lose all controlling authority."); FactionState oldFaction = world.GetFaction(settlement.ControllingFactionId); FactionState newFaction = world.GetFaction(command.NewFactionId); oldFaction.RemoveControlledSettlement(settlement.Id); newFaction.AddControlledSettlement(settlement.Id); settlement.ChangeControl(newFaction.Id); world.TransferTerritoryControl(settlement.Id, oldFaction.Id, newFaction.Id, settlement.Id); world.RecordEvent(new SocietyEventRecord(StableIdFactory.CreateChild(settlement.Id, "control-event", checked((long)world.Day)), SocietyEventKind.TerritoryChanged, world.Day, settlement.Id, newFaction.Id, 0, "Settlement control transferred.")); return $"Control of '{settlement.Plan.DisplayName}' transferred to '{newFaction.Plan.DisplayName}'.";
    }
    private static string SetRoute(SocietyWorldState world, SetTradeRouteActiveCommand command) { world.GetTradeRoute(command.TradeRouteId).SetActive(command.Active); return command.Active ? "Trade route activated." : "Trade route suspended."; }
    private static string RecordCrime(SocietyWorldState world, RecordCrimeCommand command)
    {
        var crime = new CrimeRecordState(command.Id, command.CrimeKind, command.JurisdictionFactionId, command.LocationId, command.SuspectedActorId, command.SeverityPermille, world.Day);
        foreach (StableId witness in command.WitnessIds.Order()) crime.AddWitness(witness, command.IdentityConfidencePermille);
        foreach (StableId evidenceId in (command.EvidenceIds ?? Array.Empty<StableId>()).Order()) crime.AddEvidence(evidenceId);
        if (crime.EvidenceIds.Count > 0) crime.SetIdentityConfidence(command.IdentityConfidencePermille);
        world.AddCrime(crime);
        return $"Crime '{command.CrimeKind}' recorded with {command.WitnessIds.Count} witnesses and {command.EvidenceIds?.Count ?? 0} evidence records.";
    }
    private static string ApplyEvidence(SocietyWorldState world, ApplyReputationEvidenceCommand command) { world.AddReputationEvidence(command.Evidence); return "Reputation evidence recorded."; }
    private static string VacateLeadership(SocietyWorldState world, VacateLeadershipCommand command) { world.GetLeadership(command.FactionId).VacateLeader(world.Day); return "Highest office vacated."; }
    private static string StartConflict(SocietyWorldState world, StartConflictCommand command) { if (command.AggressorFactionId == command.DefenderFactionId) throw new InvalidOperationException("Faction cannot start a conflict with itself."); _ = world.GetFaction(command.AggressorFactionId); _ = world.GetFaction(command.DefenderFactionId); var conflict = new StrategicConflictState(command.Id, command.AggressorFactionId, command.DefenderFactionId, command.LocationId, command.Stage); conflict.SetPlayerRelevance(command.PlayerRelevancePermille); world.AddConflict(conflict); return $"Conflict started at stage {command.Stage}."; }
}
