using Roentgen.Contracts;
using Roentgen.Core;

namespace Roentgen.Simulation;

public sealed record PlayerIntentCommand(PlayerIntentFrame Intent):ISimulationCommand
{
    public string TypeId=>"roentgen.player.intent";public int SchemaVersion=>1;public void WritePayload(CanonicalWriter writer){writer.WriteInt32(Intent.MoveX);writer.WriteInt32(Intent.MoveY);writer.WriteInt32(Intent.LookYawDelta);writer.WriteInt32(Intent.LookPitchDelta);writer.WriteInt32((int)Intent.Buttons);writer.WriteInt32((int)Intent.Device);}
}
public sealed record PlayerInteractionCommand(StableId PlayerId,StableId TargetId,InteractionVerb Verb):ISimulationCommand
{
    public string TypeId=>"roentgen.player.interact";public int SchemaVersion=>1;public void WritePayload(CanonicalWriter writer){writer.WriteStableId(PlayerId);writer.WriteStableId(TargetId);writer.WriteInt32((int)Verb);}
}
public sealed record TransferContainerObjectsCommand(StableId PlayerId,StableId ContainerId,bool TransferAll):ISimulationCommand
{
    public string TypeId=>"roentgen.player.container-transfer";public int SchemaVersion=>1;public void WritePayload(CanonicalWriter writer){writer.WriteStableId(PlayerId);writer.WriteStableId(ContainerId);writer.WriteBoolean(TransferAll);}
}

public sealed record InteractionSucceededEvent(StableId PlayerId,StableId TargetId,InteractionVerb Verb):ISimulationEvent
{
    public string TypeId=>"roentgen.interaction.succeeded";public int SchemaVersion=>1;public void WritePayload(CanonicalWriter writer){writer.WriteStableId(PlayerId);writer.WriteStableId(TargetId);writer.WriteInt32((int)Verb);}
}
public sealed record InteractionRejectedEvent(StableId PlayerId,StableId TargetId,InteractionVerb Verb,InteractionFailureReason Reason):ISimulationEvent
{
    public string TypeId=>"roentgen.interaction.rejected";public int SchemaVersion=>1;public void WritePayload(CanonicalWriter writer){writer.WriteStableId(PlayerId);writer.WriteStableId(TargetId);writer.WriteInt32((int)Verb);writer.WriteInt32((int)Reason);}
}
